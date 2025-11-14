using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.Enums.Type;
using DAL.Entities;
using DAL.UnitOfWork;
using Microsoft.EntityFrameworkCore; // ⚠️ Đảm bảo using
using System;
using System.Linq; // ⚠️ Đảm bảo using
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class TripDriverAssignmentService : ITripDriverAssignmentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;
        private readonly ITripDriverContractService _tripDriverContractService;
        private readonly ITripDeliveryRecordService _tripDeliveryRecordService;
        private readonly IDeliveryRecordTemplateService _templateService; // ⚠️ Thêm service này

        public TripDriverAssignmentService(
            IUnitOfWork unitOfWork,
            UserUtility userUtility,
            ITripDriverContractService tripDriverContractService,
            ITripDeliveryRecordService tripDeliveryRecordService,
            IDeliveryRecordTemplateService templateService)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
            _tripDriverContractService = tripDriverContractService;
            _tripDeliveryRecordService = tripDeliveryRecordService;
            _templateService = templateService;
        }

        /// <summary>
        /// (Owner) Gán tài xế (nội bộ/thuê) vào Trip.
        /// Tự động ACCEPT, cập nhật TripStatus, tạo HĐ (nếu thuê) và tạo Biên bản (nếu là tài chính).
        /// </summary>
        public async Task<ResponseDTO> CreateAssignmentByOwnerAsync(CreateAssignmentDTO dto)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var ownerId = _userUtility.GetUserIdFromToken();
                var userRole = _userUtility.GetUserRoleFromToken();
                if (ownerId == Guid.Empty || userRole != "Owner")
                    return new ResponseDTO("Unauthorized: Chỉ 'Owner' mới có thể gán tài xế.", 401, false);

                var trip = await _unitOfWork.TripRepo.GetByIdAsync(dto.TripId);
                if (trip == null)
                    return new ResponseDTO("Trip not found.", 404, false);
                if (trip.OwnerId != ownerId)
                    return new ResponseDTO("Forbidden: Bạn không sở hữu chuyến đi này.", 403, false);

                if (trip.Status != TripStatus.PENDING_DRIVER_ASSIGNMENT)
                    return new ResponseDTO($"Invalid trip status. Must be {TripStatus.PENDING_DRIVER_ASSIGNMENT}.", 400, false);

                var driver = await _unitOfWork.DriverRepo.GetByIdAsync(dto.DriverId);
                if (driver == null)
                    return new ResponseDTO("Driver not found.", 404, false);

                // (Giả sử DriverType.PRIMARY là 0, SECONDARY là 1)
                bool isMainDriver = dto.Type == Common.Enums.Type.DriverType.PRIMARY;

                // 5. VALIDATE: Chỉ có 1 tài xế CHÍNH
                if (isMainDriver)
                {
                    bool mainDriverExists = await _unitOfWork.TripDriverAssignmentRepo.AnyAsync(
                        a => a.TripId == dto.TripId && a.Type == Common.Enums.Type.DriverType.PRIMARY
                    );
                    if (mainDriverExists)
                        throw new Exception("This trip already has a main driver assigned.");
                }

                bool isInternalDriver = await _unitOfWork.OwnerDriverLinkRepo.CheckLinkExistsAsync(ownerId, dto.DriverId, FleetJoinStatus.APPROVED);

                // 6. Tạo Assignment
                var newAssignment = new TripDriverAssignment
                {
                    TripDriverAssignmentId = Guid.NewGuid(),
                    TripId = dto.TripId,
                    DriverId = dto.DriverId,
                    Type = (Common.Enums.Type.DriverType)dto.Type,
                    CreateAt = DateTime.UtcNow,
                    UpdateAt = DateTime.UtcNow,
                    BaseAmount = dto.BaseAmount,
                    BonusAmount = dto.BonusAmount,
                    StartLocation = dto.StartLocation,
                    EndLocation = dto.EndLocation,
                    AssignmentStatus = AssignmentStatus.ACCEPTED, // Gán là "Đã chấp nhận" ngay
                    //PaymentStatus = DriverPaymentStatus.UNPAID
                };
                await _unitOfWork.TripDriverAssignmentRepo.AddAsync(newAssignment);

                // 7. Cập nhật Trạng thái Trip VÀ gọi Service khác
                if (isInternalDriver)
                {
                    trip.Status = TripStatus.READY_FOR_VEHICLE_HANDOVER;
                }
                else
                {
                    trip.Status = TripStatus.AWAITING_DRIVER_CONTRACT;
                    var contractDto = new CreateTripDriverContractDTO
                    {
                        TripId = dto.TripId,
                        DriverId = dto.DriverId
                    };
                    await _tripDriverContractService.CreateContractInternalAsync(contractDto, ownerId);
                }
                await _unitOfWork.TripRepo.UpdateAsync(trip);


                // 8. ⚠️ LOGIC MỚI: Nếu là tài xế CHÍNH -> Tự động tạo 2 Biên bản (Nhận và Giao)
                if (isMainDriver)
                {
                    await CreateDeliveryRecordsForMainDriver(trip.TripId, dto.DriverId);
                }

                // (Cập nhật driver.IsInTrip = true; nếu cần)

                // 9. Commit
                await _unitOfWork.CommitTransactionAsync();

                return new ResponseDTO("Driver assigned successfully. Trip status updated.", 201, true, new
                {
                    assignmentId = newAssignment.TripDriverAssignmentId,
                    newTripStatus = trip.Status.ToString()
                });
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                Console.WriteLine($"Error in CreateAssignmentByOwnerAsync: {ex.Message}");
                return new ResponseDTO($"Error assigning driver: {ex.Message}", 500, false);
            }
        }

        /// <summary>
        /// (Driver) Ứng tuyển vào PostTrip.
        /// Tự động ACCEPT, cập nhật TripStatus, tạo HĐ (nếu thuê) và tạo Biên bản (nếu là tài chính).
        /// </summary>
        public async Task<ResponseDTO> CreateAssignmentByPostTripAsync(CreateAssignmentByPostTripDTO dto)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var driverId = _userUtility.GetUserIdFromToken();
                var userRole = _userUtility.GetUserRoleFromToken();
                if (driverId == Guid.Empty || userRole != "Driver")
                    return new ResponseDTO("Unauthorized: Chỉ 'Driver' mới có thể ứng tuyển.", 401, false);

                var driver = await _unitOfWork.DriverRepo.GetByIdAsync(driverId);
                if (driver == null)
                    return new ResponseDTO("Driver (self) not found.", 404, false);

                var postTrip = await _unitOfWork.PostTripRepo.GetByIdAsync(dto.PostTripId);
                if (postTrip == null)
                    return new ResponseDTO("PostTrip not found.", 404, false);

                if (postTrip.Status != PostStatus.OPEN)
                    return new ResponseDTO("This job posting is no longer open.", 400, false);

                var trip = await _unitOfWork.TripRepo.GetByIdAsync(postTrip.TripId);
                if (trip == null)
                    return new ResponseDTO("Associated Trip not found.", 404, false);
                if (trip.Status != TripStatus.PENDING_DRIVER_ASSIGNMENT)
                    return new ResponseDTO("This trip is no longer looking for drivers.", 400, false);

                var existingAssignment = await _unitOfWork.TripDriverAssignmentRepo.FirstOrDefaultAsync(
                    a => a.TripId == postTrip.TripId && a.DriverId == driverId
                );
                if (existingAssignment != null)
                    return new ResponseDTO("You have already applied for this trip.", 409, false);

                // ⚠️ SỬA ĐỔI: (Giả sử DriverType.PRIMARY là 0, SECONDARY là 1)
                bool isMainDriver = postTrip.Type == Common.Enums.Type.DriverType.PRIMARY;

                // 5. VALIDATE: Chỉ có 1 tài xế CHÍNH (đã được duyệt)
                if (isMainDriver)
                {
                    bool mainDriverExists = await _unitOfWork.TripDriverAssignmentRepo.AnyAsync(
                        a => a.TripId == postTrip.TripId &&
                             a.Type == Common.Enums.Type.DriverType.PRIMARY &&
                             a.AssignmentStatus == AssignmentStatus.ACCEPTED
                    );
                    if (mainDriverExists)
                        throw new Exception("This trip already has an accepted main driver.");
                }

                // 6. Tạo Assignment (ACCEPTED)
                var newAssignment = new TripDriverAssignment
                {
                    TripDriverAssignmentId = Guid.NewGuid(),
                    TripId = postTrip.TripId,
                    DriverId = driverId,
                    Type = (Common.Enums.Type.DriverType)postTrip.Type,
                    CreateAt = DateTime.UtcNow,
                    UpdateAt = DateTime.UtcNow,
                    BaseAmount = dto.OfferedAmount,
                    BonusAmount = null,
                    StartLocation = dto.StartLocation,
                    EndLocation = dto.EndLocation,
                    AssignmentStatus = AssignmentStatus.ACCEPTED, // ⚠️ SỬA ĐỔI: Tự động chấp nhận
                    //PaymentStatus = DriverPaymentStatus.UNPAID
                };
                await _unitOfWork.TripDriverAssignmentRepo.AddAsync(newAssignment);

                // 7. Kiểm tra tài xế nội bộ hay thuê ngoài
                bool isInternalDriver = await _unitOfWork.OwnerDriverLinkRepo.CheckLinkExistsAsync(trip.OwnerId, driverId, FleetJoinStatus.APPROVED);

                // 8. Tự động tạo Hợp đồng (nếu là thuê ngoài) và Cập nhật TripStatus
                if (!isInternalDriver)
                {
                    trip.Status = TripStatus.AWAITING_DRIVER_CONTRACT;
                    var contractDto = new CreateTripDriverContractDTO
                    {
                        TripId = trip.TripId,
                        DriverId = driverId
                    };
                    await _tripDriverContractService.CreateContractInternalAsync(contractDto, trip.OwnerId);
                }
                else
                {
                    // Tài xế nội bộ -> Sẵn sàng lấy xe
                    trip.Status = TripStatus.READY_FOR_VEHICLE_HANDOVER;
                }

                // 9. ⚠️ LOGIC MỚI: Nếu là tài xế CHÍNH -> Tự động tạo 2 Biên bản
                if (isMainDriver)
                {
                    await CreateDeliveryRecordsForMainDriver(trip.TripId, driverId);
                }

                // 10. Cập nhật Trip VÀ PostTrip
                await _unitOfWork.TripRepo.UpdateAsync(trip);

                postTrip.Status = PostStatus.DONE; // Đóng bài đăng
                await _unitOfWork.PostTripRepo.UpdateAsync(postTrip);


                // 11. Commit
                await _unitOfWork.CommitTransactionAsync();

                return new ResponseDTO("Applied to PostTrip and automatically accepted.", 201, true, new
                {
                    assignmentId = newAssignment.TripDriverAssignmentId,
                    newTripStatus = trip.Status.ToString()
                });
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                Console.WriteLine($"Error in CreateAssignmentByPostTripAsync: {ex.Message}");
                return new ResponseDTO($"Error applying for trip: {ex.Message}", 500, false);
            }
        }

        // ─────── HÀM PRIVATE HELPER (ĐÃ CẬP NHẬT) ───────

        /// <summary>
        /// (Nội bộ) Lấy 2 contact (Sender/Receiver) từ Trip và tạo 2 biên bản (Pickup/Dropoff).
        /// Hàm này phải được gọi BÊN TRONG một Transaction đang chạy.
        /// </summary>
        private async Task CreateDeliveryRecordsForMainDriver(Guid tripId, Guid mainDriverId)
        {
            // 1. Lấy 2 contacts (Sender và Receiver)
            var contacts = await _unitOfWork.TripContactRepo.GetAll()
                                .Where(c => c.TripId == tripId)
                                .ToListAsync();

            var senderContact = contacts.FirstOrDefault(c => c.Type == ContactType.SENDER);
            var receiverContact = contacts.FirstOrDefault(c => c.Type == ContactType.RECEIVER);

            if (senderContact == null || receiverContact == null)
                throw new Exception("Trip is missing Sender or Receiver contact info. Cannot auto-generate records.");

            // 2. ⚠️ SỬA ĐỔI: Lấy 2 templates (PICKUP và DROPOFF)
            // (Giả định _templateService (IDeliveryRecordTemplateService) đã được inject)
            var pickupTemplate = await _templateService.GetLatestTemplateByTypeAsync(DeliveryRecordType.PICKUP);
            var dropoffTemplate = await _templateService.GetLatestTemplateByTypeAsync(DeliveryRecordType.DROPOFF);

            // 3. Tạo Biên bản 1: PICKUP (Nhận hàng)
            var pickupRecordDto = new TripDeliveryRecordCreateDTO
            {
                TripId = tripId,
                DeliveryRecordTempalteId = pickupTemplate.DeliveryRecordTemplateId, // ⚠️ Đã điền
                StripContractId = senderContact.TripContactId, // Gán ContactId của Người gửi
                Notes = "Auto-generated record for main driver (Pickup).",
                type = DeliveryRecordType.PICKUP
            };
            // Gọi service (hàm này không SaveChanges)
            await _tripDeliveryRecordService.CreateTripDeliveryRecordAsync(pickupRecordDto, mainDriverId);

            // 4. Tạo Biên bản 2: DROPOFF (Giao hàng)
            var dropoffRecordDto = new TripDeliveryRecordCreateDTO
            {
                TripId = tripId,
                DeliveryRecordTempalteId = dropoffTemplate.DeliveryRecordTemplateId, // ⚠️ Đã điền
                StripContractId = receiverContact.TripContactId, // Gán ContactId của Người nhận
                Notes = "Auto-generated record for main driver (Dropoff).",
                type = DeliveryRecordType.DROPOFF
            };
            // Gọi service (hàm này không SaveChanges)
            await _tripDeliveryRecordService.CreateTripDeliveryRecordAsync(dropoffRecordDto, mainDriverId);
        }
    }
}