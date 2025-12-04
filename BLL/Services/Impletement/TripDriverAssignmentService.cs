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
        // 2. TÀI XẾ ỨNG TUYỂN (APPLY POST TRIP) - FIXED & OPTIMIZED
        // =========================================================================================================
        public async Task<ResponseDTO> CreateAssignmentByPostTripAsync(CreateAssignmentByPostTripDTO dto)
        {
            // Sử dụng 'using' để transaction tự động Dispose/Rollback nếu có lỗi
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                // 1. Validate User
                var driverId = _userUtility.GetUserIdFromToken();
                if (driverId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                // 2. Lấy PostTrip và Details
                // Dùng IQueryable để Include dữ liệu cần thiết
                var postTrip = await _unitOfWork.PostTripRepo.GetAll()
                    .Include(p => p.PostTripDetails)
                    .FirstOrDefaultAsync(p => p.PostTripId == dto.PostTripId);

                if (postTrip == null) return new ResponseDTO("PostTrip not found.", 404, false);

                // 3. Tìm vị trí (Slot) mà tài xế muốn ứng tuyển
                var postDetail = postTrip.PostTripDetails.FirstOrDefault(d => d.PostTripDetailId == dto.PostTripDetailId);
                if (postDetail == null) return new ResponseDTO("Slot details not found.", 404, false);

                var trip = await _unitOfWork.TripRepo.GetByIdAsync(postTrip.TripId);
                if (trip == null) return new ResponseDTO("Trip associated with this post not found.", 404, false);

                // 4. Validate: Đã ứng tuyển vào chuyến này chưa?
                bool alreadyApplied = await _unitOfWork.TripDriverAssignmentRepo.AnyAsync(
                    a => a.TripId == postTrip.TripId && a.DriverId == driverId
                );
                if (alreadyApplied) return new ResponseDTO("You have already applied for this trip.", 409, false);

                // 5. Validate: Nếu là Main Driver, kiểm tra xem chuyến đã có Main Driver chưa
                bool isMainDriver = (postDetail.Type == DriverType.PRIMARY);
                if (isMainDriver)
                {
                    bool mainDriverExists = await _unitOfWork.TripDriverAssignmentRepo.AnyAsync(
                        a => a.TripId == trip.TripId && a.Type == DriverType.PRIMARY && a.AssignmentStatus == AssignmentStatus.ACCEPTED
                    );
                    if (mainDriverExists) return new ResponseDTO("This trip already has a main driver.", 400, false);
                }

                // 6. VietMap Geocode (Xử lý Null safe)
                var startLocationObj = await _vietMapService.GeocodeAsync(dto.StartLocation) ?? new Common.ValueObjects.Location(dto.StartLocation, 0, 0);
                var endLocationObj = await _vietMapService.GeocodeAsync(dto.EndLocation) ?? new Common.ValueObjects.Location(dto.EndLocation, 0, 0);

                // 7. Tạo Assignment Mới
                var newAssignment = new TripDriverAssignment
                {
                    TripDriverAssignmentId = Guid.NewGuid(),
                    TripId = postTrip.TripId,
                    DriverId = driverId,
                    Type = postDetail.Type, // Lưu đúng loại (Primary/Assistant) dựa trên detail
                    CreateAt = DateTime.UtcNow,
                    UpdateAt = DateTime.UtcNow,
                    BaseAmount = postDetail.PricePerPerson,
                    StartLocation = startLocationObj,
                    EndLocation = endLocationObj,
                    AssignmentStatus = AssignmentStatus.ACCEPTED,
                    PaymentStatus = DriverPaymentStatus.UN_PAID
                };
                await _unitOfWork.TripDriverAssignmentRepo.AddAsync(newAssignment);

                // 8. Tạo Hợp đồng (Nếu là tài xế ngoài)
                bool isInternalDriver = await _unitOfWork.OwnerDriverLinkRepo.CheckLinkExistsAsync(trip.OwnerId, driverId, FleetJoinStatus.APPROVED);
                if (!isInternalDriver)
                {
                    await _tripDriverContractService.CreateContractInternalAsync(trip.TripId, trip.OwnerId, driverId, postDetail.PricePerPerson);
                }

                // 9. Xử lý logic riêng cho Main Driver (Cập nhật Trip & Tạo biên bản)
                if (isMainDriver)
                {
                    // Cập nhật trạng thái Trip (nếu cần)
                    // trip.Status = ...; 
                    trip.UpdateAt = DateTime.UtcNow;
                    await _unitOfWork.TripRepo.UpdateAsync(trip);

                    // Tạo biên bản Hàng hóa & Giao xe
                    await CreateRecordsForMainDriver(trip.TripId, driverId, trip.OwnerId);
                }

                // =================================================================================
                // 🛑 10. FIX LOGIC CHECK FULL SLOT (QUAN TRỌNG)
                // =================================================================================

                // A. Lấy danh sách tất cả tài xế ĐÃ NHẬN của chuyến này từ DB
                var currentAssignments = await _unitOfWork.TripDriverAssignmentRepo.GetAll()
                    .Where(a => a.TripId == postTrip.TripId && a.AssignmentStatus == AssignmentStatus.ACCEPTED)
                    .ToListAsync();

                // B. Thêm tài xế VỪA MỚI TẠO vào danh sách (để tính toán vì chưa commit DB)
                currentAssignments.Add(newAssignment);

                // C. Duyệt qua từng yêu cầu trong bài đăng (PostTripDetails)
                bool isAllSlotsFilled = true;

                foreach (var detail in postTrip.PostTripDetails)
                {
                    // Đếm xem hiện tại có bao nhiêu người thuộc Role này (Primary/Assistant)
                    // Lưu ý: detail.Type là loại tài xế yêu cầu (VD: Assistant)
                    int countForThisType = currentAssignments.Count(a => a.Type == detail.Type);

                    // Lấy tổng chỉ tiêu cho Role này (trong trường hợp DB chia nhỏ dòng detail, nên Sum lại)
                    // Thường thì: 1 dòng Primary (count=1) + 1 dòng Assistant (count=2)
                    int requiredForThisType = postTrip.PostTripDetails
                                                .Where(d => d.Type == detail.Type)
                                                .Sum(d => d.RequiredCount);

                    // Nếu số lượng hiện có < số lượng yêu cầu => Chưa Đủ
                    if (countForThisType < requiredForThisType)
                    {
                        isAllSlotsFilled = false;
                        break; // Thoát vòng lặp ngay, không cần check tiếp
                    }
                }

                // D. Nếu tất cả đều đủ -> Đóng bài đăng
                if (isAllSlotsFilled)
                {
                    //postTrip.Status = PostStatus.DONE;
                    //postTrip.UpdateAt = DateTime.UtcNow; // Kiểm tra tên field trong Entity (Updated hoặc UpdateAt)

                    // SẼ NOTIFIY CHO OWNER Ở PHẦN KHÁC

                    await _unitOfWork.PostTripRepo.UpdateAsync(postTrip);
                }

                // 11. COMMIT TRANSACTION
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