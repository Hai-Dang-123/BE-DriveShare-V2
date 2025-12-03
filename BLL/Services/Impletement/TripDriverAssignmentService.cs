using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;

using Common.Enums.Status;
using Common.Enums.Type;
using DAL.Entities;
using DAL.UnitOfWork;
using Microsoft.EntityFrameworkCore;
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

        // 1. INJECT SERVICE GIAO NHẬN XE (MỚI)
        private readonly ITripVehicleHandoverRecordService _vehicleHandoverService;

        public TripDriverAssignmentService(
            IUnitOfWork unitOfWork,
            UserUtility userUtility,
            ITripDriverContractService tripDriverContractService,
            ITripDeliveryRecordService tripDeliveryRecordService,
            IDeliveryRecordTemplateService templateService,
            IVietMapService vietMapService,
            ITripVehicleHandoverRecordService vehicleHandoverService) // Inject vào đây
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
            _tripDriverContractService = tripDriverContractService;
            _tripDeliveryRecordService = tripDeliveryRecordService;
            _templateService = templateService;
            _vietMapService = vietMapService;
            _vehicleHandoverService = vehicleHandoverService;
        }

        /// <summary>
        /// (Owner) Gán tài xế (nội bộ/thuê) vào Trip.
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

                var driver = await _unitOfWork.DriverRepo.GetByIdAsync(dto.DriverId);
                if (driver == null)
                    return new ResponseDTO("Driver not found.", 404, false);

                // --- VALIDATE 1: TÀI XẾ ĐÃ CÓ TRONG TRIP CHƯA? ---
                bool isDriverAlreadyInTrip = await _unitOfWork.TripDriverAssignmentRepo.AnyAsync(
                    a => a.TripId == dto.TripId && a.DriverId == dto.DriverId
                );

                if (isDriverAlreadyInTrip)
                {
                    return new ResponseDTO("Driver is already assigned to this trip.", 400, false);
                }

                bool isMainDriver = dto.Type == Common.Enums.Type.DriverType.PRIMARY;

                // --- VALIDATE 2: CHỈ CÓ 1 TÀI XẾ CHÍNH ---
                if (isMainDriver)
                {
                    bool mainDriverExists = await _unitOfWork.TripDriverAssignmentRepo.AnyAsync(
                        a => a.TripId == dto.TripId && a.Type == Common.Enums.Type.DriverType.PRIMARY
                    );
                    if (mainDriverExists)
                        return new ResponseDTO("This trip already has a main driver assigned.", 400, false);
                }

                // ==========================================================
                // GỌI VIETMAP & TẠO ASSIGNMENT
                // ==========================================================

                var startLocationObj = await _vietMapService.GeocodeAsync(dto.StartLocation);
                if (startLocationObj == null) startLocationObj = new Common.ValueObjects.Location(dto.StartLocation, 0, 0);

                var endLocationObj = await _vietMapService.GeocodeAsync(dto.EndLocation);
                if (endLocationObj == null) endLocationObj = new Common.ValueObjects.Location(dto.EndLocation, 0, 0);

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

                // --- TẠO BIÊN BẢN (NẾU LÀ MAIN DRIVER) ---
                // Gọi helper function để tạo cả biên bản Hàng Hóa và Xe
                if (isMainDriver)
                {
                    await CreateRecordsForMainDriver(trip.TripId, dto.DriverId);
                }

                await _unitOfWork.CommitTransactionAsync();

                return new ResponseDTO("Driver assigned successfully.", 201, true, new
                {
                    assignmentId = newAssignment.TripDriverAssignmentId,
                    startCoordinates = startLocationObj,
                    endCoordinates = endLocationObj
                });
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return new ResponseDTO($"Error assigning driver: {ex.Message}", 500, false);
            }
        }

        /// <summary>
        /// (Driver) Ứng tuyển vào PostTrip.
        /// </summary>
        public async Task<ResponseDTO> CreateAssignmentByPostTripAsync(CreateAssignmentByPostTripDTO dto)
        {
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                var driverId = _userUtility.GetUserIdFromToken();
                // ... (Validation User/Role)

                var postTrip = await _unitOfWork.PostTripRepo.GetAll()
                    .Include(p => p.PostTripDetails)
                    .FirstOrDefaultAsync(p => p.PostTripId == dto.PostTripId);

                if (postTrip == null) return new ResponseDTO("PostTrip not found.", 404, false);

                var postDetail = postTrip.PostTripDetails.FirstOrDefault(d => d.PostTripDetailId == dto.PostTripDetailId);
                if (postDetail == null) return new ResponseDTO("Slot not found.", 404, false);

                var trip = await _unitOfWork.TripRepo.GetByIdAsync(postTrip.TripId);

                // --- VALIDATE: ĐÃ ỨNG TUYỂN CHƯA ---
                var existingAssignment = await _unitOfWork.TripDriverAssignmentRepo.FirstOrDefaultAsync(
                    a => a.TripId == postTrip.TripId && a.DriverId == driverId
                );
                if (existingAssignment != null)
                    return new ResponseDTO("You have already applied for this trip.", 409, false);

                // --- VALIDATE SLOT ---
                var appliedDriverType = postDetail.Type;
                bool isMainDriver = (appliedDriverType == DriverType.PRIMARY);

                // Nếu là Main Driver -> Check xem đã có ai làm Main Driver chưa
                if (isMainDriver)
                {
                    bool mainDriverExists = await _unitOfWork.TripDriverAssignmentRepo.AnyAsync(
                        a => a.TripId == trip.TripId
                             && a.Type == DriverType.PRIMARY
                             && a.AssignmentStatus == AssignmentStatus.ACCEPTED
                    );
                    if (mainDriverExists)
                        return new ResponseDTO("This trip already has a main driver.", 400, false);
                }

                // ... (Logic VietMap & Tạo Assignment) ...
                var startLocationObj = await _vietMapService.GeocodeAsync(dto.StartLocation) ?? new Common.ValueObjects.Location(dto.StartLocation, 0, 0);
                var endLocationObj = await _vietMapService.GeocodeAsync(dto.EndLocation) ?? new Common.ValueObjects.Location(dto.EndLocation, 0, 0);

                var newAssignment = new TripDriverAssignment
                {
                    TripDriverAssignmentId = Guid.NewGuid(),
                    TripId = postTrip.TripId,
                    DriverId = driverId,
                    Type = appliedDriverType,
                    CreateAt = DateTime.UtcNow,
                    UpdateAt = DateTime.UtcNow,
                    BaseAmount = postDetail.PricePerPerson,
                    StartLocation = startLocationObj,
                    EndLocation = endLocationObj,
                    AssignmentStatus = AssignmentStatus.ACCEPTED,
                    PaymentStatus = DriverPaymentStatus.UN_PAID
                };
                await _unitOfWork.TripDriverAssignmentRepo.AddAsync(newAssignment);

                // ... (Logic Contract) ...
                bool isInternalDriver = await _unitOfWork.OwnerDriverLinkRepo.CheckLinkExistsAsync(trip.OwnerId, driverId, FleetJoinStatus.APPROVED);
                if (!isInternalDriver)
                {
                    await _tripDriverContractService.CreateContractInternalAsync(trip.TripId, trip.OwnerId, driverId, postDetail.PricePerPerson);
                }

                // --- TẠO BIÊN BẢN (NẾU LÀ MAIN DRIVER) ---
                if (isMainDriver)
                {
                    //await CreateRecordsForMainDriver(trip.TripId, driverId);
                    // 1. Chuyển trạng thái Trip đúng giai đoạn
                    trip.Status = TripStatus.READY_FOR_VEHICLE_HANDOVER;
                    trip.UpdateAt = DateTime.UtcNow;
                    await _unitOfWork.TripRepo.UpdateAsync(trip);

                    // 2. Sau đó mới được phép tạo biên bản giao – nhận xe
                    await CreateRecordsForMainDriver(trip.TripId, driverId);
                }

                // ... (Update Trip & PostTrip Status) ...
                await _unitOfWork.TripRepo.UpdateAsync(trip);

                // Logic check full slot để đóng PostTrip
                int totalAccepted = await _unitOfWork.TripDriverAssignmentRepo.GetAll().CountAsync(
                     a => a.TripId == postTrip.TripId && a.AssignmentStatus == AssignmentStatus.ACCEPTED
                ) + 1;
                int totalRequired = postTrip.PostTripDetails.Sum(d => d.RequiredCount);

                if (totalAccepted >= totalRequired)
                {
                    postTrip.Status = PostStatus.DONE;
                    await _unitOfWork.PostTripRepo.UpdateAsync(postTrip);
                }

                await _unitOfWork.CommitTransactionAsync();

                return new ResponseDTO("Applied successfully.", 201, true, new { assignmentId = newAssignment.TripDriverAssignmentId });
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return new ResponseDTO($"Error: {ex.Message}", 500, false);
            }
        }

        // ─────── HÀM PRIVATE HELPER (QUAN TRỌNG NHẤT) ───────

        /// <summary>
        /// Tạo toàn bộ biên bản cần thiết cho Tài xế Chính:
        /// 1. Biên bản Hàng hóa (TripDeliveryRecord) - Pickup & Dropoff
        /// 2. Biên bản Giao nhận xe (TripVehicleHandoverRecord) - Pickup & Dropoff
        /// </summary>
        private async Task CreateRecordsForMainDriver(Guid tripId, Guid mainDriverId)
        {
            // 0. Lấy thông tin Trip (cần OwnerId)
            var trip = await _unitOfWork.TripRepo.GetByIdAsync(tripId);
            if (trip == null) return;

            // -----------------------------------------------------------
            // A. TẠO BIÊN BẢN HÀNG HÓA (Cargo)
            // -----------------------------------------------------------
            var contacts = await _unitOfWork.TripContactRepo.GetAll()
                                            .Where(c => c.TripId == tripId)
                                            .ToListAsync();

            var senderContact = contacts.FirstOrDefault(c => c.Type == ContactType.SENDER);
            var receiverContact = contacts.FirstOrDefault(c => c.Type == ContactType.RECEIVER);

            // Chỉ tạo biên bản hàng hóa nếu đủ thông tin Contact
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
                        Notes = "Biên bản nhận hàng (Tự động tạo)",
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
                        Notes = "Biên bản giao hàng (Tự động tạo)",
                        type = DeliveryRecordType.DROPOFF
                    };
                    await _tripDeliveryRecordService.CreateTripDeliveryRecordAsync(dropoffDto, mainDriverId);
                }
            }

            // -----------------------------------------------------------
            // B. TẠO BIÊN BẢN GIAO NHẬN XE (Vehicle) - DÙNG SERVICE MỚI
            // -----------------------------------------------------------

            // 1. Biên bản GIAO XE (PICKUP)
            // Ngữ cảnh: Chủ xe (Handover) giao xe cho Tài xế (Receiver) tại điểm xuất phát
            var vehiclePickupDto = new TripVehicleHandoverRecordCreateDTO
            {
                TripId = tripId,
                Type = DeliveryRecordType.HANDOVER,
                HandoverUserId = trip.OwnerId,   // Chủ xe giao
                ReceiverUserId = mainDriverId,   // Tài xế nhận
                Notes = "Biên bản giao xe cho tài xế (Khởi tạo tự động)"
            };
            await _vehicleHandoverService.CreateTripVehicleHandoverRecordAsync(vehiclePickupDto);

            // 2. Biên bản TRẢ XE (DROPOFF)
            // Ngữ cảnh: Tài xế (Handover) trả xe lại cho Chủ xe (Receiver) tại điểm kết thúc
            var vehicleDropoffDto = new TripVehicleHandoverRecordCreateDTO
            {
                TripId = tripId,
                Type = DeliveryRecordType.RETURN,
                HandoverUserId = mainDriverId,   // Tài xế trả
                ReceiverUserId = trip.OwnerId,   // Chủ xe nhận
                Notes = "Biên bản trả xe về bãi (Khởi tạo tự động)"
            };
            await _vehicleHandoverService.CreateTripVehicleHandoverRecordAsync(vehicleDropoffDto);
        }
    }
}