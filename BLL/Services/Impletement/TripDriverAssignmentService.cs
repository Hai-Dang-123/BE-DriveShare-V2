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


        // 1. INJECT VIETMAP SERVICE
        private readonly IVietMapService _vietMapService;
        public TripDriverAssignmentService(
            IUnitOfWork unitOfWork,
            UserUtility userUtility,
            ITripDriverContractService tripDriverContractService,
            ITripDeliveryRecordService tripDeliveryRecordService,
            IDeliveryRecordTemplateService templateService,
            IVietMapService vietMapService)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
            _tripDriverContractService = tripDriverContractService;
            _tripDeliveryRecordService = tripDeliveryRecordService;
            _templateService = templateService;
            _vietMapService = vietMapService;
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

                //if (trip.Status != TripStatus.PENDING_DRIVER_ASSIGNMENT)
                //    return new ResponseDTO($"Invalid trip status. Must be {TripStatus.PENDING_DRIVER_ASSIGNMENT}.", 400, false);

                var driver = await _unitOfWork.DriverRepo.GetByIdAsync(dto.DriverId);
                if (driver == null)
                    return new ResponseDTO("Driver not found.", 404, false);

                bool isMainDriver = dto.Type == Common.Enums.Type.DriverType.PRIMARY;

                // VALIDATE: Chỉ có 1 tài xế CHÍNH
                if (isMainDriver)
                {
                    bool mainDriverExists = await _unitOfWork.TripDriverAssignmentRepo.AnyAsync(
                        a => a.TripId == dto.TripId && a.Type == Common.Enums.Type.DriverType.PRIMARY
                    );
                    if (mainDriverExists)
                        throw new Exception("This trip already has a main driver assigned.");
                }

                // ==========================================================
                // 2. GỌI VIETMAP ĐỂ LẤY TỌA ĐỘ TỪ ĐỊA CHỈ STRING
                // ==========================================================

                // Chuyển đổi địa chỉ Start
                var startLocationObj = await _vietMapService.GeocodeAsync(dto.StartLocation);
                if (startLocationObj == null)
                    // Fallback nếu lỗi: Tạo location chỉ có address, lat/lon = 0
                    startLocationObj = new Common.ValueObjects.Location(dto.StartLocation, 0, 0);

                // Chuyển đổi địa chỉ End
                var endLocationObj = await _vietMapService.GeocodeAsync(dto.EndLocation);
                if (endLocationObj == null)
                    endLocationObj = new Common.ValueObjects.Location(dto.EndLocation, 0, 0);

                // ==========================================================

                // 3. Tạo Assignment
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

                    // Gán object Location đã lấy được từ VietMap
                    StartLocation = startLocationObj,
                    EndLocation = endLocationObj,

                    AssignmentStatus = AssignmentStatus.ACCEPTED,
                    PaymentStatus = DriverPaymentStatus.UN_PAID
                };
                await _unitOfWork.TripDriverAssignmentRepo.AddAsync(newAssignment);

                // 4. Nếu là tài xế CHÍNH -> Tự động tạo 2 Biên bản (Nhận và Giao)
                if (isMainDriver)
                {
                    await CreateDeliveryRecordsForMainDriver(trip.TripId, dto.DriverId);
                }

                // 5. Commit
                await _unitOfWork.CommitTransactionAsync();

                return new ResponseDTO("Driver assigned successfully.", 201, true, new
                {
                    assignmentId = newAssignment.TripDriverAssignmentId,
                    newTripStatus = trip.Status.ToString(),
                    // Trả về location để frontend biết tọa độ đã được map
                    startCoordinates = startLocationObj,
                    endCoordinates = endLocationObj
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

                // 1. LẤY POSTTRIP (và load các slot)
                var postTrip = await _unitOfWork.PostTripRepo.GetAll()
                    .Include(p => p.PostTripDetails)
                    .FirstOrDefaultAsync(p => p.PostTripId == dto.PostTripId);

                if (postTrip == null)
                    return new ResponseDTO("PostTrip not found.", 404, false);

                if (postTrip.Status != PostStatus.OPEN)
                    return new ResponseDTO("This job posting is no longer open.", 400, false);

                // 2. LẤY SLOT CỤ THỂ
                var postDetail = postTrip.PostTripDetails.FirstOrDefault(d => d.PostTripDetailId == dto.PostTripDetailId);
                if (postDetail == null)
                    return new ResponseDTO("Job slot not found.", 404, false);

                // 3. LẤY TRIP
                var trip = await _unitOfWork.TripRepo.GetByIdAsync(postTrip.TripId);
                if (trip == null) return new ResponseDTO("Associated Trip not found.", 404, false);

                //if (trip.Status != TripStatus.PENDING_DRIVER_ASSIGNMENT)
                //    return new ResponseDTO("This trip is no longer looking for drivers.", 400, false);

                // 4. KIỂM TRA ĐÃ ỨNG TUYỂN CHƯA
                var existingAssignment = await _unitOfWork.TripDriverAssignmentRepo.FirstOrDefaultAsync(
                    a => a.TripId == postTrip.TripId && a.DriverId == driverId
                );
                if (existingAssignment != null)
                    return new ResponseDTO("You have already applied for this trip.", 409, false);

                // 5. VALIDATE SLOT
                var appliedDriverType = postDetail.Type;
                bool isMainDriver = (appliedDriverType == DriverType.PRIMARY);

                // Đếm số lượng tài xế đã được ACCEPTED cho slot CÙNG LOẠI
                int acceptedCountForType = await _unitOfWork.TripDriverAssignmentRepo.GetAll().CountAsync(
                    a => a.TripId == postTrip.TripId &&
                         a.Type == appliedDriverType &&
                         a.AssignmentStatus == AssignmentStatus.ACCEPTED
                );

                if (acceptedCountForType >= postDetail.RequiredCount)
                {
                    return new ResponseDTO($"This slot for {appliedDriverType} drivers is already full.", 400, false);
                }

                // ==========================================================
                // 🛑 6. GỌI VIETMAP ĐỂ LẤY TỌA ĐỘ (GEOCODING)
                // ==========================================================

                // Chuyển đổi Start Location
                var startLocationObj = await _vietMapService.GeocodeAsync(dto.StartLocation);
                if (startLocationObj == null)
                    startLocationObj = new Common.ValueObjects.Location(dto.StartLocation, 0, 0);

                // Chuyển đổi End Location
                var endLocationObj = await _vietMapService.GeocodeAsync(dto.EndLocation);
                if (endLocationObj == null)
                    endLocationObj = new Common.ValueObjects.Location(dto.EndLocation, 0, 0);

                // ==========================================================

                // 7. TẠO ASSIGNMENT
                var newAssignment = new TripDriverAssignment
                {
                    TripDriverAssignmentId = Guid.NewGuid(),
                    TripId = postTrip.TripId,
                    DriverId = driverId,
                    Type = appliedDriverType,
                    CreateAt = DateTime.UtcNow,
                    UpdateAt = DateTime.UtcNow,
                    BaseAmount = postDetail.PricePerPerson, // Lấy giá từ hệ thống (an toàn)
                    BonusAmount = null,

                    // ⚠️ Gán object Location (đã có tọa độ)
                    StartLocation = startLocationObj,
                    EndLocation = endLocationObj,

                    AssignmentStatus = AssignmentStatus.ACCEPTED,
                    PaymentStatus = DriverPaymentStatus.UN_PAID
                };
                await _unitOfWork.TripDriverAssignmentRepo.AddAsync(newAssignment);

                // 8. KIỂM TRA TÀI XẾ NỘI BỘ
                bool isInternalDriver = await _unitOfWork.OwnerDriverLinkRepo.CheckLinkExistsAsync(trip.OwnerId, driverId, FleetJoinStatus.APPROVED);

                // 9. TẠO HỢP ĐỒNG (NẾU THUÊ NGOÀI)
                if (!isInternalDriver)
                {
                    var contractDto = new CreateTripDriverContractDTO
                    {
                        TripId = trip.TripId,
                        DriverId = driverId
                    };
                    await _tripDriverContractService.CreateContractInternalAsync(
        trip.TripId,                // tripId
        trip.OwnerId,               // ownerId
        driverId,                   // driverId (chính là người đang login)
        postDetail.PricePerPerson   // fare
    );
                }

                // 10. TẠO BIÊN BẢN (NẾU LÀ TÀI XẾ CHÍNH)
                if (isMainDriver)
                {
                    bool recordsExist = await _unitOfWork.TripDeliveryRecordRepo.AnyAsync(r => r.TripId == trip.TripId);
                    if (!recordsExist)
                    {
                        await CreateDeliveryRecordsForMainDriver(trip.TripId, driverId);
                    }
                }

                // 11. CẬP NHẬT TRIP
                await _unitOfWork.TripRepo.UpdateAsync(trip);

                // 12. CẬP NHẬT TRẠNG THÁI POST (Kiểm tra xem đã tuyển ĐỦ hết chưa)
                // Đếm lại tổng số tài xế đã nhận (bao gồm người vừa thêm)
                int totalAcceptedAllTypes = await _unitOfWork.TripDriverAssignmentRepo.GetAll().CountAsync(
                     a => a.TripId == postTrip.TripId && a.AssignmentStatus == AssignmentStatus.ACCEPTED
                ) + 1; // +1 cái vừa add (vì chưa save change nên DB chưa có)

                int totalRequiredAllTypes = postTrip.PostTripDetails.Sum(d => d.RequiredCount);

                if (totalAcceptedAllTypes >= totalRequiredAllTypes)
                {
                    postTrip.Status = PostStatus.DONE;
                    await _unitOfWork.PostTripRepo.UpdateAsync(postTrip);
                }

                // 13. COMMIT
                await _unitOfWork.CommitTransactionAsync();

                return new ResponseDTO("Applied to PostTrip and automatically accepted.", 201, true, new
                {
                    assignmentId = newAssignment.TripDriverAssignmentId,
                    newTripStatus = trip.Status.ToString(),
                    postTripStatus = postTrip.Status.ToString(),
                    // Trả về tọa độ để FE hiển thị map
                    startCoordinates = startLocationObj,
                    endCoordinates = endLocationObj
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