using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.Enums.Type;
using Common.Settings;
using DAL.Entities;
using DAL.UnitOfWork;
using Microsoft.EntityFrameworkCore; // Vẫn cần để dùng Include
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class TripDriverAssignmentService : ITripDriverAssignmentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;
        private readonly ITripDriverContractService _tripDriverContractService;
        private readonly ITripDeliveryRecordService _tripDeliveryRecordService;
        private readonly IDeliveryRecordTemplateService _templateService;
        private readonly IVietMapService _vietMapService;
        private readonly ITripVehicleHandoverRecordService _vehicleHandoverService;
        private readonly IFirebaseUploadService _fileService;
        private readonly IDriverWorkSessionService _driverWorkSessionService;

        public TripDriverAssignmentService(
            IUnitOfWork unitOfWork,
            UserUtility userUtility,
            ITripDriverContractService tripDriverContractService,
            ITripDeliveryRecordService tripDeliveryRecordService,
            IDeliveryRecordTemplateService templateService,
            IVietMapService vietMapService,
            ITripVehicleHandoverRecordService vehicleHandoverService,
            IFirebaseUploadService fileService,
            IDriverWorkSessionService driverWorkSessionService)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
            _tripDriverContractService = tripDriverContractService;
            _tripDeliveryRecordService = tripDeliveryRecordService;
            _templateService = templateService;
            _vietMapService = vietMapService;
            _vehicleHandoverService = vehicleHandoverService;
            _fileService = fileService;
            _driverWorkSessionService = driverWorkSessionService;
        }

        // =========================================================================================================
        // 1. OWNER GÁN TÀI XẾ (ASSIGNMENT BY OWNER)
        // =========================================================================================================
        public async Task<ResponseDTO> CreateAssignmentByOwnerAsync(CreateAssignmentDTO dto)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var ownerId = _userUtility.GetUserIdFromToken();
                var userRole = _userUtility.GetUserRoleFromToken();

                // 1. Validate Quyền
                if (ownerId == Guid.Empty || userRole != "Owner")
                    return new ResponseDTO("Unauthorized: Chỉ 'Owner' mới có thể gán tài xế.", 401, false);

                // 2. Validate Trip
                var trip = await _unitOfWork.TripRepo.GetByIdAsync(dto.TripId);
                if (trip == null) return new ResponseDTO("Trip not found.", 404, false);
                if (trip.OwnerId != ownerId) return new ResponseDTO("Forbidden: Bạn không sở hữu chuyến đi này.", 403, false);

                if (trip.Status != TripStatus.PENDING_DRIVER_ASSIGNMENT)
                {
                    return new ResponseDTO("Không thể gán tài xế khi chuyến đi đang chờ ký hợp đồng (AWAITING_OWNER_CONTRACT).", 400, false);
                }

                // 3. Validate Driver
                var driver = await _unitOfWork.DriverRepo.GetByIdAsync(dto.DriverId);
                if (driver == null) return new ResponseDTO("Driver not found.", 404, false);

                if (!driver.HasDeclaredInitialHistory)
                {
                    // Trả về mã lỗi đặc biệt (VD: 428 Precondition Required) để Frontend biết đường hiển thị Popup
                    return new ResponseDTO(
                        "Bạn cần cập nhật lịch sử lái xe trong tuần hiện tại trước khi nhận chuyến đi đầu tiên.",
                        428,
                        false
                    );
                }
                // ----------------------------------------

                // --- [MỚI] CHECK 2: QUY ĐỊNH 10H/48H ---
                // Gọi Service để tính toán giờ lái của tài xế này
                var availability = await _driverWorkSessionService.CheckDriverAvailabilityAsync(dto.DriverId);
                if (!availability.CanDrive)
                {
                    return new ResponseDTO(
                        $"Không thể gán: Tài xế đã hết giờ lái. {availability.Message}",
                        400,
                        false
                    );
                }
                // ----------------------------------------

                // 4. Check Duplicate
                bool isDriverAlreadyInTrip = await _unitOfWork.TripDriverAssignmentRepo.AnyAsync(
                    a => a.TripId == dto.TripId && a.DriverId == dto.DriverId
                );
                if (isDriverAlreadyInTrip) return new ResponseDTO("Driver is already assigned to this trip.", 400, false);

                // 5. Check Main Driver Constraint
                bool isMainDriver = dto.Type == DriverType.PRIMARY;
                if (isMainDriver)
                {
                    bool mainDriverExists = await _unitOfWork.TripDriverAssignmentRepo.AnyAsync(
                        a => a.TripId == dto.TripId && a.Type == DriverType.PRIMARY
                    );
                    if (mainDriverExists) return new ResponseDTO("This trip already has a main driver assigned.", 400, false);
                }

                // 6. Geocode
                var startLocationObj = await _vietMapService.GeocodeAsync(dto.StartLocation) ?? new Common.ValueObjects.Location(dto.StartLocation, 0, 0);
                var endLocationObj = await _vietMapService.GeocodeAsync(dto.EndLocation) ?? new Common.ValueObjects.Location(dto.EndLocation, 0, 0);

                // 7. Tạo Assignment (Trạng thái OFFERED - Chờ tài xế accept & đóng cọc)
                var newAssignment = new TripDriverAssignment
                {
                    TripDriverAssignmentId = Guid.NewGuid(),
                    TripId = dto.TripId,
                    DriverId = dto.DriverId,
                    Type = dto.Type,
                    CreateAt = DateTime.UtcNow,
                    UpdateAt = DateTime.UtcNow,
                    BaseAmount = dto.BaseAmount,
                    BonusAmount = dto.BonusAmount,
                    StartLocation = startLocationObj,
                    EndLocation = endLocationObj,

                    // [QUAN TRỌNG] Set là OFFERED vì tài xế chưa đồng ý
                    AssignmentStatus = AssignmentStatus.ACCEPTED,
                    PaymentStatus = DriverPaymentStatus.UN_PAID,

                    // [NEW] KHỞI TẠO CÁC TRƯỜNG CHECK-IN / CHECK-OUT (Mặc định chưa làm gì cả)
                    IsOnBoard = false,
                    OnBoardTime = null,
                    OnBoardLocation = null,
                    OnBoardImage = null,
                    CheckInNote = null,

                    IsFinished = false,
                    OffBoardTime = null,
                    OffBoardLocation = null,
                    OffBoardImage = null,
                    CheckOutNote = null,

                    // [TIỀN CỌC] Set là PENDING
                    DepositAmount = 0,
                    DepositStatus = DepositStatus.NOT_REQUIRED
                };

                await _unitOfWork.TripDriverAssignmentRepo.AddAsync(newAssignment);

                // 8. (Optional) Gửi Notification cho Driver tại đây
                // await _notificationService.SendAsync(dto.DriverId, "Lời mời chuyến đi", $"Bạn được mời chạy chuyến {trip.TripCode}. Cọc: {dto.DepositAmount:N0}đ");

                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync();

                return new ResponseDTO("Đã gửi lời mời cho tài xế.", 201, true, new { assignmentId = newAssignment.TripDriverAssignmentId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO($"Error assigning driver: {ex.Message}", 500, false);
            }
        }

        // =========================================================================================================
        // 2. TÀI XẾ ỨNG TUYỂN (APPLY POST TRIP) - [UPDATED: Trừ Tiền Cọc]
        // =========================================================================================================
        public async Task<ResponseDTO> CreateAssignmentByPostTripAsync(CreateAssignmentByPostTripDTO dto)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                // 1. Validate Driver
                var driverId = _userUtility.GetUserIdFromToken();
                if (driverId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                var driver = await _unitOfWork.DriverRepo.GetByIdAsync(driverId);
                if (driver == null) return new ResponseDTO("Driver not found.", 404, false);
                if (!driver.HasDeclaredInitialHistory)
                {
                    // Trả về mã lỗi đặc biệt (VD: 428 Precondition Required) để Frontend biết đường hiển thị Popup
                    return new ResponseDTO(
                        "Bạn cần cập nhật lịch sử lái xe trong tuần hiện tại trước khi nhận chuyến đi đầu tiên.",
                        428,
                        false
                    );
                }
                // ----------------------------------------

                // --- [MỚI] CHECK 2: QUY ĐỊNH 10H/48H ---
                var availability = await _driverWorkSessionService.CheckDriverAvailabilityAsync(driverId);
                if (!availability.CanDrive)
                {
                    return new ResponseDTO(
                        $"Bạn không thể nhận chuyến do đã quá giờ lái quy định. {availability.Message}",
                        400,
                        false
                    );
                }
                // ----------------------------------------

                // 2. Lấy PostTrip
                var postTrip = await _unitOfWork.PostTripRepo.GetAll()
                    .Include(p => p.PostTripDetails)
                    .FirstOrDefaultAsync(p => p.PostTripId == dto.PostTripId);

                if (postTrip == null) return new ResponseDTO("PostTrip not found.", 404, false);
                if (postTrip.Status != PostStatus.OPEN) return new ResponseDTO("Bài đăng này đã đóng.", 400, false);

                // 3. Validate Slot
                var postDetail = postTrip.PostTripDetails.FirstOrDefault(d => d.PostTripDetailId == dto.PostTripDetailId);
                if (postDetail == null) return new ResponseDTO("Slot details not found.", 404, false);
                if (postDetail.RequiredCount <= 0) return new ResponseDTO("Vị trí này đã đủ người.", 400, false);

                // 4. Lấy Trip
                var trip = await _unitOfWork.TripRepo.GetAll()
                    .Include(t => t.TripRoute)
                    .Include(t => t.ShippingRoute).ThenInclude(r => r.StartLocation)
                    .Include(t => t.ShippingRoute).ThenInclude(r => r.EndLocation)
                    .FirstOrDefaultAsync(t => t.TripId == postTrip.TripId);

                if (trip == null) return new ResponseDTO("Trip not found.", 404, false);

                // 5. Check Duplicate Application
                bool alreadyApplied = await _unitOfWork.TripDriverAssignmentRepo.AnyAsync(
                    a => a.TripId == postTrip.TripId && a.DriverId == driverId
                );
                if (alreadyApplied) return new ResponseDTO("Bạn đã ứng tuyển chuyến này rồi.", 409, false);

                // 6. Check Main Driver Constraint
                bool isMainDriver = (postDetail.Type == DriverType.PRIMARY);
                if (isMainDriver)
                {
                    bool mainDriverExists = await _unitOfWork.TripDriverAssignmentRepo.AnyAsync(
                        a => a.TripId == trip.TripId && a.Type == DriverType.PRIMARY && a.AssignmentStatus == AssignmentStatus.ACCEPTED
                    );
                    if (mainDriverExists) return new ResponseDTO("Chuyến này đã có tài xế chính.", 400, false);
                }

                // =========================================================================
                // [NEW] XỬ LÝ TIỀN CỌC (QUAN TRỌNG)
                // =========================================================================
                decimal depositAmount = postDetail.DepositAmount;
                DepositStatus depositStatus = DepositStatus.NOT_REQUIRED;

                if (depositAmount > 0)
                {
                    // A. Lấy ví Driver
                    var driverWallet = await _unitOfWork.WalletRepo.FirstOrDefaultAsync(w => w.UserId == driverId);
                    if (driverWallet == null) return new ResponseDTO("Ví tài xế không tồn tại.", 400, false);

                    // B. Kiểm tra số dư
                    if (driverWallet.Balance < depositAmount)
                    {
                        // [FAIL FAST] Không đủ tiền -> Báo lỗi luôn, không cho nợ
                        return new ResponseDTO($"Số dư ví không đủ để đặt cọc ({depositAmount:N0}đ). Vui lòng nạp thêm tiền.", 402, false);
                    }

                    // C. Trừ tiền Driver (Logic tiền trao cháo múc)
                    driverWallet.Balance -= depositAmount;
                    driverWallet.LastUpdatedAt = DateTime.UtcNow;
                    await _unitOfWork.WalletRepo.UpdateAsync(driverWallet);

                    // D. Log Giao Dịch
                    await _unitOfWork.TransactionRepo.AddAsync(new Transaction
                    {
                        TransactionId = Guid.NewGuid(),
                        WalletId = driverWallet.WalletId,
                        TripId = trip.TripId,
                        Amount = -depositAmount, // Số âm thể hiện trừ tiền
                        Type = TransactionType.DEPOSIT, // Cần thêm enum này
                        Status = TransactionStatus.SUCCEEDED,
                        Description = $"Đặt cọc cho chuyến đi {trip.TripCode}",
                        CreatedAt = DateTime.UtcNow
                    });

                    depositStatus = DepositStatus.DEPOSITED;
                }
                // =========================================================================

                // 7. Xử lý địa điểm (Logic cũ)
                var tripStart = trip.ShippingRoute.StartLocation;
                var tripEnd = trip.ShippingRoute.EndLocation;
                Common.ValueObjects.Location finalStartLoc;
                Common.ValueObjects.Location finalEndLoc;

                if (isMainDriver)
                {
                    string pickupAddr = !string.IsNullOrWhiteSpace(postDetail.PickupLocation) ? postDetail.PickupLocation : tripStart.Address;
                    string dropAddr = !string.IsNullOrWhiteSpace(postDetail.DropoffLocation) ? postDetail.DropoffLocation : tripEnd.Address;
                    var geoStart = await _vietMapService.GeocodeAsync(pickupAddr);
                    var geoEnd = await _vietMapService.GeocodeAsync(dropAddr);
                    finalStartLoc = new Common.ValueObjects.Location(pickupAddr, geoStart?.Latitude ?? 0, geoStart?.Longitude ?? 0);
                    finalEndLoc = new Common.ValueObjects.Location(dropAddr, geoEnd?.Latitude ?? 0, geoEnd?.Longitude ?? 0);
                }
                else
                {
                    // Logic Tài phụ: Ưu tiên input của Driver -> Default theo Post -> Default theo Trip
                    string pAddr = !string.IsNullOrWhiteSpace(dto.StartLocation) ? dto.StartLocation : (postDetail.PickupLocation ?? tripStart.Address);
                    var geoS = await _vietMapService.GeocodeAsync(pAddr);
                    if (geoS == null) return new ResponseDTO("Địa chỉ đón không hợp lệ.", 400, false);
                    finalStartLoc = new Common.ValueObjects.Location(pAddr, geoS.Latitude ?? 0, geoS.Longitude ?? 0);

                    string dAddr = !string.IsNullOrWhiteSpace(dto.EndLocation) ? dto.EndLocation : (postDetail.DropoffLocation ?? tripEnd.Address);
                    var geoE = await _vietMapService.GeocodeAsync(dAddr);
                    if (geoE == null) return new ResponseDTO("Địa chỉ trả không hợp lệ.", 400, false);
                    finalEndLoc = new Common.ValueObjects.Location(dAddr, geoE.Latitude ?? 0, geoE.Longitude ?? 0);
                }

                // 8. Tạo Assignment
                var newAssignment = new TripDriverAssignment
                {
                    TripDriverAssignmentId = Guid.NewGuid(),
                    TripId = postTrip.TripId,
                    DriverId = driverId,
                    Type = postDetail.Type,
                    CreateAt = DateTime.UtcNow,
                    UpdateAt = DateTime.UtcNow,
                    BaseAmount = postDetail.PricePerPerson,

                    // [CẬP NHẬT] Thông tin cọc đã xử lý ở trên
                    DepositAmount = depositAmount,
                    DepositStatus = depositStatus,
                    DepositAt = (depositStatus == DepositStatus.DEPOSITED) ? DateTime.UtcNow : null,

                    StartLocation = finalStartLoc,
                    EndLocation = finalEndLoc,

                    // [NEW] KHỞI TẠO CÁC TRƯỜNG CHECK-IN / CHECK-OUT (Mặc định chưa làm gì cả)
                    IsOnBoard = false,
                    OnBoardTime = null,
                    OnBoardLocation = null,
                    OnBoardImage = null,
                    CheckInNote = null,

                    IsFinished = false,
                    OffBoardTime = null,
                    OffBoardLocation = null,
                    OffBoardImage = null,
                    CheckOutNote = null,

                    // Vì Driver chủ động apply và đã đóng cọc -> ACCEPTED luôn
                    AssignmentStatus = AssignmentStatus.ACCEPTED,
                    PaymentStatus = DriverPaymentStatus.UN_PAID
                };
                await _unitOfWork.TripDriverAssignmentRepo.AddAsync(newAssignment);

                // 9. Contract Logic (Internal Driver check)
                bool isInternalDriver = await _unitOfWork.OwnerDriverLinkRepo.CheckLinkExistsAsync(trip.OwnerId, driverId, FleetJoinStatus.APPROVED);
                if (!isInternalDriver)
                {
                    await _tripDriverContractService.CreateContractInternalAsync(trip.TripId, trip.OwnerId, driverId, postDetail.PricePerPerson);
                }

                // 10. Main Driver Logic
                if (isMainDriver)
                {
                    trip.UpdateAt = DateTime.UtcNow;
                    await _unitOfWork.TripRepo.UpdateAsync(trip);
                    await CreateRecordsForMainDriver(trip.TripId, driverId, trip.OwnerId);
                }

                // 11. Update Count & Status
                postDetail.RequiredCount -= 1;
                bool isAllSlotsFilled = postTrip.PostTripDetails.All(d => d.RequiredCount <= 0);
                if (isAllSlotsFilled) postTrip.Status = PostStatus.DONE;

                postTrip.UpdateAt = DateTime.UtcNow;
                await _unitOfWork.PostTripRepo.UpdateAsync(postTrip);

                // 12. Commit
                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync();

                return new ResponseDTO("Ứng tuyển thành công (Đã thanh toán cọc).", 201, true, new { assignmentId = newAssignment.TripDriverAssignmentId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO($"Error: {ex.Message}", 500, false);
            }
        }

        // =========================================================================================================
        // PRIVATE HELPER: TẠO BIÊN BẢN (RECORDS)
        // =========================================================================================================
        private async Task CreateRecordsForMainDriver(Guid tripId, Guid mainDriverId, Guid ownerId)
        {
            // 1. Lấy thông tin Contact để tạo Delivery Records (Hàng hóa)
            var contacts = await _unitOfWork.TripContactRepo.GetAll()
                                            .Where(c => c.TripId == tripId)
                                            .ToListAsync();

            var senderContact = contacts.FirstOrDefault(c => c.Type == ContactType.SENDER);
            var receiverContact = contacts.FirstOrDefault(c => c.Type == ContactType.RECEIVER);

            if (senderContact != null && receiverContact != null)
            {
                var pickupTemplate = await _templateService.GetLatestTemplateByTypeAsync(DeliveryRecordType.PICKUP);
                var dropoffTemplate = await _templateService.GetLatestTemplateByTypeAsync(DeliveryRecordType.DROPOFF);

                if (pickupTemplate != null)
                {
                    var pickupDto = new TripDeliveryRecordCreateDTO
                    {
                        TripId = tripId,
                        DeliveryRecordTempalteId = pickupTemplate.DeliveryRecordTemplateId,
                        StripContractId = senderContact.TripContactId,
                        Notes = "Biên bản nhận hàng (Auto-generated)",
                        type = DeliveryRecordType.PICKUP
                    };
                    await _tripDeliveryRecordService.CreateTripDeliveryRecordAsync(pickupDto, mainDriverId);
                }

                if (dropoffTemplate != null)
                {
                    var dropoffDto = new TripDeliveryRecordCreateDTO
                    {
                        TripId = tripId,
                        DeliveryRecordTempalteId = dropoffTemplate.DeliveryRecordTemplateId,
                        StripContractId = receiverContact.TripContactId,
                        Notes = "Biên bản giao hàng (Auto-generated)",
                        type = DeliveryRecordType.DROPOFF
                    };
                    await _tripDeliveryRecordService.CreateTripDeliveryRecordAsync(dropoffDto, mainDriverId);
                }
            }

            // 2. Tạo biên bản Giao nhận xe (Vehicle Handover Records)
            // LƯU Ý QUAN TRỌNG: 
            // TripVehicleHandoverRecordCreateDTO cần phải khớp với DTO bên Service Vehicle Handover
            // Nếu Service bên kia cần UserId từ Token, ta phải truyền thủ công vào hàm CreateTripVehicleHandoverRecordAsync (nếu hàm đó hỗ trợ nhận userId làm tham số).
            // Ở đây giả định hàm CreateTripVehicleHandoverRecordAsync nhận DTO chứa sẵn ID người Giao/Nhận.

            // A. GIAO XE (PICKUP): Chủ xe giao -> Tài xế nhận
            var vehiclePickupDto = new TripVehicleHandoverRecordCreateDTO
            {
                TripId = tripId,
                Type = DeliveryRecordType.HANDOVER, // Handover = Giao xe đi
                HandoverUserId = ownerId,           // Chủ xe
                ReceiverUserId = mainDriverId,      // Tài xế
                Notes = "Biên bản giao xe cho tài xế (Auto-generated)"
            };
            await _vehicleHandoverService.CreateTripVehicleHandoverRecordAsync(vehiclePickupDto);

            // B. TRẢ XE (RETURN): Tài xế trả -> Chủ xe nhận
            var vehicleReturnDto = new TripVehicleHandoverRecordCreateDTO
            {
                TripId = tripId,
                Type = DeliveryRecordType.RETURN,   // Return = Trả xe về
                HandoverUserId = mainDriverId,      // Tài xế
                ReceiverUserId = ownerId,           // Chủ xe
                Notes = "Biên bản trả xe về bãi (Auto-generated)"
            };
            await _vehicleHandoverService.CreateTripVehicleHandoverRecordAsync(vehicleReturnDto);
        }

        // =========================================================================
        // 1. TÀI XẾ CHECK-IN (LÊN XE)
        // =========================================================================
        public async Task<ResponseDTO> DriverCheckInAsync(DriverCheckInDTO dto)
        {
            try
            {
                var driverId = _userUtility.GetUserIdFromToken();

                // A. Lấy thông tin phân công
                var assignment = await _unitOfWork.TripDriverAssignmentRepo.GetAll()
                    .Include(a => a.Trip)
                    .FirstOrDefaultAsync(a => a.TripId == dto.TripId && a.DriverId == driverId);

                if (assignment == null) return new ResponseDTO("Bạn không có trong chuyến này.", 404, false);

                // B. Validate Trạng thái
                if (assignment.IsOnBoard) return new ResponseDTO("Bạn đã Check-in rồi.", 400, false);

                if (assignment.AssignmentStatus != AssignmentStatus.ACCEPTED)
                    return new ResponseDTO("Bạn chưa được chấp nhận vào chuyến này.", 400, false);

                // C. XỬ LÝ KHOẢNG CÁCH (SWITCH CASE LOGIC)
                string distanceNote = "";
                double? finalDistance = null;

                if (assignment.StartLocation != null
                    && assignment.StartLocation.Latitude.HasValue
                    && assignment.StartLocation.Longitude.HasValue)
                {
                    // Fix lỗi double?: Dùng .Value để lấy giá trị thực
                    double distKm = CalculateDistance(
                        assignment.StartLocation.Latitude.Value,
                        assignment.StartLocation.Longitude.Value,
                        dto.Latitude,
                        dto.Longitude
                    );

                    finalDistance = distKm;

                    // [NEW] SWITCH CASE ĐỂ PHÂN LOẠI KHOẢNG CÁCH
                    distanceNote = distKm switch
                    {
                        <= 0.5 => "", // Dưới 500m -> Hợp lệ, không ghi note
                        <= 3.0 => $" | ⚠️ Cảnh báo: Lệch {distKm:N2}km", // 0.5 - 3km -> Cảnh báo nhẹ
                        _ => $" | ⛔ CẢNH BÁO ĐỎ: Check-in quá xa ({distKm:N2}km)" // > 3km -> Cảnh báo nặng
                    };
                }

                // D. Upload Ảnh (Fix lỗi tham số driverId và FileType)
                string imageUrl = await _fileService.UploadFileAsync(dto.EvidenceImage, driverId, FirebaseFileType.CHECKIN_CHECKOUT_IMAGES);
                if (string.IsNullOrEmpty(imageUrl)) return new ResponseDTO("Lỗi upload ảnh minh chứng.", 500, false);

                // E. Cập nhật DB
                assignment.IsOnBoard = true;
                assignment.OnBoardTime = DateTime.UtcNow;

                // Lưu vị trí kèm Note cảnh báo
                assignment.OnBoardLocation = $"{dto.Latitude},{dto.Longitude}|{dto.CurrentAddress}";
                assignment.OnBoardImage = imageUrl;

                assignment.CheckInNote = distanceNote;

                // Nếu Entity có trường lưu khoảng cách số (như đã bàn trước đó), gán vào luôn
                // assignment.CheckInDistanceDiff = finalDistance; 

                await _unitOfWork.TripDriverAssignmentRepo.UpdateAsync(assignment);
                await _unitOfWork.SaveChangeAsync();

                //// F. Gửi thông báo
                //await _notificationService.SendAsync(assignment.Trip.OwnerId, "TÀI XẾ ĐÃ LÊN XE",
                //    $"Tài xế check-in tại {dto.CurrentAddress}.{distanceNote}");

                return new ResponseDTO("Check-in thành công!", 200, true, new { imageUrl, warning = distanceNote });
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi: {ex.Message}", 500, false);
            }
        }

        // =========================================================================
        // 2. TÀI XẾ CHECK-OUT (XUỐNG XE)
        // =========================================================================
        public async Task<ResponseDTO> DriverCheckOutAsync(DriverCheckOutDTO dto)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var driverId = _userUtility.GetUserIdFromToken();

                var assignment = await _unitOfWork.TripDriverAssignmentRepo.GetAll()
                    .Include(a => a.Trip)
                    .FirstOrDefaultAsync(a => a.TripId == dto.TripId && a.DriverId == driverId);

                if (assignment == null) return new ResponseDTO("Lỗi phân công.", 404, false);

                if (!assignment.IsOnBoard) return new ResponseDTO("Bạn chưa Check-in nên không thể Check-out.", 400, false);
                if (assignment.IsFinished) return new ResponseDTO("Bạn đã Check-out rồi.", 400, false);

                // C. XỬ LÝ KHOẢNG CÁCH (SWITCH CASE LOGIC)
                string distanceNote = "";

                if (assignment.EndLocation != null
                    && assignment.EndLocation.Latitude.HasValue
                    && assignment.EndLocation.Longitude.HasValue)
                {
                    double distKm = CalculateDistance(
                        assignment.EndLocation.Latitude.Value,
                        assignment.EndLocation.Longitude.Value,
                        dto.Latitude,
                        dto.Longitude
                    );

                    // [NEW] SWITCH CASE CHO CHECK-OUT
                    distanceNote = distKm switch
                    {
                        <= 1.0 => "", // Check-out cho phép sai số rộng hơn (1km)
                        <= 5.0 => $" | ⚠️ Cảnh báo: Lệch {distKm:N2}km",
                        _ => $" | ⛔ CẢNH BÁO ĐỎ: Check-out quá xa ({distKm:N2}km)"
                    };
                }

                // D. Upload Ảnh
                string imageUrl = await _fileService.UploadFileAsync(dto.EvidenceImage, driverId, FirebaseFileType.CHECKIN_CHECKOUT_IMAGES);
                if (string.IsNullOrEmpty(imageUrl)) return new ResponseDTO("Lỗi upload ảnh minh chứng.", 500, false);

                // E. Cập nhật DB
                assignment.IsFinished = true;
                assignment.OffBoardTime = DateTime.UtcNow;
                assignment.OffBoardLocation = $"{dto.Latitude},{dto.Longitude}|{dto.CurrentAddress}";
                assignment.OffBoardImage = imageUrl;

                assignment.CheckOutNote = distanceNote;

                // Đánh dấu hoàn thành
                assignment.AssignmentStatus = AssignmentStatus.COMPLETED;

                await _unitOfWork.TripDriverAssignmentRepo.UpdateAsync(assignment);
                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync();

                return new ResponseDTO("Check-out thành công. Cảm ơn bạn!", 200, true, new { warning = distanceNote });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO($"Lỗi: {ex.Message}", 500, false);
            }
        }

        // --- HELPER: TÍNH KHOẢNG CÁCH ---
        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371;
            var dLat = Deg2Rad(lat2 - lat1);
            var dLon = Deg2Rad(lon2 - lon1);
            var a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(Deg2Rad(lat1)) * Math.Cos(Deg2Rad(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }
        private double Deg2Rad(double deg) => deg * (Math.PI / 180);
    }
}
