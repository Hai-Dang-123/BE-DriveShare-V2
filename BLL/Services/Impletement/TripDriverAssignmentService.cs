using BLL.Services.Implement;
using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.Enums.Type;
using Common.Helpers;
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
        private readonly IUserDocumentService _userDocumentService;
        private readonly ITransactionService _transactionService;
        private readonly IEmailService _emailService;

        public TripDriverAssignmentService(
            IUnitOfWork unitOfWork,
            UserUtility userUtility,
            ITripDriverContractService tripDriverContractService,
            ITripDeliveryRecordService tripDeliveryRecordService,
            IDeliveryRecordTemplateService templateService,
            IVietMapService vietMapService,
            ITripVehicleHandoverRecordService vehicleHandoverService,
            IFirebaseUploadService fileService,
            IDriverWorkSessionService driverWorkSessionService,
            IUserDocumentService userDocumentService,
            ITransactionService transactionService,
            IEmailService emailService)
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
            _userDocumentService = userDocumentService;
            _transactionService = transactionService;
            _emailService = emailService;
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

                // --- [MỚI] CHECK 3.1: STATUS ACTIVE ---
                if (driver.Status != UserStatus.ACTIVE)
                {
                    return new ResponseDTO("Tài khoản tài xế đang bị khóa hoặc chưa kích hoạt (Status != ACTIVE).", 403, false);
                }

                // --- [MỚI] CHECK 3.2: LỊCH SỬ LÁI XE ---
                if (!driver.HasDeclaredInitialHistory)
                {
                    return new ResponseDTO(
                        "Tài xế cần cập nhật lịch sử lái xe trong tuần hiện tại trước khi nhận chuyến đi đầu tiên.",
                        428,
                        false
                    );
                }

                // --- [MỚI] CHECK 3.3: GIẤY TỜ (CCCD, GPLX, GKSK) ---
                // Lấy danh sách loại giấy tờ ĐÃ ĐƯỢC DUYỆT (Active) của tài xế này
                var verifiedDocTypes = await _unitOfWork.UserDocumentRepo.GetAll()
                    .Where(d => d.UserId == dto.DriverId && d.Status == VerifileStatus.ACTIVE)
                    .Select(d => d.DocumentType)
                    .ToListAsync();

                bool hasCCCD = verifiedDocTypes.Contains(DocumentType.CCCD);
                bool hasGPLX = verifiedDocTypes.Contains(DocumentType.DRIVER_LINCENSE);
                bool hasGKSK = verifiedDocTypes.Contains(DocumentType.HEALTH_CHECK); // Giấy khám sức khỏe

                if (!hasCCCD || !hasGPLX || !hasGKSK)
                {
                    return new ResponseDTO($"Tài xế chưa xác thực đủ giấy tờ bắt buộc. (CCCD: {hasCCCD}, GPLX: {hasGPLX}, GKSK: {hasGKSK})", 403, false);
                }
                // -------------------------------------------------------------

                if (await _transactionService.IsUserRestrictedDueToDebtAsync(driver.UserId))
                {
                    return new ResponseDTO("Tài khoản đang bị hạn chế do dư nợ hoặc không đủ số dư tối thiểu.", 403, false);
                }

                // --- CHECK VALIDATE GIỜ LÁI (SOLO/TEAM) ---
                var hoursCheck = await ValidateDriverHoursForTripAsync(dto.DriverId, dto.TripId, null);
                if (!hoursCheck.IsValid)
                {
                    return new ResponseDTO(hoursCheck.ErrorMsg, 400, false);
                }

                // --- CHECK 2: QUY ĐỊNH 10H/48H ---
                var availability = await _driverWorkSessionService.CheckDriverAvailabilityAsync(dto.DriverId);
                if (!availability.CanDrive)
                {
                    return new ResponseDTO($"Không thể gán: Tài xế đã hết giờ lái. {availability.Message}", 400, false);
                }

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

                // 7. Tạo Assignment
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

                    AssignmentStatus = AssignmentStatus.ACCEPTED,
                    PaymentStatus = DriverPaymentStatus.UN_PAID,

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

                    DepositAmount = 0,
                    DepositStatus = DepositStatus.NOT_REQUIRED
                };

                await _unitOfWork.TripDriverAssignmentRepo.AddAsync(newAssignment);

                // -------------------------------------------------------------------------
                // [LOGIC MỚI BỔ SUNG]
                // Kiểm tra Nội bộ & Cập nhật trạng thái Trip nếu là Main Driver
                // -------------------------------------------------------------------------
                bool isInternalDriver = await _unitOfWork.OwnerDriverLinkRepo.CheckLinkExistsAsync(trip.OwnerId, dto.DriverId, FleetJoinStatus.APPROVED);

                // Nếu KHÔNG PHẢI nội bộ -> Vẫn phải tạo hợp đồng (nếu Owner gán tài xế ngoài)
                if (!isInternalDriver)
                {
                    // Logic tạo hợp đồng cho tài xế ngoài (nếu business cho phép Owner gán tài xế ngoài)
                    await _tripDriverContractService.CreateContractInternalAsync(trip.TripId, trip.OwnerId, dto.DriverId, dto.BaseAmount);
                }

                // Nếu LÀ TÀI XẾ CHÍNH (Primary)
                if (isMainDriver)
                {
                    // 1. Tạo bản ghi main driver record (Logic cũ của bạn)
                    trip.UpdateAt = DateTime.UtcNow;
                    await CreateRecordsForMainDriver(trip.TripId, dto.DriverId, trip.OwnerId);

                    // 2. [UPDATE] Nếu là Nội bộ -> Chốt chuyến luôn (Done Assign)
                    if (isInternalDriver)
                    {
                        trip.Status = TripStatus.DONE_ASSIGNING_DRIVER; // Cập nhật trạng thái đã có tài xế
                    }

                    // Lưu update trip
                    await _unitOfWork.TripRepo.UpdateAsync(trip);
                }
                // -------------------------------------------------------------------------

                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync();

                return new ResponseDTO("Đã gán tài xế thành công.", 201, true, new { assignmentId = newAssignment.TripDriverAssignmentId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO($"Error assigning driver: {ex.Message}", 500, false);
            }
        }

        // =========================================================================================================
        // 2. TÀI XẾ ỨNG TUYỂN (ASSIGNMENT BY POST TRIP)
        // =========================================================================================================
        public async Task<ResponseDTO> CreateAssignmentByPostTripAsync(CreateAssignmentByPostTripDTO dto)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                // =========================================================================
                // 1. VALIDATE DRIVER & QUOTA
                // =========================================================================
                var driverId = _userUtility.GetUserIdFromToken();
                if (driverId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                var driver = await _unitOfWork.DriverRepo.GetByIdAsync(driverId);
                if (driver == null) return new ResponseDTO("Driver not found.", 404, false);

                // --- [MỚI] CHECK 1.1: STATUS ACTIVE ---
                if (driver.Status != UserStatus.ACTIVE)
                {
                    return new ResponseDTO("Tài khoản của bạn đang bị khóa hoặc chưa kích hoạt.", 403, false);
                }

                // --- [MỚI] CHECK 1.2: LỊCH SỬ LÁI XE ---
                if (!driver.HasDeclaredInitialHistory)
                {
                    return new ResponseDTO("Bạn cần cập nhật lịch sử lái xe trong tuần hiện tại trước khi nhận chuyến đi đầu tiên.", 428, false);
                }

                // --- [MỚI] CHECK 1.3: GIẤY TỜ (CCCD, GPLX, GKSK) ---
                var verifiedDocTypes = await _unitOfWork.UserDocumentRepo.GetAll()
                    .Where(d => d.UserId == driverId && d.Status == VerifileStatus.ACTIVE)
                    .Select(d => d.DocumentType)
                    .ToListAsync();

                bool hasCCCD = verifiedDocTypes.Contains(DocumentType.CCCD);
                bool hasGPLX = verifiedDocTypes.Contains(DocumentType.DRIVER_LINCENSE);
                bool hasGKSK = verifiedDocTypes.Contains(DocumentType.HEALTH_CHECK);

                if (!hasCCCD || !hasGPLX || !hasGKSK)
                {
                    return new ResponseDTO($"Bạn chưa xác thực đủ giấy tờ. (CCCD: {hasCCCD}, GPLX: {hasGPLX}, GKSK: {hasGKSK})", 403, false);
                }
                // -------------------------------------------------------------

                if (await _transactionService.IsUserRestrictedDueToDebtAsync(driver.UserId))
                {
                    return new ResponseDTO("Tài khoản đang bị hạn chế do dư nợ hoặc không đủ số dư tối thiểu.", 403, false);
                }

                var availability = await _driverWorkSessionService.CheckDriverAvailabilityAsync(driverId);
                if (!availability.CanDrive)
                {
                    return new ResponseDTO($"Bạn không thể nhận chuyến do đã quá giờ lái quy định. {availability.Message}", 400, false);
                }

                // ------------------------------------------------

                // =========================================================================
                // 2. GET & VALIDATE TRIP/POST DATA
                // =========================================================================
                var postTrip = await _unitOfWork.PostTripRepo.GetAll()
                    .Include(p => p.PostTripDetails)
                    .FirstOrDefaultAsync(p => p.PostTripId == dto.PostTripId);

                if (postTrip == null) return new ResponseDTO("PostTrip not found.", 404, false);
                if (postTrip.Status != PostStatus.OPEN) return new ResponseDTO("Bài đăng này đã đóng.", 400, false);

                var postDetail = postTrip.PostTripDetails.FirstOrDefault(d => d.PostTripDetailId == dto.PostTripDetailId);
                if (postDetail == null) return new ResponseDTO("Slot details not found.", 404, false);
                if (postDetail.RequiredCount <= 0) return new ResponseDTO("Vị trí này đã đủ người.", 400, false);

                // --- [MỚI] CHECK VALIDATE GIỜ LÁI (SOLO/TEAM) ---
                var hoursCheck = await ValidateDriverHoursForTripAsync(driverId, postTrip.TripId, dto.PostTripId); // Có PostTripId
                if (!hoursCheck.IsValid)
                {
                    return new ResponseDTO(hoursCheck.ErrorMsg, 400, false);
                }

                var trip = await _unitOfWork.TripRepo.GetAll()
                    .Include(t => t.TripRoute)
                    .Include(t => t.ShippingRoute).ThenInclude(r => r.StartLocation)
                    .Include(t => t.ShippingRoute).ThenInclude(r => r.EndLocation)
                    .FirstOrDefaultAsync(t => t.TripId == postTrip.TripId);

                if (trip == null) return new ResponseDTO("Trip not found.", 404, false);

                // Check Duplicate
                bool alreadyApplied = await _unitOfWork.TripDriverAssignmentRepo.AnyAsync(
                    a => a.TripId == postTrip.TripId && a.DriverId == driverId
                );
                if (alreadyApplied) return new ResponseDTO("Bạn đã ứng tuyển chuyến này rồi.", 409, false);

                // Check Main Driver
                bool isMainDriver = (postDetail.Type == DriverType.PRIMARY);
                if (isMainDriver)
                {
                    bool mainDriverExists = await _unitOfWork.TripDriverAssignmentRepo.AnyAsync(
                        a => a.TripId == trip.TripId && a.Type == DriverType.PRIMARY && a.AssignmentStatus == AssignmentStatus.ACCEPTED
                    );
                    if (mainDriverExists) return new ResponseDTO("Chuyến này đã có tài xế chính.", 400, false);
                }

                // =========================================================================
                // 3. XỬ LÝ TIỀN CỌC
                // =========================================================================
                decimal depositAmount = postDetail.DepositAmount;
                DepositStatus depositStatus = DepositStatus.NOT_REQUIRED;

                if (depositAmount > 0)
                {
                    var driverWallet = await _unitOfWork.WalletRepo.FirstOrDefaultAsync(w => w.UserId == driverId);
                    if (driverWallet == null) return new ResponseDTO("Ví tài xế không tồn tại.", 400, false);

                    if (driverWallet.Balance < depositAmount)
                    {
                        return new ResponseDTO($"Số dư ví không đủ để đặt cọc ({depositAmount:N0}đ). Vui lòng nạp thêm tiền.", 402, false);
                    }

                    driverWallet.Balance -= depositAmount;
                    driverWallet.LastUpdatedAt = DateTime.UtcNow;
                    await _unitOfWork.WalletRepo.UpdateAsync(driverWallet);

                    await _unitOfWork.TransactionRepo.AddAsync(new Transaction
                    {
                        TransactionId = Guid.NewGuid(),
                        WalletId = driverWallet.WalletId,
                        TripId = trip.TripId,
                        Amount = -depositAmount,
                        Type = TransactionType.DEPOSIT,
                        Status = TransactionStatus.SUCCEEDED,
                        Description = $"Đặt cọc cho chuyến đi {trip.TripCode}",
                        CreatedAt = DateTime.UtcNow,
                        BalanceBefore = driverWallet.Balance + depositAmount,
                        BalanceAfter = driverWallet.Balance,
                        CompletedAt = DateTime.UtcNow
                    });

                    depositStatus = DepositStatus.DEPOSITED;
                }

                // =========================================================================
                // [UPDATED] 4. XỬ LÝ ĐỊA ĐIỂM & VALIDATE KHOẢNG CÁCH
                // =========================================================================
                var tripStart = trip.ShippingRoute.StartLocation;
                var tripEnd = trip.ShippingRoute.EndLocation;

                Common.ValueObjects.Location finalStartLoc;
                Common.ValueObjects.Location finalEndLoc;

                if (isMainDriver)
                {
                    // --- CASE: TÀI XẾ CHÍNH (Giữ nguyên logic cũ) ---
                    string pickupAddr = !string.IsNullOrWhiteSpace(postDetail.PickupLocation) ? postDetail.PickupLocation : tripStart.Address;
                    string dropAddr = !string.IsNullOrWhiteSpace(postDetail.DropoffLocation) ? postDetail.DropoffLocation : tripEnd.Address;

                    var geoStart = await _vietMapService.GeocodeAsync(pickupAddr);
                    var geoEnd = await _vietMapService.GeocodeAsync(dropAddr);

                    finalStartLoc = new Common.ValueObjects.Location(pickupAddr, geoStart?.Latitude ?? 0, geoStart?.Longitude ?? 0);
                    finalEndLoc = new Common.ValueObjects.Location(dropAddr, geoEnd?.Latitude ?? 0, geoEnd?.Longitude ?? 0);
                }
                else
                {
                    // --- CASE: TÀI XẾ PHỤ (Logic mới) ---
                    bool needValidateStart = false;
                    bool needValidateEnd = false;

                    // A. Xử lý ĐIỂM ĐÓN (Start)
                    if (!string.IsNullOrWhiteSpace(dto.StartLocation))
                    {
                        // Có nhập -> Lấy theo User nhập -> Đánh dấu cần Validate
                        var geoS = await _vietMapService.GeocodeAsync(dto.StartLocation);
                        if (geoS == null || geoS.Latitude == 0) return new ResponseDTO("Địa chỉ đón nhập vào không hợp lệ.", 400, false);

                        finalStartLoc = new Common.ValueObjects.Location(dto.StartLocation, geoS.Latitude ?? 0, geoS.Longitude ?? 0);
                        needValidateStart = true;
                    }
                    else
                    {
                        // Không nhập (Null/Empty) -> Lấy theo Post Detail hoặc Trip -> KHÔNG validate
                        string defStart = !string.IsNullOrWhiteSpace(postDetail.PickupLocation) ? postDetail.PickupLocation : tripStart.Address;
                        var geoS = await _vietMapService.GeocodeAsync(defStart); // Vẫn cần geocode để lưu tọa độ

                        finalStartLoc = new Common.ValueObjects.Location(defStart, geoS?.Latitude ?? 0, geoS?.Longitude ?? 0);
                        needValidateStart = false;
                    }

                    // B. Xử lý ĐIỂM TRẢ (End)
                    if (!string.IsNullOrWhiteSpace(dto.EndLocation))
                    {
                        // Có nhập -> Lấy theo User nhập -> Đánh dấu cần Validate
                        var geoE = await _vietMapService.GeocodeAsync(dto.EndLocation);
                        if (geoE == null || geoE.Latitude == 0) return new ResponseDTO("Địa chỉ trả nhập vào không hợp lệ.", 400, false);

                        finalEndLoc = new Common.ValueObjects.Location(dto.EndLocation, geoE.Latitude ?? 0, geoE.Longitude ?? 0);
                        needValidateEnd = true;
                    }
                    else
                    {
                        // Không nhập -> Lấy theo Post Detail hoặc Trip -> KHÔNG validate
                        string defEnd = !string.IsNullOrWhiteSpace(postDetail.DropoffLocation) ? postDetail.DropoffLocation : tripEnd.Address;
                        var geoE = await _vietMapService.GeocodeAsync(defEnd);

                        finalEndLoc = new Common.ValueObjects.Location(defEnd, geoE?.Latitude ?? 0, geoE?.Longitude ?? 0);
                        needValidateEnd = false;
                    }

                    // C. Thực hiện Validate (Nếu cần)
                    if (needValidateStart || needValidateEnd)
                    {
                        // Chỉ lấy Polyline khi thực sự cần check để tối ưu hiệu năng
                        string encodedPolyline = trip.TripRoute.RouteData;
                        if (string.IsNullOrEmpty(encodedPolyline))
                        {
                            var routePath = await _vietMapService.GetRouteAsync(tripStart, tripEnd, "truck");
                            if (routePath != null)
                            {
                                encodedPolyline = routePath.Points;
                                // Cache lại vào DB nếu muốn
                                // trip.TripRoute.RouteData = encodedPolyline; 
                            }
                        }

                        if (string.IsNullOrEmpty(encodedPolyline))
                        {
                            // Nếu không lấy được lộ trình mà bắt buộc phải validate -> Lỗi
                            return new ResponseDTO("Hệ thống không xác định được lộ trình để kiểm tra vị trí đón/trả.", 500, false);
                        }

                        // Check Start
                        if (needValidateStart)
                        {
                            bool isPickupValid = _vietMapService.IsLocationOnRoute(finalStartLoc, encodedPolyline, 5.0); // Buffer 5km
                            if (!isPickupValid) return new ResponseDTO($"Điểm đón '{finalStartLoc.Address}' nằm quá xa lộ trình (>5km).", 400, false);
                        }

                        // Check End
                        if (needValidateEnd)
                        {
                            bool isDropoffValid = _vietMapService.IsLocationOnRoute(finalEndLoc, encodedPolyline, 5.0);
                            if (!isDropoffValid) return new ResponseDTO($"Điểm trả '{finalEndLoc.Address}' nằm quá xa lộ trình (>5km).", 400, false);
                        }
                    }
                }

                // =========================================================================
                // 5. TẠO ASSIGNMENT & KẾT THÚC
                // =========================================================================
                var newAssignment = new TripDriverAssignment
                {
                    TripDriverAssignmentId = Guid.NewGuid(),
                    TripId = postTrip.TripId,
                    DriverId = driverId,
                    Type = postDetail.Type,
                    CreateAt = DateTime.UtcNow,
                    UpdateAt = DateTime.UtcNow,
                    BaseAmount = postDetail.PricePerPerson,
                    BonusAmount = postDetail.BonusAmount,

                    DepositAmount = depositAmount,
                    DepositStatus = depositStatus,
                    DepositAt = (depositStatus == DepositStatus.DEPOSITED) ? DateTime.UtcNow : null,

                    StartLocation = finalStartLoc,
                    EndLocation = finalEndLoc,

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

                    AssignmentStatus = AssignmentStatus.ACCEPTED,
                    PaymentStatus = DriverPaymentStatus.UN_PAID
                };
                await _unitOfWork.TripDriverAssignmentRepo.AddAsync(newAssignment);

                // Contract Logic
                bool isInternalDriver = await _unitOfWork.OwnerDriverLinkRepo.CheckLinkExistsAsync(trip.OwnerId, driverId, FleetJoinStatus.APPROVED);
                if (!isInternalDriver)
                {
                    await _tripDriverContractService.CreateContractInternalAsync(trip.TripId, trip.OwnerId, driverId, postDetail.PricePerPerson);
                }

                // Main Driver Records Logic
                if (isMainDriver)
                {
                    trip.UpdateAt = DateTime.UtcNow;
                    await _unitOfWork.TripRepo.UpdateAsync(trip);
                    await CreateRecordsForMainDriver(trip.TripId, driverId, trip.OwnerId);
                }

                // Update Slot Counts
                postDetail.RequiredCount -= 1;
                bool isAllSlotsFilled = postTrip.PostTripDetails.All(d => d.RequiredCount <= 0);

                if (isAllSlotsFilled)
                {
                    postTrip.Status = PostStatus.DONE;

                    // --- [LOGIC MỚI] CHECK NỘI BỘ + ĐỦ NGƯỜI THÌ DONE ASSIGN ---
                    if (isInternalDriver)
                    {
                        // Nếu người ứng tuyển cuối cùng là Nội bộ -> Chốt chuyến luôn, không cần ký hợp đồng
                        // (Giả định TripStatus.ASSIGNED là trạng thái đã chốt tài xế)
                        trip.Status = TripStatus.DONE_ASSIGNING_DRIVER;
                        trip.UpdateAt = DateTime.UtcNow;
                        await _unitOfWork.TripRepo.UpdateAsync(trip);
                    }
                    // ------------------------------------------------------------
                }

                postTrip.UpdateAt = DateTime.UtcNow;
                await _unitOfWork.PostTripRepo.UpdateAsync(postTrip);


                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync();

                return new ResponseDTO("Ứng tuyển thành công.", 201, true, new { assignmentId = newAssignment.TripDriverAssignmentId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO($"System Error: {ex.Message}", 500, false);
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

        private async Task<(bool IsValid, string ErrorMsg)> ValidateDriverHoursForTripAsync(
    Guid driverId,
    Guid tripId,
    Guid? postTripId = null)
        {
            try
            {
                // =================================================================
                // BƯỚC 1: LẤY DỮ LIỆU TRIP, ROUTE VÀ XE
                // =================================================================
                var trip = await _unitOfWork.TripRepo.GetAll()
                    .Include(t => t.ShippingRoute)
                    .Include(t => t.Vehicle) // <--- QUAN TRỌNG: Include Vehicle để lấy Payload
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.TripId == tripId);

                if (trip == null) return (false, "Trip not found.");

                // =================================================================
                // BƯỚC 2: VALIDATE BẰNG LÁI & TRỌNG TẢI (MỚI)
                // =================================================================
                var driverInfo = await _unitOfWork.DriverRepo.GetByIdAsync(driverId);
                if (driverInfo == null) return (false, "Driver not found.");

                // Nếu Trip đã có xe gán vào (thường là có vì Trip tạo bởi Owner)
                if (trip.Vehicle != null)
                {
                    var licenseCheck = ValidateDriverLicenseForVehicle(driverInfo.LicenseClass, trip.Vehicle.PayloadInKg);
                    if (!licenseCheck.IsValid)
                    {
                        return (false, licenseCheck.ErrorMsg);
                    }
                }
                else
                {
                    // Fallback: Nếu Trip chưa có xe, check theo yêu cầu của PostTrip (nếu có)
                    if (postTripId.HasValue)
                    {
                        var postTripObj = await _unitOfWork.PostTripRepo.GetByIdAsync(postTripId.Value);
                        if (postTripObj != null && postTripObj.RequiredPayloadInKg.HasValue)
                        {
                            var licenseCheck = ValidateDriverLicenseForVehicle(driverInfo.LicenseClass, postTripObj.RequiredPayloadInKg.Value);
                            if (!licenseCheck.IsValid) return (false, licenseCheck.ErrorMsg);
                        }
                    }
                }

                // =================================================================
                // BƯỚC 3: XÁC ĐỊNH CHẾ ĐỘ CHẠY (SOLO / TEAM)
                // =================================================================
                bool isTeamMode = false;

                if (postTripId.HasValue)
                {
                    var postTrip = await _unitOfWork.PostTripRepo.GetAll()
                        .Include(p => p.PostTripDetails)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.PostTripId == postTripId);

                    if (postTrip != null)
                    {
                        // Check nếu có role Phụ hoặc tổng slot > 1
                        bool hasAssistant = postTrip.PostTripDetails.Any(d => d.Type == DriverType.SECONDARY);
                        int totalSlotsRequired = postTrip.PostTripDetails.Sum(d => d.RequiredCount);

                        // Logic: Có nhiều hơn 1 người -> Team Mode
                        if (hasAssistant || totalSlotsRequired > 1)
                        {
                            isTeamMode = true;
                        }
                    }
                }
                else
                {
                    // Logic cho trường hợp Owner tự gán (không qua bài đăng)
                    int currentDrivers = await _unitOfWork.TripDriverAssignmentRepo.GetAll()
                        .Where(a => a.TripId == tripId && a.AssignmentStatus == AssignmentStatus.ACCEPTED)
                        .CountAsync(); // Sử dụng CountAsync của EF Core

                    if (currentDrivers > 0) isTeamMode = true;

                    // Hoặc nếu lộ trình quá dài (> 500km) -> Mặc định Team
                    if (trip.ShippingRoute.EstimatedDistanceKm > 500) isTeamMode = true;
                }

                // =================================================================
                // BƯỚC 4: TÍNH TOÁN GIỜ LÁI CẦN THIẾT (HELPER)
                // =================================================================
                double distance = trip.ShippingRoute.EstimatedDistanceKm;
                double duration = trip.ShippingRoute.EstimatedDurationHours;

                // Fallback data cũ
                if (duration <= 0 && distance > 0) duration = distance / 50.0;

                // Lấy WaitTime & Buffer (dùng ?? 0 cho an toàn)
                double waitTime = trip.ShippingRoute.WaitTimeHours ?? 0;
                double bufferTime = duration * 0.15; // Buffer 15%

                // Gọi Helper (6 tham số)
                var analysis = TripCalculationHelper.CalculateScenarios(
                    distance,
                    duration,
                    waitTime,
                    bufferTime,
                    trip.ShippingRoute.ExpectedPickupDate,
                    trip.ShippingRoute.ExpectedDeliveryDate
                );

                // Lấy giờ yêu cầu dựa trên Mode
                double requiredHours;
                if (isTeamMode)
                {
                    requiredHours = analysis.TeamScenario.DrivingHoursPerDriver;
                }
                else
                {
                    requiredHours = analysis.SoloScenario.DrivingHoursPerDriver;
                }

                // =================================================================
                // BƯỚC 5: CHECK QUOTA TÀI XẾ
                // =================================================================
                var availability = await _driverWorkSessionService.CheckDriverAvailabilityAsync(driverId);

                // 5.1. Check tài khoản có bị khóa/hết sạch giờ không
                if (!availability.CanDrive)
                    return (false, $"Tài xế không khả dụng: {availability.Message}");

                // 5.2. Check đủ giờ cho chuyến này không
                // (Đảm bảo DTO DriverAvailabilityDTO đã có field RemainingHoursThisWeek)
                if (availability.RemainingHoursThisWeek < requiredHours)
                {
                    string modeStr = isTeamMode ? "Team" : "Solo";
                    return (false, $"Tài xế không đủ giờ lái. Cần: {requiredHours:F1}h ({modeStr}), Quỹ còn: {availability.RemainingHoursThisWeek:F1}h.");
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi hệ thống khi validate: {ex.Message}");
            }
        }

        // Helper kiểm tra hạng bằng lái so với trọng tải xe
        private (bool IsValid, string ErrorMsg) ValidateDriverLicenseForVehicle(string licenseClass, decimal vehiclePayloadKg)
        {
            if (string.IsNullOrWhiteSpace(licenseClass))
                return (false, "Tài xế chưa cập nhật thông tin hạng bằng lái.");

            // Quy đổi sang tấn
            decimal payloadTon = vehiclePayloadKg / 1000m;
            string currentClass = licenseClass.Trim().ToUpper();

            // --- LOGIC LUẬT GIAO THÔNG (VÍ DỤ) ---

            // 1. Bằng B2: Chỉ lái xe tải < 3.5 tấn
            if (currentClass == "B2")
            {
                if (payloadTon >= 3.5m)
                    return (false, $"Bằng B2 chỉ được chạy xe dưới 3.5 tấn. Xe này tải trọng {payloadTon:N1} tấn.");
                return (true, null);
            }

            // 2. Bằng C: Lái được xe tải >= 3.5 tấn (và cả nhỏ hơn)
            if (currentClass == "C")
            {
                return (true, null); // C cân hết xe tải thường
            }

            // 3. Các bằng cao hơn (D, E, F, FC...): Thường mặc định bao gồm quyền của C
            if (currentClass.StartsWith("F") || currentClass == "D" || currentClass == "E" || currentClass == "FC")
            {
                return (true, null);
            }

            // 4. Các trường hợp khác (A1, B1...): Không được lái xe tải kinh doanh
            return (false, $"Hạng bằng '{currentClass}' không phù hợp để vận chuyển hàng hóa (yêu cầu tối thiểu B2 hoặc C).");
        }

        // =========================================================================================================
        // 3. XÓA TÀI XẾ KHỎI TRIP (CANCEL ASSIGNMENT) & HOÀN CỌC
        // =========================================================================================================
        public async Task<ResponseDTO> CancelAssignmentAsync(Guid assignmentId)
        {
            // Dùng Transaction để đảm bảo tiền về ví an toàn
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var currentUserId = _userUtility.GetUserIdFromToken();
                var userRole = _userUtility.GetUserRoleFromToken();

                // 1. Load Assignment (NHỚ INCLUDE DRIVER ĐỂ LẤY EMAIL)
                var assignment = await _unitOfWork.TripDriverAssignmentRepo.GetAll()
                    .Include(a => a.Trip)
                    .Include(a => a.Driver) // <--- QUAN TRỌNG: Thêm dòng này
                    .Include(a => a.Trip.PostTrip).ThenInclude(p => p.PostTripDetails)
                    .FirstOrDefaultAsync(a => a.TripDriverAssignmentId == assignmentId);

                if (assignment == null) return new ResponseDTO("Assignment not found.", 404, false);

                // 2. Validate Quyền (Chỉ Owner của Trip hoặc Admin mới được xóa)
                if (userRole != "Admin" && assignment.Trip.OwnerId != currentUserId)
                {
                    // Cho phép thêm trường hợp Driver tự hủy (nếu logic cho phép)
                    // if (assignment.DriverId != currentUserId) ...
                    return new ResponseDTO("Unauthorized: Bạn không có quyền xóa tài xế khỏi chuyến này.", 403, false);
                }

                // 3. Validate Trạng thái Trip & Assignment
                if (assignment.Trip.Status == TripStatus.COMPLETED || assignment.Trip.Status == TripStatus.CANCELLED)
                {
                    return new ResponseDTO("Không thể xóa tài xế khi chuyến đi đã kết thúc hoặc bị hủy.", 400, false);
                }

                // Nếu tài xế đã Check-in (đã lên xe), không cho xóa ngang xương
                if (assignment.IsOnBoard)
                {
                    return new ResponseDTO("Tài xế đang thực hiện chuyến đi (Đã Check-in), không thể gỡ bỏ.", 400, false);
                }

                // 4. XỬ LÝ HOÀN CỌC (REFUND)
                if (assignment.DepositStatus == DepositStatus.DEPOSITED && assignment.DepositAmount > 0)
                {
                    var driverWallet = await _unitOfWork.WalletRepo.FirstOrDefaultAsync(w => w.UserId == assignment.DriverId);
                    if (driverWallet != null)
                    {
                        // Cộng tiền lại cho Driver
                        decimal refundAmount = assignment.DepositAmount;
                        driverWallet.Balance += refundAmount;
                        driverWallet.LastUpdatedAt = DateTime.UtcNow;

                        await _unitOfWork.WalletRepo.UpdateAsync(driverWallet);

                        // Tạo Transaction Refund
                        await _unitOfWork.TransactionRepo.AddAsync(new Transaction
                        {
                            TransactionId = Guid.NewGuid(),
                            WalletId = driverWallet.WalletId,
                            TripId = assignment.TripId,
                            Amount = refundAmount, // Số dương (Cộng tiền)
                            Type = TransactionType.REFUND,
                            Status = TransactionStatus.SUCCEEDED,
                            Description = $"Hoàn cọc do bị hủy khỏi chuyến {assignment.Trip.TripCode}",
                            CreatedAt = DateTime.UtcNow,
                            BalanceBefore = driverWallet.Balance - refundAmount,
                            BalanceAfter = driverWallet.Balance
                        });

                        assignment.DepositStatus = DepositStatus.REFUNDED;

                        // --- [MỚI] GỬI EMAIL HOÀN CỌC ---
                        if (assignment.Driver != null && !string.IsNullOrEmpty(assignment.Driver.Email))
                        {
                            // Chạy async để không block luồng chính
                            _ = _emailService.SendDepositRefundEmailAsync(
                                assignment.Driver.Email,
                                assignment.Driver.FullName,
                                refundAmount,
                                assignment.Trip.TripCode,
                                "Chủ xe hủy phân công (Hoàn cọc 100%)"
                            );
                        }
                    }
                }

                // 5. Cập nhật trạng thái Assignment
                // Thay vì xóa cứng (Delete), ta đổi status để lưu vết lịch sử
                assignment.AssignmentStatus = AssignmentStatus.CANCELLED;
                assignment.UpdateAt = DateTime.UtcNow;

                await _unitOfWork.TripDriverAssignmentRepo.UpdateAsync(assignment);

                // 6. Cập nhật lại Slot trong PostTrip (Nếu assign này đến từ PostTrip)
                if (assignment.Trip.PostTrip != null)
                {
                    // Tìm PostDetail tương ứng với loại tài xế này
                    var postDetail = assignment.Trip.PostTrip.PostTripDetails
                        .FirstOrDefault(d => d.Type == assignment.Type);

                    if (postDetail != null)
                    {
                        // Cộng lại slot
                        postDetail.RequiredCount += 1;

                        // Mở lại trạng thái PostTrip nếu nó đang DONE (đầy)
                        if (assignment.Trip.PostTrip.Status == PostStatus.DONE)
                        {
                            assignment.Trip.PostTrip.Status = PostStatus.OPEN;
                        }

                        assignment.Trip.PostTrip.UpdateAt = DateTime.UtcNow;
                        await _unitOfWork.PostTripRepo.UpdateAsync(assignment.Trip.PostTrip);
                    }
                }

                // 7. Nếu là Main Driver -> Clear thông tin Main Driver trong Trip (nếu cần thiết)
                // (Tuỳ logic của bạn, có thể không cần vì Trip chỉ link qua Assignment)

                // 8. Commit
                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync();

                // 9. Gửi thông báo cho Driver (Fire-and-forget)
                // _ = _notificationService.SendAsync(assignment.DriverId, "BẠN ĐÃ BỊ HỦY KHỎI CHUYẾN", 
                //      $"Chủ xe đã hủy phân công của bạn trong chuyến {assignment.Trip.TripCode}. Tiền cọc (nếu có) đã được hoàn lại.");

                return new ResponseDTO("Đã xóa tài xế và hoàn cọc thành công.", 200, true);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO($"Error cancelling assignment: {ex.Message}", 500, false);
            }
        }

        // =========================================================================================================
        // 4. TÀI XẾ TỰ HỦY CHUYẾN (DRIVER SELF-CANCEL)
        // =========================================================================================================
        // =========================================================================================================
        // 4. TÀI XẾ TỰ HỦY CHUYẾN (DRIVER SELF-CANCEL) - CÓ TÍNH PHẠT HỦY KÈO
        // =========================================================================================================
        public async Task<ResponseDTO> CancelAssignmentByDriverAsync(Guid assignmentId)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var driverId = _userUtility.GetUserIdFromToken();

                // 1. Load Assignment & Related Info
                var assignment = await _unitOfWork.TripDriverAssignmentRepo.GetAll()
                    .Include(a => a.Trip).ThenInclude(t => t.ShippingRoute)
                    .Include(a => a.Driver)
                    .Include(a => a.Trip.PostTrip).ThenInclude(p => p.PostTripDetails)
                    .FirstOrDefaultAsync(a => a.TripDriverAssignmentId == assignmentId);

                if (assignment == null) return new ResponseDTO("Không tìm thấy phân công.", 404, false);

                // 2. Validate
                if (assignment.DriverId != driverId)
                    return new ResponseDTO("Unauthorized: Bạn không phải chủ nhân của phân công này.", 403, false);

                if (assignment.IsOnBoard || assignment.IsFinished)
                    return new ResponseDTO("Không thể hủy khi đã Check-in hoặc hoàn thành.", 400, false);

                if (assignment.AssignmentStatus == AssignmentStatus.CANCELLED)
                    return new ResponseDTO("Chuyến này đã hủy rồi.", 400, false);

                // 3. TÍNH TOÁN THỜI GIAN & MỨC PHẠT (PENALTY CALCULATION)
                var startTime = assignment.Trip.ShippingRoute.ExpectedPickupDate;
                if (startTime == default(DateTime)) startTime = assignment.Trip.CreateAt.AddDays(1); // Fallback nếu data lỗi

                var timeRemaining = startTime - DateTime.UtcNow;
                double hoursRemaining = timeRemaining.TotalHours;

                decimal depositAmount = assignment.DepositAmount;
                decimal penaltyRate = 0;
                string penaltyReason = "";

                // --- RULE HỦY CHUYẾN ---
                if (hoursRemaining >= 72) // > 3 ngày
                {
                    penaltyRate = 0.0m; // Không phạt
                    penaltyReason = "Hủy sớm (Trước 72h) - Hoàn cọc 100%";
                }
                else if (hoursRemaining >= 24) // 1 - 3 ngày
                {
                    penaltyRate = 0.5m; // Phạt 50%
                    penaltyReason = "Hủy gấp (24h-72h) - Phạt 50% cọc";
                }
                else // < 24 giờ (Sát nút)
                {
                    penaltyRate = 1.0m; // Phạt 100%
                    penaltyReason = "Hủy sát giờ (<24h) - Mất 100% cọc";
                }

                // 4. XỬ LÝ TIỀN CỌC (NẾU CÓ CỌC)
                if (assignment.DepositStatus == DepositStatus.DEPOSITED && depositAmount > 0)
                {
                    decimal penaltyAmount = depositAmount * penaltyRate;
                    decimal refundAmount = depositAmount - penaltyAmount;

                    var driverWallet = await _unitOfWork.WalletRepo.FirstOrDefaultAsync(w => w.UserId == driverId);
                    var ownerWallet = await _unitOfWork.WalletRepo.FirstOrDefaultAsync(w => w.UserId == assignment.Trip.OwnerId);

                    if (driverWallet != null && ownerWallet != null)
                    {
                        // A. HOÀN TIỀN CÒN LẠI CHO DRIVER (Nếu có)
                        if (refundAmount > 0)
                        {
                            driverWallet.Balance += refundAmount;
                            driverWallet.LastUpdatedAt = DateTime.UtcNow;

                            await _unitOfWork.TransactionRepo.AddAsync(new Transaction
                            {
                                TransactionId = Guid.NewGuid(),
                                WalletId = driverWallet.WalletId,
                                TripId = assignment.TripId,
                                Amount = refundAmount,
                                Type = TransactionType.REFUND,
                                Status = TransactionStatus.SUCCEEDED,
                                Description = $"Hoàn cọc hủy chuyến {assignment.Trip.TripCode}. ({penaltyReason})",
                                CreatedAt = DateTime.UtcNow,
                                BalanceBefore = driverWallet.Balance - refundAmount,
                                BalanceAfter = driverWallet.Balance
                            });

                            // Gửi mail báo Driver
                            if (!string.IsNullOrEmpty(assignment.Driver.Email))
                            {
                                _ = _emailService.SendDepositRefundEmailAsync(
                                    assignment.Driver.Email,
                                    assignment.Driver.FullName,
                                    refundAmount,
                                    assignment.Trip.TripCode,
                                    penaltyReason
                                );
                            }
                        }

                        // B. CHUYỂN TIỀN PHẠT CHO OWNER (Bồi thường hợp đồng)
                        if (penaltyAmount > 0)
                        {
                            ownerWallet.Balance += penaltyAmount;
                            ownerWallet.LastUpdatedAt = DateTime.UtcNow;

                            await _unitOfWork.TransactionRepo.AddAsync(new Transaction
                            {
                                TransactionId = Guid.NewGuid(),
                                WalletId = ownerWallet.WalletId,
                                TripId = assignment.TripId,
                                Amount = penaltyAmount,
                                Type = TransactionType.COMPENSATION, // Dạng bồi thường
                                Status = TransactionStatus.SUCCEEDED,
                                Description = $"Nhận bồi thường do Tài xế hủy chuyến {assignment.Trip.TripCode}. ({penaltyReason})",
                                CreatedAt = DateTime.UtcNow,
                                BalanceBefore = ownerWallet.Balance - penaltyAmount,
                                BalanceAfter = ownerWallet.Balance
                            });

                            // (Optional) Gửi mail cho Owner báo nhận tiền bồi thường
                        }

                        // Update Wallet trong DB
                        await _unitOfWork.WalletRepo.UpdateAsync(driverWallet);
                        await _unitOfWork.WalletRepo.UpdateAsync(ownerWallet);
                    }

                    // Đánh dấu cọc đã xử lý (Dù là hoàn hay phạt hết thì cũng coi như xong khoản cọc này)
                    assignment.DepositStatus = penaltyRate == 1.0m ? DepositStatus.SEIZED : DepositStatus.REFUNDED;
                }
                else
                {
                    // Case: Không cọc nhưng hủy sát giờ (< 24h)
                    // Có thể ghi log xấu vào hồ sơ tài xế hoặc trừ điểm uy tín (Reputation Score)
                    if (penaltyRate > 0)
                    {
                        penaltyReason += " (Không có cọc để phạt)";
                    }
                }

                // 5. Cập nhật trạng thái Assignment
                assignment.AssignmentStatus = AssignmentStatus.CANCELLED;
                assignment.UpdateAt = DateTime.UtcNow;
                assignment.CheckOutNote = $"Tài xế tự hủy. {penaltyReason}";

                await _unitOfWork.TripDriverAssignmentRepo.UpdateAsync(assignment);

                // 6. Mở lại Slot trong PostTrip (Cho Owner tuyển người mới)
                if (assignment.Trip.PostTrip != null)
                {
                    var postDetail = assignment.Trip.PostTrip.PostTripDetails
                        .FirstOrDefault(d => d.Type == assignment.Type);

                    if (postDetail != null)
                    {
                        postDetail.RequiredCount += 1;

                        if (assignment.Trip.PostTrip.Status == PostStatus.DONE)
                        {
                            assignment.Trip.PostTrip.Status = PostStatus.OPEN;
                        }

                        assignment.Trip.PostTrip.UpdateAt = DateTime.UtcNow;
                        await _unitOfWork.PostTripRepo.UpdateAsync(assignment.Trip.PostTrip);
                    }
                }

                // 7. Commit
                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync();

                return new ResponseDTO($"Hủy chuyến thành công. {penaltyReason}", 200, true, new
                {
                    Refunded = depositAmount * (1 - penaltyRate),
                    Penalty = depositAmount * penaltyRate
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO($"Lỗi hệ thống: {ex.Message}", 500, false);
            }
        }
    }
}
