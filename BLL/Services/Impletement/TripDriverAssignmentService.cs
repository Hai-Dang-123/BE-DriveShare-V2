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
        private readonly ITripVehicleHandoverRecordService _vehicleHandoverService;
        private readonly IFirebaseUploadService _fileService;
        private readonly IDriverWorkSessionService _driverWorkSessionService;
        private readonly IUserDocumentService _userDocumentService;
        private readonly ITransactionService _transactionService;
        private readonly IEmailService _emailService;
        private readonly IUserService _userService;

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
            IEmailService emailService,
            IUserService userService)
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
            _userService = userService;
        }

        // =========================================================================================================
        // PRIVATE HELPER: VALIDATE GIỜ LÁI & QUOTA (LOGIC 2 CHIỀU + TUẦN 48H)
        // =========================================================================================================
        // =========================================================================================================
        // PRIVATE HELPER: VALIDATE GIỜ LÁI & QUOTA (LOGIC 2 CHIỀU + TUẦN KHỞI HÀNH)
        // =========================================================================================================
        private async Task<(bool IsValid, string ErrorMsg)> ValidateDriverHoursForTripAsync(Guid driverId, Guid tripId, DriverType driverType)
        {
            try
            {
                // 1. Lấy thông tin chuyến đi & Route
                var trip = await _unitOfWork.TripRepo.GetAll()
                    .AsNoTracking()
                    .Include(t => t.ShippingRoute)
                    .FirstOrDefaultAsync(t => t.TripId == tripId);

                if (trip == null) return (false, "Không tìm thấy thông tin chuyến đi.");

                // 2. Tính toán thời gian lái YÊU CẦU (Logic 2 chiều + Buffer)
                // -----------------------------------------------------------------------
                double oneWayHours = (trip.ShippingRoute?.EstimatedDurationHours > 0)
                    ? trip.ShippingRoute.EstimatedDurationHours
                    : (trip.ActualDuration.TotalHours > 0 ? trip.ActualDuration.TotalHours : 0);

                if (oneWayHours <= 0) return (true, ""); // Chưa có dữ liệu thì tạm bỏ qua

                // Tính 2 chiều (Khứ hồi) + Buffer 15%
                double totalTripHours = (oneWayHours * 2) * 1.15;

                // 3. Phân bổ giờ lái (Primary/Secondary)
                double requiredHoursForThisDriver = (driverType == DriverType.SECONDARY)
                    ? totalTripHours / 2
                    : totalTripHours; // Primary tạm tính full

                // 4. Xác định TUẦN CẦN CHECK (Dựa trên ngày khởi hành) - [FIX QUAN TRỌNG]
                // -----------------------------------------------------------------------
                // Nếu chuyến đi có ngày Pickup dự kiến -> Lấy tuần đó. Nếu không -> Lấy tuần hiện tại.
                DateTime anchorDate = trip.ShippingRoute?.ExpectedPickupDate ?? TimeUtil.NowVN();

                // Tính ngày đầu tuần (Thứ 2) của cái tuần khởi hành đó
                // (DayOfWeek.Monday = 1, Sunday = 0 trong C#)
                int diff = (7 + (anchorDate.DayOfWeek - DayOfWeek.Monday)) % 7;
                DateTime startOfWeek = anchorDate.Date.AddDays(-diff);
                DateTime endOfWeek = startOfWeek.AddDays(7);

                // 5. Lấy lịch sử lái trong tuần đó
                // Lưu ý: Nếu check cho tuần tương lai, thường lịch sử sẽ trống (trừ khi đã nhận chuyến khác cùng tuần đó)
                // Để chặt chẽ: Cần tính tổng giờ của các "Chuyến đi đã nhận (Assigned)" trong tuần đó nữa (Future Bookings).
                // Nhưng ở mức độ cơ bản, ta check WorkSession (đã chạy) + Logic chồng lịch (Conflict) ở tầng ngoài là ổn.

                var workSessions = await _unitOfWork.DriverWorkSessionRepo.GetAll()
                    .AsNoTracking()
                    .Where(s => s.DriverId == driverId
                             && s.StartTime >= startOfWeek
                             && s.StartTime < endOfWeek)
                    .ToListAsync();

                double workedHoursInWeek = workSessions.Sum(s => s.DurationInHours);

                // [NÂNG CAO - OPTIONAL]: Cộng thêm giờ của các chuyến "Đã nhận" trong tuần đó (nhưng chưa chạy)
                // Để tránh trường hợp nhận 5 chuyến cho tuần sau -> Bể quota
                var futureTrips = await _unitOfWork.TripDriverAssignmentRepo.GetAll()
                    .AsNoTracking()
                    .Include(a => a.Trip).ThenInclude(t => t.ShippingRoute)
                    .Where(a => a.DriverId == driverId
                             && a.TripId != tripId // Trừ chuyến đang xét
                             && a.AssignmentStatus == AssignmentStatus.ACCEPTED
                             && a.Trip.ShippingRoute.ExpectedPickupDate >= startOfWeek
                             && a.Trip.ShippingRoute.ExpectedPickupDate < endOfWeek)
                    .ToListAsync();

                foreach (var futureTrip in futureTrips)
                {
                    double estOneWay = futureTrip.Trip.ShippingRoute?.EstimatedDurationHours ?? 0;
                    // Cộng giờ dự kiến của các chuyến đã nhận đó vào "đã làm"
                    workedHoursInWeek += (estOneWay * 2 * 1.15);
                }

                // 6. So sánh Quota
                // -----------------------------------------------------------------------
                const double MAX_WEEKLY_HOURS = 48.0;
                double remainingQuota = MAX_WEEKLY_HOURS - workedHoursInWeek;

                if (requiredHoursForThisDriver > remainingQuota)
                {
                    return (false, $"Không đủ giờ lái! Tuần khởi hành ({startOfWeek:dd/MM}) bạn chỉ còn {Math.Round(remainingQuota, 1)}h (Đã book/chạy: {Math.Round(workedHoursInWeek, 1)}h), nhưng chuyến này cần ~{Math.Round(requiredHoursForThisDriver, 1)}h.");
                }

                return (true, "");
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi validate giờ lái: {ex.Message}");
            }
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

                // 1. Validate Quyền & Trip
                if (ownerId == Guid.Empty || userRole != "Owner")
                    return new ResponseDTO("Unauthorized: Chỉ 'Owner' mới có thể gán tài xế.", 401, false);

                var trip = await _unitOfWork.TripRepo.GetByIdAsync(dto.TripId);
                if (trip == null) return new ResponseDTO("Trip not found.", 404, false);
                if (trip.OwnerId != ownerId) return new ResponseDTO("Forbidden: Bạn không sở hữu chuyến đi này.", 403, false);

                // 2. Validate Driver Status
                var driver = await _unitOfWork.DriverRepo.GetByIdAsync(dto.DriverId);
                if (driver == null) return new ResponseDTO("Driver not found.", 404, false);

                if (driver.Status != UserStatus.ACTIVE)
                    return new ResponseDTO("Tài khoản tài xế đang bị khóa hoặc chưa kích hoạt.", 403, false);

                if (!driver.HasDeclaredInitialHistory)
                    return new ResponseDTO("Tài xế cần cập nhật lịch sử lái xe trong tuần hiện tại.", 428, false);

                // 3. Validate Giấy tờ
                var verifiedDocTypes = await _unitOfWork.UserDocumentRepo.GetAll()
                    .Where(d => d.UserId == dto.DriverId && d.Status == VerifileStatus.ACTIVE)
                    .Select(d => d.DocumentType)
                    .ToListAsync();

                bool hasCCCD = verifiedDocTypes.Contains(DocumentType.CCCD);
                bool hasGPLX = verifiedDocTypes.Contains(DocumentType.DRIVER_LINCENSE);
                bool hasGKSK = verifiedDocTypes.Contains(DocumentType.HEALTH_CHECK);

                if (!hasCCCD || !hasGPLX || !hasGKSK)
                    return new ResponseDTO($"Tài xế thiếu giấy tờ bắt buộc (CCCD, GPLX, GKSK).", 403, false);

                if (await _transactionService.IsUserRestrictedDueToDebtAsync(driver.UserId))
                    return new ResponseDTO("Tài khoản đang bị hạn chế do dư nợ.", 403, false);

                // --- [QUAN TRỌNG] CHECK VALIDATE GIỜ LÁI & QUOTA ---
                var hoursCheck = await ValidateDriverHoursForTripAsync(dto.DriverId, dto.TripId, dto.Type);
                if (!hoursCheck.IsValid)
                {
                    return new ResponseDTO(hoursCheck.ErrorMsg, 400, false);
                }

                // --- CHECK AVAILABLE STATUS (Đang không chạy chuyến khác) ---
                var availability = await _driverWorkSessionService.CheckDriverAvailabilityAsync(dto.DriverId);
                if (!availability.CanDrive)
                    return new ResponseDTO($"Tài xế đang không sẵn sàng (Đang chạy chuyến khác hoặc quá giờ lái ngày). {availability.Message}", 400, false);

                // 4. Check Duplicate & Main Driver Constraint
                bool isDriverAlreadyInTrip = await _unitOfWork.TripDriverAssignmentRepo.AnyAsync(
                    a => a.TripId == dto.TripId && a.DriverId == dto.DriverId
                );
                if (isDriverAlreadyInTrip) return new ResponseDTO("Tài xế đã có trong chuyến này.", 400, false);

                bool isMainDriver = dto.Type == DriverType.PRIMARY;
                if (isMainDriver)
                {
                    bool mainDriverExists = await _unitOfWork.TripDriverAssignmentRepo.AnyAsync(
                        a => a.TripId == dto.TripId && a.Type == DriverType.PRIMARY
                    );
                    if (mainDriverExists) return new ResponseDTO("Chuyến đi đã có tài xế chính.", 400, false);
                }

                // 5. Geocode Locations
                var startLocationObj = await _vietMapService.GeocodeAsync(dto.StartLocation) ?? new Common.ValueObjects.Location(dto.StartLocation, 0, 0);
                var endLocationObj = await _vietMapService.GeocodeAsync(dto.EndLocation) ?? new Common.ValueObjects.Location(dto.EndLocation, 0, 0);

                // 6. Tạo Assignment
                var newAssignment = new TripDriverAssignment
                {
                    TripDriverAssignmentId = Guid.NewGuid(),
                    TripId = dto.TripId,
                    DriverId = dto.DriverId,
                    Type = dto.Type,
                    CreateAt = TimeUtil.NowVN(),
                    UpdateAt = TimeUtil.NowVN(),
                    BaseAmount = dto.BaseAmount,
                    BonusAmount = dto.BonusAmount,
                    StartLocation = startLocationObj,
                    EndLocation = endLocationObj,
                    AssignmentStatus = AssignmentStatus.ACCEPTED,
                    PaymentStatus = DriverPaymentStatus.UN_PAID,
                    IsOnBoard = false,
                    DepositStatus = DepositStatus.NOT_REQUIRED
                };

                await _unitOfWork.TripDriverAssignmentRepo.AddAsync(newAssignment);

                // 7. Logic Hợp đồng & Nội bộ
                bool isInternalDriver = await _unitOfWork.OwnerDriverLinkRepo.CheckLinkExistsAsync(trip.OwnerId, dto.DriverId, FleetJoinStatus.APPROVED);
                if (!isInternalDriver)
                {
                    await _tripDriverContractService.CreateContractInternalAsync(trip.TripId, trip.OwnerId, dto.DriverId, dto.BaseAmount);
                }

                if (isMainDriver)
                {
                    trip.UpdateAt = TimeUtil.NowVN();
                    await CreateRecordsForMainDriver(trip.TripId, dto.DriverId, trip.OwnerId);
                    if (isInternalDriver) trip.Status = TripStatus.DONE_ASSIGNING_DRIVER;
                    await _unitOfWork.TripRepo.UpdateAsync(trip);
                }

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
                // 1. VALIDATE DRIVER BASIC INFO
                var driverId = _userUtility.GetUserIdFromToken();
                if (driverId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                var driver = await _unitOfWork.DriverRepo.GetByIdAsync(driverId);
                if (driver == null) return new ResponseDTO("Driver not found.", 404, false);

                if (driver.Status != UserStatus.ACTIVE)
                    return new ResponseDTO("Tài khoản đang bị khóa hoặc chưa kích hoạt.", 403, false);

                if (!driver.HasDeclaredInitialHistory)
                    return new ResponseDTO("Cần cập nhật lịch sử lái xe trước khi nhận chuyến.", 428, false);

                // Validate Giấy tờ
                var verifiedDocTypes = await _unitOfWork.UserDocumentRepo.GetAll()
                    .Where(d => d.UserId == driverId && d.Status == VerifileStatus.ACTIVE)
                    .Select(d => d.DocumentType)
                    .ToListAsync();

                bool hasCCCD = verifiedDocTypes.Contains(DocumentType.CCCD);
                bool hasGPLX = verifiedDocTypes.Contains(DocumentType.DRIVER_LINCENSE);
                bool hasGKSK = verifiedDocTypes.Contains(DocumentType.HEALTH_CHECK);

                if (!hasCCCD || !hasGPLX || !hasGKSK)
                    return new ResponseDTO($"Thiếu giấy tờ xác thực (CCCD, GPLX, GKSK).", 403, false);

                if (await _transactionService.IsUserRestrictedDueToDebtAsync(driver.UserId))
                    return new ResponseDTO("Tài khoản bị hạn chế do dư nợ.", 403, false);

                var availability = await _driverWorkSessionService.CheckDriverAvailabilityAsync(driverId);
                if (!availability.CanDrive)
                    return new ResponseDTO($"Bạn không thể nhận chuyến do đang bận hoặc quá giờ lái ngày. {availability.Message}", 400, false);

                // 2. GET & VALIDATE TRIP/POST DATA
                var postTrip = await _unitOfWork.PostTripRepo.GetAll()
                    .Include(p => p.PostTripDetails)
                    .FirstOrDefaultAsync(p => p.PostTripId == dto.PostTripId);

                if (postTrip == null) return new ResponseDTO("PostTrip not found.", 404, false);
                if (postTrip.Status != PostStatus.OPEN) return new ResponseDTO("Bài đăng này đã đóng.", 400, false);

                var postDetail = postTrip.PostTripDetails.FirstOrDefault(d => d.PostTripDetailId == dto.PostTripDetailId);
                if (postDetail == null) return new ResponseDTO("Slot details not found.", 404, false);
                if (postDetail.RequiredCount <= 0) return new ResponseDTO("Vị trí này đã đủ người.", 400, false);

                // --- [QUAN TRỌNG] CHECK VALIDATE GIỜ LÁI & QUOTA ---
                var hoursCheck = await ValidateDriverHoursForTripAsync(driverId, postTrip.TripId, postDetail.Type);
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

                // Check Main Driver Constraint
                bool isMainDriver = (postDetail.Type == DriverType.PRIMARY);
                if (isMainDriver)
                {
                    bool mainDriverExists = await _unitOfWork.TripDriverAssignmentRepo.AnyAsync(
                        a => a.TripId == trip.TripId && a.Type == DriverType.PRIMARY && a.AssignmentStatus == AssignmentStatus.ACCEPTED
                    );
                    if (mainDriverExists) return new ResponseDTO("Chuyến này đã có tài xế chính.", 400, false);
                }

                // 3. XỬ LÝ TIỀN CỌC
                decimal depositAmount = postDetail.DepositAmount;
                DepositStatus depositStatus = DepositStatus.NOT_REQUIRED;

                if (depositAmount > 0)
                {
                    var driverWallet = await _unitOfWork.WalletRepo.FirstOrDefaultAsync(w => w.UserId == driverId);
                    if (driverWallet == null) return new ResponseDTO("Ví tài xế không tồn tại.", 400, false);
                    if (driverWallet.Balance < depositAmount) return new ResponseDTO($"Số dư ví không đủ để cọc ({depositAmount:N0}đ).", 402, false);

                    var adminId = await _userService.GetAdminUserIdAsync();
                    var adminWallet = await _unitOfWork.WalletRepo.FirstOrDefaultAsync(w => w.UserId == adminId);
                    if (adminWallet == null) return new ResponseDTO("Ví admin không tồn tại.", 500, false);

                    // Trừ Driver
                    var driverBalanceBefore = driverWallet.Balance;
                    driverWallet.Balance -= depositAmount;
                    driverWallet.LastUpdatedAt = TimeUtil.NowVN();
                    await _unitOfWork.WalletRepo.UpdateAsync(driverWallet);
                    await _unitOfWork.TransactionRepo.AddAsync(new Transaction
                    {
                        TransactionId = Guid.NewGuid(),
                        WalletId = driverWallet.WalletId,
                        TripId = trip.TripId,
                        Amount = -depositAmount,
                        Type = TransactionType.DEPOSIT,
                        Status = TransactionStatus.SUCCEEDED,
                        BalanceBefore = driverBalanceBefore,
                        BalanceAfter = driverWallet.Balance,
                        Description = $"Đặt cọc chuyến {trip.TripCode}",
                        CreatedAt = TimeUtil.NowVN(),
                        CompletedAt = TimeUtil.NowVN()
                    });

                    // Cộng Admin
                    var adminBalanceBefore = adminWallet.Balance;
                    adminWallet.Balance += depositAmount;
                    adminWallet.LastUpdatedAt = TimeUtil.NowVN();
                    await _unitOfWork.WalletRepo.UpdateAsync(adminWallet);
                    await _unitOfWork.TransactionRepo.AddAsync(new Transaction
                    {
                        TransactionId = Guid.NewGuid(),
                        WalletId = adminWallet.WalletId,
                        TripId = trip.TripId,
                        Amount = depositAmount,
                        Type = TransactionType.DEPOSIT_HOLD,
                        Status = TransactionStatus.SUCCEEDED,
                        BalanceBefore = adminBalanceBefore,
                        BalanceAfter = adminWallet.Balance,
                        Description = $"Nhận cọc từ tài xế {driverId}",
                        CreatedAt = TimeUtil.NowVN(),
                        CompletedAt = TimeUtil.NowVN()
                    });

                    depositStatus = DepositStatus.DEPOSITED;
                }

                // 4. XỬ LÝ ĐỊA ĐIỂM (Geocoding)
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
                    bool needValidateStart = false;
                    bool needValidateEnd = false;

                    // Xử lý Start
                    if (!string.IsNullOrWhiteSpace(dto.StartLocation))
                    {
                        var geoS = await _vietMapService.GeocodeAsync(dto.StartLocation);
                        if (geoS == null || geoS.Latitude == 0) return new ResponseDTO("Địa chỉ đón nhập vào không hợp lệ.", 400, false);
                        finalStartLoc = new Common.ValueObjects.Location(dto.StartLocation, geoS.Latitude ?? 0, geoS.Longitude ?? 0);
                        needValidateStart = true;
                    }
                    else
                    {
                        string defStart = !string.IsNullOrWhiteSpace(postDetail.PickupLocation) ? postDetail.PickupLocation : tripStart.Address;
                        var geoS = await _vietMapService.GeocodeAsync(defStart);
                        finalStartLoc = new Common.ValueObjects.Location(defStart, geoS?.Latitude ?? 0, geoS?.Longitude ?? 0);
                        needValidateStart = false;
                    }

                    // Xử lý End
                    if (!string.IsNullOrWhiteSpace(dto.EndLocation))
                    {
                        var geoE = await _vietMapService.GeocodeAsync(dto.EndLocation);
                        if (geoE == null || geoE.Latitude == 0) return new ResponseDTO("Địa chỉ trả nhập vào không hợp lệ.", 400, false);
                        finalEndLoc = new Common.ValueObjects.Location(dto.EndLocation, geoE.Latitude ?? 0, geoE.Longitude ?? 0);
                        needValidateEnd = true;
                    }
                    else
                    {
                        string defEnd = !string.IsNullOrWhiteSpace(postDetail.DropoffLocation) ? postDetail.DropoffLocation : tripEnd.Address;
                        var geoE = await _vietMapService.GeocodeAsync(defEnd);
                        finalEndLoc = new Common.ValueObjects.Location(defEnd, geoE?.Latitude ?? 0, geoE?.Longitude ?? 0);
                        needValidateEnd = false;
                    }

                    // Validate Khoảng cách
                    if (needValidateStart || needValidateEnd)
                    {
                        string encodedPolyline = trip.TripRoute.RouteData;
                        if (string.IsNullOrEmpty(encodedPolyline))
                        {
                            var routePath = await _vietMapService.GetRouteAsync(tripStart, tripEnd, "truck");
                            if (routePath != null) encodedPolyline = routePath.Points;
                        }

                        if (string.IsNullOrEmpty(encodedPolyline)) return new ResponseDTO("Hệ thống không xác định được lộ trình.", 500, false);

                        if (needValidateStart)
                        {
                            bool isPickupValid = _vietMapService.IsLocationOnRoute(finalStartLoc, encodedPolyline, 5.0);
                            if (!isPickupValid) return new ResponseDTO($"Điểm đón '{finalStartLoc.Address}' quá xa lộ trình (>5km).", 400, false);
                        }
                        if (needValidateEnd)
                        {
                            bool isDropoffValid = _vietMapService.IsLocationOnRoute(finalEndLoc, encodedPolyline, 5.0);
                            if (!isDropoffValid) return new ResponseDTO($"Điểm trả '{finalEndLoc.Address}' quá xa lộ trình (>5km).", 400, false);
                        }
                    }
                }

                // 5. TẠO ASSIGNMENT
                var newAssignment = new TripDriverAssignment
                {
                    TripDriverAssignmentId = Guid.NewGuid(),
                    TripId = postTrip.TripId,
                    DriverId = driverId,
                    Type = postDetail.Type,
                    CreateAt = TimeUtil.NowVN(),
                    UpdateAt = TimeUtil.NowVN(),
                    BaseAmount = postDetail.PricePerPerson,
                    BonusAmount = postDetail.BonusAmount,
                    DepositAmount = depositAmount,
                    DepositStatus = depositStatus,
                    DepositAt = (depositStatus == DepositStatus.DEPOSITED) ? TimeUtil.NowVN() : null,
                    StartLocation = finalStartLoc,
                    EndLocation = finalEndLoc,
                    AssignmentStatus = AssignmentStatus.ACCEPTED,
                    PaymentStatus = DriverPaymentStatus.UN_PAID,
                    IsOnBoard = false
                };
                await _unitOfWork.TripDriverAssignmentRepo.AddAsync(newAssignment);

                // Hợp đồng & Nội bộ
                bool isInternalDriver = await _unitOfWork.OwnerDriverLinkRepo.CheckLinkExistsAsync(trip.OwnerId, driverId, FleetJoinStatus.APPROVED);
                if (!isInternalDriver)
                {
                    await _tripDriverContractService.CreateContractInternalAsync(trip.TripId, trip.OwnerId, driverId, postDetail.PricePerPerson);
                }

                if (isMainDriver)
                {
                    trip.UpdateAt = TimeUtil.NowVN();
                    await _unitOfWork.TripRepo.UpdateAsync(trip);
                    await CreateRecordsForMainDriver(trip.TripId, driverId, trip.OwnerId);
                }

                // 6. UPDATE POST & TRIP STATUS
                postDetail.RequiredCount = (postDetail.RequiredCount > 0) ? postDetail.RequiredCount - 1 : 0;
                await _unitOfWork.PostTripDetailRepo.UpdateAsync(postDetail);

                int totalRemainingSlots = postTrip.PostTripDetails.Sum(d => d.RequiredCount);
                if (totalRemainingSlots <= 0)
                {
                    postTrip.Status = PostStatus.DONE;
                    postTrip.UpdateAt = TimeUtil.NowVN();
                    if (isInternalDriver)
                    {
                        trip.Status = TripStatus.DONE_ASSIGNING_DRIVER;
                        trip.UpdateAt = TimeUtil.NowVN();
                    }
                }
                else
                {
                    postTrip.UpdateAt = TimeUtil.NowVN();
                    if (postTrip.Status == PostStatus.DONE) postTrip.Status = PostStatus.OPEN;
                }

                await _unitOfWork.TripRepo.UpdateAsync(trip);
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
                assignment.OnBoardTime = TimeUtil.NowVN();

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
        // 1. TÀI XẾ CHECK-IN (LÊN XE)
        // =========================================================================
        //public async Task<ResponseDTO> DriverCheckInAsync(DriverCheckInDTO dto)
        //{
        //    try
        //    {
        //        var driverId = _userUtility.GetUserIdFromToken();

        //        // A. Lấy thông tin phân công
        //        var assignment = await _unitOfWork.TripDriverAssignmentRepo.GetAll()
        //            .Include(a => a.Trip)
        //            .FirstOrDefaultAsync(a => a.TripId == dto.TripId && a.DriverId == driverId);

        //        if (assignment == null) return new ResponseDTO("Bạn không có trong chuyến này.", 404, false);

        //        // B. Validate Trạng thái
        //        if (assignment.IsOnBoard) return new ResponseDTO("Bạn đã Check-in rồi.", 400, false);

        //        if (assignment.AssignmentStatus != AssignmentStatus.ACCEPTED)
        //            return new ResponseDTO("Bạn chưa được chấp nhận vào chuyến này.", 400, false);

        //        // C. XỬ LÝ KHOẢNG CÁCH & VALIDATE > 5KM
        //        string distanceNote = "";
        //        // double? finalDistance = null; // Mở comment nếu cần lưu số liệu

        //        if (assignment.StartLocation != null
        //            && assignment.StartLocation.Latitude.HasValue
        //            && assignment.StartLocation.Longitude.HasValue)
        //        {
        //            // Tính khoảng cách (Km)
        //            double distKm = CalculateDistance(
        //                assignment.StartLocation.Latitude.Value,
        //                assignment.StartLocation.Longitude.Value,
        //                dto.Latitude,
        //                dto.Longitude
        //            );

        //            // finalDistance = distKm; // Mở comment nếu cần lưu số liệu

        //            // 1. Validate CHẶN nếu > 5km
        //            if (distKm > 5.0)
        //            {
        //                return new ResponseDTO($"Vị trí của bạn quá xa điểm đón ({distKm:N2}km > 5km). Vui lòng di chuyển lại gần để Check-in.", 400, false);
        //            }

        //            // 2. Phân loại cảnh báo cho các trường hợp hợp lệ (<= 5km)
        //            distanceNote = distKm switch
        //            {
        //                <= 0.5 => "", // Dưới 500m -> Tốt
        //                <= 3.0 => $" | ⚠️ Cảnh báo: Lệch {distKm:N2}km", // 0.5 - 3km -> Cảnh báo nhẹ
        //                _ => $" | ⛔ Cảnh báo cao: Lệch {distKm:N2}km" // 3km - 5km -> Cảnh báo nặng (nhưng vẫn cho qua)
        //            };
        //        }

        //        // D. Upload Ảnh (Chỉ chạy khi đã qua validate khoảng cách để tiết kiệm tài nguyên)
        //        string imageUrl = await _fileService.UploadFileAsync(dto.EvidenceImage, driverId, FirebaseFileType.CHECKIN_CHECKOUT_IMAGES);
        //        if (string.IsNullOrEmpty(imageUrl)) return new ResponseDTO("Lỗi upload ảnh minh chứng.", 500, false);

        //        // E. Cập nhật DB
        //        assignment.IsOnBoard = true;
        //        assignment.OnBoardTime = DateTime.UtcNow;

        //        // Lưu vị trí kèm Note cảnh báo (nếu có)
        //        assignment.OnBoardLocation = $"{dto.Latitude},{dto.Longitude}|{dto.CurrentAddress}";
        //        assignment.OnBoardImage = imageUrl;
        //        assignment.CheckInNote = distanceNote;

        //        // assignment.CheckInDistanceDiff = finalDistance; // Nếu có cột lưu khoảng cách

        //        await _unitOfWork.TripDriverAssignmentRepo.UpdateAsync(assignment);
        //        await _unitOfWork.SaveChangeAsync();

        //        // F. Gửi thông báo (Optional)
        //        // await _notificationService.SendAsync(assignment.Trip.OwnerId, "TÀI XẾ ĐÃ LÊN XE",
        //        //     $"Tài xế check-in tại {dto.CurrentAddress}.{distanceNote}");

        //        return new ResponseDTO("Check-in thành công!", 200, true, new { imageUrl, warning = distanceNote });
        //    }
        //    catch (Exception ex)
        //    {
        //        return new ResponseDTO($"Lỗi: {ex.Message}", 500, false);
        //    }
        //}

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
                assignment.OffBoardTime = TimeUtil.NowVN();
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
                        driverWallet.LastUpdatedAt = TimeUtil.NowVN();

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
                            CreatedAt = TimeUtil.NowVN(),
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
                assignment.UpdateAt = TimeUtil.NowVN();

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

                        assignment.Trip.PostTrip.UpdateAt = TimeUtil.NowVN();
                        await _unitOfWork.PostTripRepo.UpdateAsync(assignment.Trip.PostTrip);
                    }
                }

                // 7. Nếu là Main Driver -> Clear thông tin Main Driver trong Trip (nếu cần thiết)
                // (Tuỳ logic của bạn, có thể không cần vì Trip chỉ link qua Assignment)

                // [MỚI] 7. CLEAN UP RECORDS
                await CleanupDriverRecordsAsync(assignment.TripId, assignment.DriverId);

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

                var timeRemaining = startTime - TimeUtil.NowVN();
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
                            driverWallet.LastUpdatedAt = TimeUtil.NowVN();

                            await _unitOfWork.TransactionRepo.AddAsync(new Transaction
                            {
                                TransactionId = Guid.NewGuid(),
                                WalletId = driverWallet.WalletId,
                                TripId = assignment.TripId,
                                Amount = refundAmount,
                                Type = TransactionType.REFUND,
                                Status = TransactionStatus.SUCCEEDED,
                                Description = $"Hoàn cọc hủy chuyến {assignment.Trip.TripCode}. ({penaltyReason})",
                                CreatedAt = TimeUtil.NowVN(),
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
                            ownerWallet.LastUpdatedAt = TimeUtil.NowVN();

                            await _unitOfWork.TransactionRepo.AddAsync(new Transaction
                            {
                                TransactionId = Guid.NewGuid(),
                                WalletId = ownerWallet.WalletId,
                                TripId = assignment.TripId,
                                Amount = penaltyAmount,
                                Type = TransactionType.COMPENSATION, // Dạng bồi thường
                                Status = TransactionStatus.SUCCEEDED,
                                Description = $"Nhận bồi thường do Tài xế hủy chuyến {assignment.Trip.TripCode}. ({penaltyReason})",
                                CreatedAt = TimeUtil.NowVN(),
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
                assignment.UpdateAt = TimeUtil.NowVN();
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

                        assignment.Trip.PostTrip.UpdateAt = TimeUtil.NowVN();
                        await _unitOfWork.PostTripRepo.UpdateAsync(assignment.Trip.PostTrip);
                    }
                }

                // [MỚI] 7. CLEAN UP RECORDS
                await CleanupDriverRecordsAsync(assignment.TripId, assignment.DriverId);

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

        // =========================================================================================================
        // PRIVATE HELPER: XÓA BIÊN BẢN KHI HỦY TÀI XẾ (CLEAN UP RECORDS)
        // =========================================================================================================
        private async Task CleanupDriverRecordsAsync(Guid tripId, Guid driverId)
        {
            // 1. Xóa Delivery Records (Biên bản Giao/Nhận hàng)
            var deliveryRecords = await _unitOfWork.TripDeliveryRecordRepo.GetAll()
                .Where(r => r.TripId == tripId  && r.Status != DeliveryRecordStatus.COMPLETED)
                .ToListAsync();

            if (deliveryRecords.Any())
            {
                // FIX: Dùng vòng lặp nếu Repo không có DeleteRange
                foreach (var record in deliveryRecords)
                {
                    // Kiểm tra hàm xóa của bạn tên là Remove hay Delete nhé. 
                    // Thường GenericRepo chuẩn sẽ là Remove(entity) hoặc Delete(entity)
                    await _unitOfWork.TripDeliveryRecordRepo.DeleteAsync(record.DeliveryRecordId);
                }
            }

            // 2. Xóa Vehicle Handover Records (Biên bản Giao/Nhận xe)
            var vehicleRecords = await _unitOfWork.TripVehicleHandoverRecordRepo.GetAll()
                .Where(r => r.TripId == tripId &&
                           (r.DriverId == driverId))
                .ToListAsync();

            if (vehicleRecords.Any())
            {
                // FIX: Dùng vòng lặp
                foreach (var record in vehicleRecords)
                {
                    await _unitOfWork.TripVehicleHandoverRecordRepo.DeleteAsync(record.DeliveryRecordId);
                }
            }
        }
    }
}
