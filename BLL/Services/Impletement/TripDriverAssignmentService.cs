using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.Enums.Type;
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

        public TripDriverAssignmentService(
            IUnitOfWork unitOfWork,
            UserUtility userUtility,
            ITripDriverContractService tripDriverContractService,
            ITripDeliveryRecordService tripDeliveryRecordService,
            IDeliveryRecordTemplateService templateService,
            IVietMapService vietMapService,
            ITripVehicleHandoverRecordService vehicleHandoverService)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
            _tripDriverContractService = tripDriverContractService;
            _tripDeliveryRecordService = tripDeliveryRecordService;
            _templateService = templateService;
            _vietMapService = vietMapService;
            _vehicleHandoverService = vehicleHandoverService;
        }

        // =========================================================================================================
        // 1. OWNER GÁN TÀI XẾ (ASSIGNMENT BY OWNER) - [UPDATED]
        // =========================================================================================================
        public async Task<ResponseDTO> CreateAssignmentByOwnerAsync(CreateAssignmentDTO dto)
        {
            // [OPTIMIZED] Sử dụng 'using' để tự động quản lý transaction scope
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

                // [NEW] 3. VALIDATE TRIP STATUS (CHẶN NẾU ĐANG CHỜ KÝ HỢP ĐỒNG)
                if (trip.Status != TripStatus.PENDING_DRIVER_ASSIGNMENT)
                {
                    return new ResponseDTO("Không thể gán tài xế khi chuyến đi đang chờ ký hợp đồng với Provider (AWAITING_OWNER_CONTRACT).", 400, false);
                }

                // 4. Validate Driver
                var driver = await _unitOfWork.DriverRepo.GetByIdAsync(dto.DriverId);
                if (driver == null) return new ResponseDTO("Driver not found.", 404, false);

                // 5. Check tài xế đã trong trip chưa
                bool isDriverAlreadyInTrip = await _unitOfWork.TripDriverAssignmentRepo.AnyAsync(
                    a => a.TripId == dto.TripId && a.DriverId == dto.DriverId
                );
                if (isDriverAlreadyInTrip) return new ResponseDTO("Driver is already assigned to this trip.", 400, false);

                // 6. Check Main Driver (Chỉ được 1 tài chính)
                bool isMainDriver = dto.Type == DriverType.PRIMARY;
                if (isMainDriver)
                {
                    bool mainDriverExists = await _unitOfWork.TripDriverAssignmentRepo.AnyAsync(
                        a => a.TripId == dto.TripId && a.Type == DriverType.PRIMARY
                    );
                    if (mainDriverExists) return new ResponseDTO("This trip already has a main driver assigned.", 400, false);
                }

                // 7. VietMap Geocode (Xử lý null safe)
                var startLocationObj = await _vietMapService.GeocodeAsync(dto.StartLocation) ?? new Common.ValueObjects.Location(dto.StartLocation, 0, 0);
                var endLocationObj = await _vietMapService.GeocodeAsync(dto.EndLocation) ?? new Common.ValueObjects.Location(dto.EndLocation, 0, 0);

                // 8. Tạo Assignment
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
                    PaymentStatus = DriverPaymentStatus.UN_PAID
                };
                await _unitOfWork.TripDriverAssignmentRepo.AddAsync(newAssignment);

                // 9. Nếu là Main Driver -> Tạo các biên bản (Hàng hóa & Xe)
                if (isMainDriver)
                {
                    await CreateRecordsForMainDriver(trip.TripId, dto.DriverId, trip.OwnerId);
                }

                // 10. Lưu & Commit
                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync();

                return new ResponseDTO("Driver assigned successfully.", 201, true, new
                {
                    assignmentId = newAssignment.TripDriverAssignmentId,
                    startCoordinates = startLocationObj,
                    endCoordinates = endLocationObj
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO($"Error assigning driver: {ex.Message}", 500, false);
            }
        }

        // =========================================================================================================
        // 2. TÀI XẾ ỨNG TUYỂN (APPLY POST TRIP)
        // =========================================================================================================
        public async Task<ResponseDTO> CreateAssignmentByPostTripAsync(CreateAssignmentByPostTripDTO dto)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                // 1. Validate User
                var driverId = _userUtility.GetUserIdFromToken();
                if (driverId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                // 2. Lấy PostTrip và Details
                var postTrip = await _unitOfWork.PostTripRepo.GetAll()
                    .Include(p => p.PostTripDetails)
                    .FirstOrDefaultAsync(p => p.PostTripId == dto.PostTripId);

                if (postTrip == null) return new ResponseDTO("PostTrip not found.", 404, false);
                if (postTrip.Status != PostStatus.OPEN) return new ResponseDTO("Bài đăng này đã đóng.", 400, false);

                // 3. Tìm Slot ứng tuyển
                var postDetail = postTrip.PostTripDetails.FirstOrDefault(d => d.PostTripDetailId == dto.PostTripDetailId);
                if (postDetail == null) return new ResponseDTO("Slot details not found.", 404, false);

                if (postDetail.RequiredCount <= 0) return new ResponseDTO("Vị trí này đã đủ người.", 400, false);

                // 4. Lấy Trip + RouteData
                // Include cả TripRoute để lấy RouteData (Polyline) phục vụ validate
                var trip = await _unitOfWork.TripRepo.GetAll()
                    .Include(t => t.TripRoute)
                    .Include(t => t.ShippingRoute).ThenInclude(r => r.StartLocation)
                    .Include(t => t.ShippingRoute).ThenInclude(r => r.EndLocation)
                    .FirstOrDefaultAsync(t => t.TripId == postTrip.TripId);

                if (trip == null) return new ResponseDTO("Trip associated with this post not found.", 404, false);

                // 5. Check duplicate application
                bool alreadyApplied = await _unitOfWork.TripDriverAssignmentRepo.AnyAsync(
                    a => a.TripId == postTrip.TripId && a.DriverId == driverId
                );
                if (alreadyApplied) return new ResponseDTO("You have already applied for this trip.", 409, false);

                // 6. Check Main Driver Exists (Nếu đang ứng tuyển làm Main)
                bool isMainDriver = (postDetail.Type == DriverType.PRIMARY);
                if (isMainDriver)
                {
                    bool mainDriverExists = await _unitOfWork.TripDriverAssignmentRepo.AnyAsync(
                        a => a.TripId == trip.TripId && a.Type == DriverType.PRIMARY && a.AssignmentStatus == AssignmentStatus.ACCEPTED
                    );
                    if (mainDriverExists) return new ResponseDTO("This trip already has a main driver.", 400, false);
                }

                // =========================================================================
                // 🛑 7. XỬ LÝ ĐỊA ĐIỂM ĐÓN/TRẢ (CORE LOGIC)
                // =========================================================================

                Common.ValueObjects.Location finalStartLoc;
                Common.ValueObjects.Location finalEndLoc;

                // A. Lấy địa điểm gốc của chuyến đi (để tham chiếu)
                var tripStart = trip.ShippingRoute.StartLocation;
                var tripEnd = trip.ShippingRoute.EndLocation;

                if (isMainDriver)
                {
                    // --- CASE 1: TÀI CHÍNH ---
                    // Bắt buộc lấy từ PostDetail (do Owner nhập) hoặc Trip Start
                    // Bỏ qua hoàn toàn dto.StartLocation

                    string pickupAddr = !string.IsNullOrWhiteSpace(postDetail.PickupLocation)
                                        ? postDetail.PickupLocation
                                        : tripStart.Address;

                    string dropAddr = !string.IsNullOrWhiteSpace(postDetail.DropoffLocation)
                                      ? postDetail.DropoffLocation
                                      : tripEnd.Address;

                    // Geocode để lấy tọa độ chuẩn
                    var geoStart = await _vietMapService.GeocodeAsync(pickupAddr);
                    var geoEnd = await _vietMapService.GeocodeAsync(dropAddr);

                    finalStartLoc = new Common.ValueObjects.Location(pickupAddr, geoStart?.Latitude ?? 0, geoStart?.Longitude ?? 0);
                    finalEndLoc = new Common.ValueObjects.Location(dropAddr, geoEnd?.Latitude ?? 0, geoEnd?.Longitude ?? 0);
                }
                else
                {
                    // --- CASE 2: TÀI PHỤ ---

                    // XỬ LÝ ĐIỂM ĐÓN (Start)
                    if (!string.IsNullOrWhiteSpace(dto.StartLocation))
                    {
                        // -- Tài phụ REQUEST điểm đón riêng --
                        var geoStart = await _vietMapService.GeocodeAsync(dto.StartLocation);
                        if (geoStart == null || (geoStart.Latitude == 0 && geoStart.Longitude == 0))
                            return new ResponseDTO("Không tìm thấy địa chỉ đón bạn nhập.", 400, false);

                        // Validate On-Route (Cách đường đi tối đa 10km)
                        if (trip.TripRoute != null && !string.IsNullOrEmpty(trip.TripRoute.RouteData))
                        {
                            bool isOnRoute = _vietMapService.IsLocationOnRoute(geoStart, trip.TripRoute.RouteData, bufferKm: 10.0);
                            if (!isOnRoute)
                                return new ResponseDTO("Điểm đón quá xa lộ trình xe chạy (>10km). Vui lòng chọn điểm gần hơn.", 400, false);
                        }

                        finalStartLoc = new Common.ValueObjects.Location(geoStart.Address, geoStart.Latitude ?? 0, geoStart.Longitude ?? 0);
                    }
                    else
                    {
                        // -- Tài phụ KHÔNG nhập (Theo Owner) --
                        string defaultPickup = !string.IsNullOrWhiteSpace(postDetail.PickupLocation)
                                               ? postDetail.PickupLocation
                                               : tripStart.Address;
                        var geoStart = await _vietMapService.GeocodeAsync(defaultPickup);
                        finalStartLoc = new Common.ValueObjects.Location(defaultPickup, geoStart?.Latitude ?? 0, geoStart?.Longitude ?? 0);
                    }

                    // XỬ LÝ ĐIỂM TRẢ (End) - Logic tương tự
                    if (!string.IsNullOrWhiteSpace(dto.EndLocation))
                    {
                        var geoEnd = await _vietMapService.GeocodeAsync(dto.EndLocation);
                        if (geoEnd == null || (geoEnd.Latitude == 0 && geoEnd.Longitude == 0))
                            return new ResponseDTO("Không tìm thấy địa chỉ trả bạn nhập.", 400, false);

                        // Validate On-Route (Cách đường đi tối đa 10km)
                        if (trip.TripRoute != null && !string.IsNullOrEmpty(trip.TripRoute.RouteData))
                        {
                            bool isOnRoute = _vietMapService.IsLocationOnRoute(geoEnd, trip.TripRoute.RouteData, bufferKm: 10.0);
                            if (!isOnRoute)
                                return new ResponseDTO("Điểm trả quá xa lộ trình xe chạy (>10km). Vui lòng chọn điểm gần hơn.", 400, false);
                        }

                        finalEndLoc = new Common.ValueObjects.Location(geoEnd.Address, geoEnd.Latitude ?? 0, geoEnd.Longitude ?? 0);
                    }
                    else
                    {
                        string defaultDrop = !string.IsNullOrWhiteSpace(postDetail.DropoffLocation)
                                             ? postDetail.DropoffLocation
                                             : tripEnd.Address;
                        var geoEnd = await _vietMapService.GeocodeAsync(defaultDrop);
                        finalEndLoc = new Common.ValueObjects.Location(defaultDrop, geoEnd?.Latitude ?? 0, geoEnd?.Longitude ?? 0);
                    }
                }

                // =========================================================================

                // 8. Tạo Assignment
                var newAssignment = new TripDriverAssignment
                {
                    TripDriverAssignmentId = Guid.NewGuid(),
                    TripId = postTrip.TripId,
                    DriverId = driverId,
                    Type = postDetail.Type,
                    CreateAt = DateTime.UtcNow,
                    UpdateAt = DateTime.UtcNow,
                    BaseAmount = postDetail.PricePerPerson, // Giá tiền giữ nguyên theo Post

                    StartLocation = finalStartLoc,
                    EndLocation = finalEndLoc,

                    AssignmentStatus = AssignmentStatus.ACCEPTED,
                    PaymentStatus = DriverPaymentStatus.UN_PAID
                };
                await _unitOfWork.TripDriverAssignmentRepo.AddAsync(newAssignment);

                // 9. Contract Logic
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

                return new ResponseDTO("Applied successfully.", 201, true, new { assignmentId = newAssignment.TripDriverAssignmentId });
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
    }
}