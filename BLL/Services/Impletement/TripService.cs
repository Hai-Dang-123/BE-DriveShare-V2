using BLL.Services.Impletement;
using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.Enums.Type;
using DAL.Entities;
using DAL.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace BLL.Services.Implement
{
    public class TripService : ITripService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;
        private readonly IVietMapService _vietMapService;
        private readonly ITripRouteService _tripRouteService;
        private readonly ITripContactService _tripContactService;
        private readonly ITripProviderContractService _tripProviderContractService;
        private readonly ITripDeliveryRecordService _tripDeliveryRecordService;
        private readonly IEmailService _emailService;
        private readonly IServiceScopeFactory _serviceScopeFactory;


        public TripService(IUnitOfWork unitOfWork, UserUtility userUtility, IVietMapService vietMapService, ITripRouteService tripRouteService, ITripContactService tripContactService, ITripProviderContractService tripProviderContractService, ITripDeliveryRecordService tripDeliveryRecordService, IEmailService emailService, IServiceScopeFactory serviceScopeFactory)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
            _vietMapService = vietMapService;
            _tripRouteService = tripRouteService;
            _tripContactService = tripContactService;
            _tripProviderContractService = tripProviderContractService;
            _tripDeliveryRecordService = tripDeliveryRecordService;
            _emailService = emailService;
            _serviceScopeFactory = serviceScopeFactory;
        }

        //public async Task<ResponseDTO> CreateForOwnerAsync(TripCreateDTO dto)
        //{
        //    try
        //    {
        //        var ownerId = _userUtility.GetUserIdFromToken();
        //        if (ownerId == Guid.Empty)
        //            return new ResponseDTO("Unauthorized or invalid token", 401, false);

        //        var vehicle = await _unitOfWork.VehicleRepo.GetByIdAsync(dto.VehicleId);
        //        if (vehicle == null || vehicle.OwnerId != ownerId)
        //            return new ResponseDTO("Vehicle not found or not owned by current user", 403, false);

        //        var trip = new Trip
        //        {
        //            TripId = Guid.NewGuid(),
        //            TripCode = GenerateTripCode(),
        //            Status = TripStatus.CREATED,
        //            Type = TripType.OWNER_CREATE,
        //            CreateAt = DateTime.Now,
        //            UpdateAt = DateTime.Now,

        //            VehicleId = dto.VehicleId,
        //            OwnerId = ownerId,
        //            ShippingRouteId = dto.ShippingRouteId,
        //            TripRouteId = dto.TripRouteId,

        //            TotalFare = dto.TotalFare,
        //            ActualDistanceKm = dto.ActualDistanceKm,
        //            ActualDuration = dto.ActualDuration,
        //            ActualPickupTime = dto.ActualPickupTime,
        //            ActualCompletedTime = dto.ActualCompletedTime
        //        };

        //        await _unitOfWork.TripRepo.AddAsync(trip);
        //        await _unitOfWork.SaveChangeAsync();

        //        var result = new TripCreatedResultDTO
        //        {
        //            TripId = trip.TripId,
        //            TripCode = trip.TripCode,
        //            Status = trip.Status,
        //            Type = trip.Type
        //        };

        //        return new ResponseDTO("Trip created successfully", 200, true, result);
        //    }
        //    catch (Exception ex)
        //    {
        //        return new ResponseDTO($"Error while creating trip: {ex.Message}", 500, false);
        //    }
        //}


        //public async Task<ResponseDTO> CreateTripFromPostAsync(TripCreateFromPostDTO dto)
        //{
        //    // Bắt đầu Transaction
        //    await _unitOfWork.BeginTransactionAsync();
        //    try
        //    {
        //        // 1. VALIDATE OWNER (Lấy ID từ Token)
        //        var ownerId = _userUtility.GetUserIdFromToken();
        //        if (ownerId == Guid.Empty)
        //            throw new Exception("Unauthorized or invalid token");

        //        // 2. VALIDATE POST PACKAGE (Lấy hết dữ liệu liên quan)
        //        var postPackage = await _unitOfWork.PostPackageRepo.FirstOrDefaultAsync(
        //            p => p.PostPackageId == dto.PostPackageId,
        //            // Include tất cả mọi thứ chúng ta cần
        //            includeProperties: "ShippingRoute,PostContacts,Provider,Packages"
        //        );
        //        if (postPackage == null)
        //            throw new Exception("Không tìm thấy Bài đăng (PostPackage).");
        //        if (postPackage.Status != PostStatus.OPEN)
        //            throw new Exception("Bài đăng này đã đóng hoặc đã được nhận.");
        //        if (postPackage.ShippingRoute == null)
        //            throw new Exception("Bài đăng thiếu thông tin Lộ trình (ShippingRoute).");
        //        if (postPackage.Provider == null)
        //            throw new Exception("Bài đăng thiếu thông tin Nhà cung cấp (Provider).");

        //        // 3. VALIDATE VEHICLE (Kiểm tra sở hữu và lấy VehicleType)
        //        var vehicle = await _unitOfWork.VehicleRepo.FirstOrDefaultAsync(
        //            v => v.VehicleId == dto.VehicleId && v.OwnerId == ownerId,
        //            includeProperties: "VehicleType"
        //        );
        //        if (vehicle == null)
        //            throw new Exception("Xe (Vehicle) không tìm thấy hoặc không thuộc về bạn.");

        //        // 4. GỌI SERVICE 1: TẠO TRIPROUTE
        //        // (Service này gọi VietMap và AddAsync)
        //        var newTripRoute = await _tripRouteService.CreateAndAddTripRouteAsync(
        //            postPackage.ShippingRoute, vehicle
        //        );

        //        // 5. TẠO TRIP (Entity chính)
        //        var trip = new Trip
        //        {
        //            TripId = Guid.NewGuid(),
        //            TripCode = GenerateTripCode(),
        //            Status = TripStatus.CREATED,
        //            Type = TripType.FROM_PROVIDER, // (Type mới)
        //            CreateAt = DateTime.UtcNow,
        //            UpdateAt = DateTime.UtcNow,
        //            VehicleId = dto.VehicleId,
        //            OwnerId = ownerId,
        //            TripRouteId = newTripRoute.TripRouteId, // Tuyến đường thực tế
        //            ShippingRouteId = postPackage.ShippingRoute.ShippingRouteId,
        //            TotalFare = postPackage.OfferedPrice, // Lấy giá từ bài đăng
        //            ActualDistanceKm = newTripRoute.DistanceKm,
        //            ActualDuration = newTripRoute.Duration,
        //            ActualPickupTime = null,
        //            ActualCompletedTime = null
        //        };
        //        await _unitOfWork.TripRepo.AddAsync(trip);

        //        // 6. GỌI SERVICE 2: TẠO CONTRACT
        //        // (Service này AddAsync)
        //        await _tripProviderContractService.CreateAndAddContractAsync(
        //            trip.TripId, ownerId, postPackage.ProviderId, postPackage.OfferedPrice
        //        );

        //        // 7. GỌI SERVICE 3: SAO CHÉP CONTACTS
        //        // (Service này AddAsync - logic "lấy từ post contact")
        //        await _tripContactService.CopyContactsFromPostAsync(
        //            trip.TripId, postPackage.PostContacts
        //        );

        //        // 8. CẬP NHẬT TRẠNG THÁI (PostPackage và Packages)
        //        postPackage.Status = PostStatus.DONE; // Đánh dấu bài đăng là "Đã nhận"
        //        await _unitOfWork.PostPackageRepo.UpdateAsync(postPackage);

        //        // Gán TripId và cập nhật trạng thái cho tất cả Package trong bài đăng
        //        foreach (var pkg in postPackage.Packages)
        //        {
        //            pkg.TripId = trip.TripId;
        //            pkg.OwnerId = ownerId; // Owner nhận gói hàng này
        //            pkg.Status = PackageStatus.IN_PROGRESS;
        //            await _unitOfWork.PackageRepo.UpdateAsync(pkg);
        //        }

        //        // 9. LƯU TẤT CẢ (COMMIT)
        //        await _unitOfWork.CommitTransactionAsync();

        //        // 10. Trả về kết quả
        //        var result = new TripCreatedResultDTO
        //        {
        //            TripId = trip.TripId,
        //            TripCode = trip.TripCode,
        //            Status = trip.Status
        //        };
        //        return new ResponseDTO("Nhận chuyến và tạo Trip thành công!", 201, true, result);
        //    }
        //    catch (Exception ex)
        //    {
        //        await _unitOfWork.RollbackTransactionAsync();
        //        return new ResponseDTO($"Lỗi khi nhận chuyến: {ex.Message}", 400, false);
        //    }
        //}

        public async Task<ResponseDTO> CreateTripFromPostAsync(TripCreateFromPostDTO dto)
        {
            // Bắt đầu Transaction
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // 1. VALIDATE OWNER
                var ownerId = _userUtility.GetUserIdFromToken();
                if (ownerId == Guid.Empty)
                    throw new Exception("Unauthorized or invalid token");

                // 2. VALIDATE POST PACKAGE
                var postPackage = await _unitOfWork.PostPackageRepo.FirstOrDefaultAsync(
                    p => p.PostPackageId == dto.PostPackageId,
                    includeProperties: "ShippingRoute,PostContacts,Provider,Packages"
                );

                if (postPackage == null) throw new Exception("Không tìm thấy Bài đăng (PostPackage).");
                if (postPackage.Status != PostStatus.OPEN) throw new Exception("Bài đăng này đã đóng hoặc đã được nhận.");
                if (postPackage.ShippingRoute == null) throw new Exception("Bài đăng thiếu thông tin Lộ trình.");
                if (postPackage.Provider == null) throw new Exception("Bài đăng thiếu thông tin Nhà cung cấp.");

                // 3. VALIDATE VEHICLE
                var vehicle = await _unitOfWork.VehicleRepo.FirstOrDefaultAsync(
                    v => v.VehicleId == dto.VehicleId && v.OwnerId == ownerId,
                    includeProperties: "VehicleType"
                );
                if (vehicle == null) throw new Exception("Xe (Vehicle) không tìm thấy hoặc không thuộc về bạn.");

                // =======================================================================
                // 🛑 3.1 (FIXED) VALIDATE VEHICLE SCHEDULE (KIỂM TRA TRÙNG LỊCH)
                // =======================================================================

                var route = postPackage.ShippingRoute;

                // A. Hợp nhất Ngày + Giờ để tạo mốc thời gian chính xác cho CHUYẾN MỚI
                // Lưu ý: DateTime không bao giờ null, nên bỏ check HasValue. 
                // Chỉ check TimeWindow có null không thôi.

                TimeSpan startTimeSpan = TimeSpan.Zero;
                if (route.PickupTimeWindow != null && route.PickupTimeWindow.StartTime != null)
                {
                    startTimeSpan = route.PickupTimeWindow.StartTime.Value.ToTimeSpan();
                }

                TimeSpan endTimeSpan = new TimeSpan(23, 59, 59); // Mặc định cuối ngày nếu không có giờ
                if (route.DeliveryTimeWindow != null && route.DeliveryTimeWindow.EndTime != null)
                {
                    endTimeSpan = route.DeliveryTimeWindow.EndTime.Value.ToTimeSpan();
                }

                // Mốc thời gian đầy đủ (Ngày + Giờ)
                var newStartFull = route.ExpectedPickupDate.Date.Add(startTimeSpan);
                var newEndFull = route.ExpectedDeliveryDate.Date.Add(endTimeSpan);

                if (newStartFull >= newEndFull)
                {
                    throw new Exception("Thời gian Lấy hàng phải nhỏ hơn thời gian Giao hàng.");
                }

                // B. Lấy danh sách các chuyến ĐANG HOẠT ĐỘNG của xe này về RAM
                // (Tránh lỗi SQL không dịch được phép cộng ngày giờ phức tạp)
                var activeTrips = await _unitOfWork.TripRepo.GetAll()
                    .Include(t => t.ShippingRoute)
                    .Where(t => t.VehicleId == dto.VehicleId &&
                                t.Status != TripStatus.COMPLETED &&
                                t.Status != TripStatus.CANCELLED &&
                                t.Status != TripStatus.DELETED)
                    .ToListAsync(); // <--- Tải về RAM ở đây

                // C. Duyệt danh sách để kiểm tra trùng lặp 


                foreach (var existingTrip in activeTrips)
                {
                    var exRoute = existingTrip.ShippingRoute;
                    if (exRoute == null) continue;

                    // Tính toán thời gian cho chuyến cũ (Tương tự như trên)
                    TimeSpan exStartSpan = TimeSpan.Zero;
                    if (exRoute.PickupTimeWindow != null && exRoute.PickupTimeWindow.StartTime != null)
                        exStartSpan = exRoute.PickupTimeWindow.StartTime.Value.ToTimeSpan();

                    TimeSpan exEndSpan = new TimeSpan(23, 59, 59);
                    if (exRoute.DeliveryTimeWindow != null && exRoute.DeliveryTimeWindow.EndTime != null)
                        exEndSpan = exRoute.DeliveryTimeWindow.EndTime.Value.ToTimeSpan();

                    var exStartFull = exRoute.ExpectedPickupDate.Date.Add(exStartSpan);
                    var exEndFull = exRoute.ExpectedDeliveryDate.Date.Add(exEndSpan);

                    // LOGIC OVERLAP: (Chuyến cũ bắt đầu TRƯỚC KHI chuyến mới kết thúc) VÀ (Chuyến cũ kết thúc SAU KHI chuyến mới bắt đầu)
                    if (exStartFull < newEndFull && exEndFull > newStartFull)
                    {
                        throw new Exception($"Xe {vehicle.PlateNumber} bị trùng lịch với chuyến {existingTrip.TripCode} " +
                                            $"({exStartFull:dd/MM HH:mm} - {exEndFull:dd/MM HH:mm}).");
                    }
                }

                // =======================================================================

                // 4. GỌI SERVICE 1: TẠO TRIPROUTE
                var newTripRoute = await _tripRouteService.CreateAndAddTripRouteAsync(
                    postPackage.ShippingRoute, vehicle
                );

                // 5. TẠO TRIP
                var trip = new Trip
                {
                    TripId = Guid.NewGuid(),
                    TripCode = GenerateTripCode(),
                    Status = TripStatus.AWAITING_OWNER_CONTRACT,
                    Type = TripType.FROM_PROVIDER,
                    CreateAt = DateTime.UtcNow,
                    UpdateAt = DateTime.UtcNow,
                    VehicleId = dto.VehicleId,
                    OwnerId = ownerId,
                    TripRouteId = newTripRoute.TripRouteId,
                    ShippingRouteId = postPackage.ShippingRoute.ShippingRouteId,
                    TotalFare = postPackage.OfferedPrice,
                    ActualDistanceKm = newTripRoute.DistanceKm,
                    ActualDuration = newTripRoute.Duration,
                    ActualPickupTime = null,
                    ActualCompletedTime = null
                };
                await _unitOfWork.TripRepo.AddAsync(trip);

                // 6. GỌI SERVICE 2: TẠO CONTRACT
                await _tripProviderContractService.CreateAndAddContractAsync(
                    trip.TripId, ownerId, postPackage.ProviderId, postPackage.OfferedPrice
                );

                // 7. GỌI SERVICE 3: SAO CHÉP CONTACTS
                await _tripContactService.CopyContactsFromPostAsync(
                    trip.TripId, postPackage.PostContacts
                );

                // 8. CẬP NHẬT TRẠNG THÁI
                postPackage.Status = PostStatus.DONE;
                await _unitOfWork.PostPackageRepo.UpdateAsync(postPackage);

                foreach (var pkg in postPackage.Packages)
                {
                    pkg.TripId = trip.TripId;
                    pkg.OwnerId = ownerId;
                    pkg.Status = PackageStatus.IN_PROGRESS;
                    await _unitOfWork.PackageRepo.UpdateAsync(pkg);
                }

                // 9. COMMIT
                await _unitOfWork.CommitTransactionAsync();

                // 10. RETURN
                var result = new TripCreatedResultDTO
                {
                    TripId = trip.TripId,
                    TripCode = trip.TripCode,
                    Status = trip.Status
                };
                return new ResponseDTO("Nhận chuyến và tạo Trip thành công!", 201, true, result);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return new ResponseDTO($"Lỗi khi nhận chuyến: {ex.Message}", 400, false);
            }
        }

        private string GenerateTripCode()
        {
            return $"TRIP-{Guid.NewGuid().ToString("N").ToUpper().Substring(0, 8)}";
        }
        private string GenerateContractCode()
        {
            return $"CON-PROV-{DateTime.UtcNow:yyyyMMddHHmmss}";
        }

        public async Task<ResponseDTO> ChangeTripStatusAsync(ChangeTripStatusDTO dto)
        {
            // 1. Bắt đầu Transaction lớn
            await _unitOfWork.BeginTransactionAsync();

            // Khai báo biến lưu kết quả tài chính để dùng cho email sau khi commit
            decimal ownerReceived = 0;
            decimal providerPaid = 0;

            // Lưu danh sách tài xế đã được trả tiền để gửi mail {DriverId, Amount}
            var paidDriversMap = new Dictionary<Guid, decimal>();

            try
            {
                // 2. Lấy Trip
                var trip = await _unitOfWork.TripRepo.GetByIdAsync(dto.TripId);
                if (trip == null)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    return new ResponseDTO("Trip not found.", 404, false);
                }

                // Lưu lại tổng tiền Provider đã trả
                providerPaid = trip.TotalFare;

                // 3. Cập nhật trạng thái cơ bản
                trip.Status = dto.NewStatus;
                trip.UpdateAt = DateTime.UtcNow;

                if (dto.NewStatus == TripStatus.LOADING) trip.ActualPickupTime = DateTime.UtcNow;
                if (dto.NewStatus == TripStatus.COMPLETED) trip.ActualCompletedTime = DateTime.UtcNow;

                await _unitOfWork.TripRepo.UpdateAsync(trip);
                await _unitOfWork.SaveChangeAsync();

                // =================================================================
                // 🚀 A. LOGIC GỬI EMAIL LINK KÝ TÊN (LOADING / UNLOADING)
                // =================================================================
                if (dto.NewStatus == TripStatus.LOADING)
                {
                    var pickupRecord = await _unitOfWork.TripDeliveryRecordRepo.GetAll()
                        .FirstOrDefaultAsync(r => r.TripId == trip.TripId && r.Type == DeliveryRecordType.PICKUP);

                    if (pickupRecord != null && pickupRecord.ContactSigned != true)
                    {
                        await _tripDeliveryRecordService.SendAccessLinkToContactAsync(pickupRecord.DeliveryRecordId);
                    }
                }
                else if (dto.NewStatus == TripStatus.UNLOADING)
                {
                    var dropoffRecord = await _unitOfWork.TripDeliveryRecordRepo.GetAll()
                        .FirstOrDefaultAsync(r => r.TripId == trip.TripId && r.Type == DeliveryRecordType.DROPOFF);

                    if (dropoffRecord != null && dropoffRecord.ContactSigned != true)
                    {
                        await _tripDeliveryRecordService.SendAccessLinkToContactAsync(dropoffRecord.DeliveryRecordId);
                    }
                }

                // =================================================================
                // 💰 B. LOGIC THANH TOÁN KHI HOÀN THÀNH (COMPLETED)
                // =================================================================
                if (dto.NewStatus == TripStatus.COMPLETED)
                {
                    // --- 1. TRẢ TIỀN CHO OWNER ---
                    try
                    {
                        var ownerWallet = await _unitOfWork.WalletRepo.FirstOrDefaultAsync(w => w.UserId == trip.OwnerId);
                        if (ownerWallet == null) throw new Exception("Owner wallet not found");
                        if (ownerWallet.Status != WalletStatus.ACTIVE) throw new Exception("Owner wallet is locked");

                        decimal platformFee = trip.TotalFare * 0.1m; // Phí sàn 10%
                        ownerReceived = trip.TotalFare - platformFee; // Tiền thực nhận

                        ownerWallet.Balance += ownerReceived;
                        ownerWallet.LastUpdatedAt = DateTime.UtcNow;
                        await _unitOfWork.WalletRepo.UpdateAsync(ownerWallet);

                        var ownerTx = new Transaction
                        {
                            TransactionId = Guid.NewGuid(),
                            WalletId = ownerWallet.WalletId,
                            TripId = trip.TripId,
                            Amount = ownerReceived,
                            Type = TransactionType.OWNER_PAYOUT,
                            Status = TransactionStatus.SUCCEEDED,
                            Description = $"Nhận thanh toán hoàn thành chuyến {trip.TripCode} (đã trừ phí sàn)",
                            BalanceBefore = ownerWallet.Balance - ownerReceived,
                            BalanceAfter = ownerWallet.Balance,
                            CreatedAt = DateTime.UtcNow
                        };
                        await _unitOfWork.TransactionRepo.AddAsync(ownerTx);
                    }
                    catch (Exception ex)
                    {
                        // Xử lý lỗi Owner -> Rollback trạng thái Trip
                        trip.Status = TripStatus.AWAITING_FINAL_PROVIDER_PAYOUT;
                        await _unitOfWork.TripRepo.UpdateAsync(trip);
                        await _unitOfWork.CommitTransactionAsync();
                        return new ResponseDTO($"Lỗi thanh toán Owner: {ex.Message}", 500, false);
                    }

                    // --- 2. TRẢ TIỀN CHO CÁC DRIVER (CHÍNH + PHỤ) ---
                    try
                    {
                        // Lấy tất cả tài xế đã nhận việc nhưng chưa được trả tiền
                        var assignments = await _unitOfWork.TripDriverAssignmentRepo.GetAll()
                            .Where(a => a.TripId == trip.TripId
                                     && a.AssignmentStatus == AssignmentStatus.ACCEPTED
                                     && a.PaymentStatus != DriverPaymentStatus.PAID)
                            .ToListAsync();

                        foreach (var assign in assignments)
                        {
                            var driverWallet = await _unitOfWork.WalletRepo.FirstOrDefaultAsync(w => w.UserId == assign.DriverId);

                            // Nếu driver không có ví -> Skip hoặc log, không chặn luồng chung
                            if (driverWallet != null && driverWallet.Status == WalletStatus.ACTIVE)
                            {
                                decimal driverPayout = assign.TotalAmount;

                                driverWallet.Balance += driverPayout;
                                driverWallet.LastUpdatedAt = DateTime.UtcNow;
                                await _unitOfWork.WalletRepo.UpdateAsync(driverWallet);

                                assign.PaymentStatus = DriverPaymentStatus.PAID;
                                assign.UpdateAt = DateTime.UtcNow;
                                await _unitOfWork.TripDriverAssignmentRepo.UpdateAsync(assign);

                                var driverTx = new Transaction
                                {
                                    TransactionId = Guid.NewGuid(),
                                    WalletId = driverWallet.WalletId,
                                    TripId = trip.TripId,
                                    Amount = driverPayout,
                                    Type = TransactionType.DRIVER_PAYOUT,
                                    Status = TransactionStatus.SUCCEEDED,
                                    Description = $"Nhận lương chuyến {trip.TripCode} ({assign.Type})",
                                    BalanceBefore = driverWallet.Balance - driverPayout,
                                    BalanceAfter = driverWallet.Balance,
                                    CreatedAt = DateTime.UtcNow
                                };
                                await _unitOfWork.TransactionRepo.AddAsync(driverTx);

                                // Lưu lại để gửi mail
                                if (!paidDriversMap.ContainsKey(assign.DriverId))
                                {
                                    paidDriversMap.Add(assign.DriverId, driverPayout);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Xử lý lỗi Driver -> Owner đã nhận tiền, Driver chưa nhận -> Treo trạng thái
                        trip.Status = TripStatus.AWAITING_FINAL_DRIVER_PAYOUT;
                        await _unitOfWork.TripRepo.UpdateAsync(trip);
                        await _unitOfWork.CommitTransactionAsync();
                        return new ResponseDTO($"Lỗi thanh toán Driver: {ex.Message}", 500, false);
                    }
                }

                // 4. COMMIT THÀNH CÔNG
                await _unitOfWork.CommitTransactionAsync();

                // =================================================================
                // 📩 C. GỬI EMAIL BÁO CÁO HOÀN THÀNH (Background Task)
                // =================================================================
                if (dto.NewStatus == TripStatus.COMPLETED)
                {
                    // Capture local variables for Thread Safety
                    var tripIdLocal = trip.TripId;
                    var ownerIdLocal = trip.OwnerId;
                    var providerPaidLocal = providerPaid;
                    var ownerReceivedLocal = ownerReceived;
                    var paidDriversLocal = new Dictionary<Guid, decimal>(paidDriversMap); // Clone map

                    _ = Task.Run(async () =>
                    {
                        // ⚠️ TẠO SCOPE MỚI ĐỂ TRÁNH LỖI DISPOSED CONTEXT
                        using (var scope = _serviceScopeFactory.CreateScope())
                        {
                            var scopedUow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                            var scopedEmailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                            try
                            {
                                // Query Trip Full Info trong scope mới
                                var tripFull = await scopedUow.TripRepo.GetAll()
                                    .Include(t => t.ShippingRoute)
                                    .Include(t => t.Vehicle).ThenInclude(v => v.VehicleType)
                                    .Include(t => t.Packages)
                                    .Include(t => t.TripProviderContract)
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync(t => t.TripId == tripIdLocal);

                                if (tripFull == null) return;

                                // Safe access location string
                                var startAddr = tripFull.ShippingRoute?.StartLocation?.Address ?? "N/A";
                                var endAddr = tripFull.ShippingRoute?.EndLocation?.Address ?? "N/A";

                                // Model cơ bản
                                var commonData = new TripCompletionReportModel
                                {
                                    TripCode = tripFull.TripCode,
                                    CompletedAt = DateTime.UtcNow.AddHours(7).ToString("HH:mm dd/MM/yyyy"),
                                    StartAddress = startAddr,
                                    EndAddress = endAddr,
                                    DistanceKm = (double)tripFull.ActualDistanceKm,
                                    VehiclePlate = tripFull.Vehicle?.PlateNumber ?? "N/A",
                                    VehicleType = tripFull.Vehicle?.VehicleType?.VehicleTypeName ?? "N/A",
                                    PackageCount = tripFull.Packages?.Count ?? 0,
                                    TotalPayload = tripFull.Packages?.Sum(p => p.WeightKg) ?? 0
                                };

                                // 1. Gửi cho Provider
                                if (tripFull.TripProviderContract != null)
                                {
                                    var provider = await scopedUow.BaseUserRepo.GetByIdAsync(tripFull.TripProviderContract.CounterpartyId);
                                    if (provider != null)
                                    {
                                        var pReport = commonData.Clone();
                                        pReport.RecipientName = provider.FullName;
                                        pReport.Role = "Provider";
                                        pReport.IsIncome = false;
                                        pReport.Amount = providerPaidLocal;
                                        pReport.FinancialDescription = "TỔNG CHI PHÍ VẬN CHUYỂN";

                                        await scopedEmailService.SendTripCompletionEmailAsync(provider.Email, pReport);
                                    }
                                }

                                // 2. Gửi cho Owner
                                var owner = await scopedUow.BaseUserRepo.GetByIdAsync(ownerIdLocal);
                                if (owner != null)
                                {
                                    var oReport = commonData.Clone();
                                    oReport.RecipientName = owner.FullName;
                                    oReport.Role = "Owner";
                                    oReport.IsIncome = true;
                                    oReport.Amount = ownerReceivedLocal;
                                    oReport.FinancialDescription = "DOANH THU THỰC NHẬN (SAU PHÍ)";

                                    await scopedEmailService.SendTripCompletionEmailAsync(owner.Email, oReport);
                                }

                                // 3. Gửi cho TẤT CẢ Driver đã được trả tiền
                                foreach (var pd in paidDriversLocal)
                                {
                                    var driver = await scopedUow.BaseUserRepo.GetByIdAsync(pd.Key);
                                    if (driver != null)
                                    {
                                        var dReport = commonData.Clone();
                                        dReport.RecipientName = driver.FullName;
                                        dReport.Role = "Driver";
                                        dReport.IsIncome = true;
                                        dReport.Amount = pd.Value; // Số tiền cụ thể của driver này
                                        dReport.FinancialDescription = "TIỀN CÔNG / LƯƠNG CHUYẾN";

                                        await scopedEmailService.SendTripCompletionEmailAsync(driver.Email, dReport);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error sending completion emails: {ex.Message}");
                            }
                        } // End Scope
                    });
                }

                return new ResponseDTO($"Trip status changed to {dto.NewStatus} successfully.", 200, true);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return new ResponseDTO($"Error changing trip status: {ex.Message}", 500, false);
            }
        }

        public class TripCompletionReportModel
        {
            public string TripCode { get; set; }
            public string CompletedAt { get; set; }

            // Thông tin lộ trình
            public string StartAddress { get; set; }
            public string EndAddress { get; set; }
            public double DistanceKm { get; set; }

            // Thông tin xe & Hàng
            public string VehiclePlate { get; set; }
            public string VehicleType { get; set; }
            public int PackageCount { get; set; }
            public decimal TotalPayload { get; set; }

            // Tài chính (Sẽ hiển thị khác nhau tùy Role)
            public decimal Amount { get; set; } // Số tiền
            public bool IsIncome { get; set; } // True: Tiền vào (Xanh), False: Tiền ra (Đỏ)
            public string FinancialDescription { get; set; } // Ví dụ: "Doanh thu thuần", "Chi phí vận chuyển"

            // Thông tin người nhận
            public string RecipientName { get; set; }
            public string Role { get; set; } // "Provider", "Owner", "Driver"

            // ✅ THÊM HÀM NÀY ĐỂ SỬA LỖI PROTECTED MEMBERWISECLONE
            public TripCompletionReportModel Clone()
            {
                return (TripCompletionReportModel)this.MemberwiseClone();
            }
        }
        /*
LƯU Ý: Đây chỉ là MỘT HÀM.
Bạn hãy chép nội dung hàm này và dán THAY THẾ
cho hàm IsValidTransition CŨ trong file TripService.cs của bạn.
*/

        private bool IsValidTransition(TripStatus current, TripStatus next)
        {
            // 1. Không thể chuyển trạng thái TỪ các trạng thái cuối cùng
            if (current == TripStatus.COMPLETED || current == TripStatus.CANCELLED || current == TripStatus.DELETED)
            {
                return false;
            }

            // 2. Có thể Hủy (CANCELLED) từ bất kỳ đâu (trừ các trạng thái cuối)
            if (next == TripStatus.CANCELLED)
            {
                return true;
            }

            // 3. Có thể Xóa (DELETED) từ bất kỳ đâu (trừ các trạng thái cuối)
            if (next == TripStatus.DELETED)
            {
                return true;
            }

            // 4. Xử lý luồng tuyến tính (Linear Flow)
            return next switch
            {
                // Giai đoạn 1 (HĐ Provider)
                // (CREATED -> AWAITING_PROVIDER_CONTRACT xảy ra trong CreateTripFromPostAsync)
                // (AWAITING_PROVIDER_CONTRACT -> AWAITING_PROVIDER_PAYMENT xảy ra trong SignAsync của ContractService)
                TripStatus.AWAITING_PROVIDER_PAYMENT => current == TripStatus.AWAITING_PROVIDER_CONTRACT,

                // Giai đoạn 2 (Tìm Driver)
                TripStatus.PENDING_DRIVER_ASSIGNMENT => current == TripStatus.AWAITING_PROVIDER_PAYMENT,
                TripStatus.AWAITING_DRIVER_CONTRACT => current == TripStatus.PENDING_DRIVER_ASSIGNMENT,

                // ⚠️ SỬA ĐỔI 1: Luồng thanh toán cọc cho Driver (Logic mới)
                // (Sau khi ký HĐ Driver -> Chuyển sang đợi Owner trả tiền)
                TripStatus.AWAITING_OWNER_PAYMENT => current == TripStatus.AWAITING_DRIVER_CONTRACT,

                // Giai đoạn 3 (Chuẩn bị)
                // ⚠️ SỬA ĐỔI 2: Cập nhật luồng vào Giai đoạn 3
                TripStatus.READY_FOR_VEHICLE_HANDOVER =>
                                            current == TripStatus.AWAITING_OWNER_PAYMENT || // 1. Lái xe thuê (đã trả cọc Escrow)
                                            current == TripStatus.PENDING_DRIVER_ASSIGNMENT, // 2. Lái xe nội bộ (đã gán, không cần cọc)

                TripStatus.VEHICLE_HANDOVER => current == TripStatus.READY_FOR_VEHICLE_HANDOVER,
                TripStatus.LOADING => current == TripStatus.VEHICLE_HANDOVER,

                // Giai đoạn 4 (Vận hành)
                //TripStatus.IN_TRANSIT => current == TripStatus.LOADING,
                //TripStatus.UNLOADING => current == TripStatus.IN_TRANSIT,
                TripStatus.DELIVERED => current == TripStatus.UNLOADING,

                // Giai đoạn 5 (Trả xe)
                TripStatus.RETURNING_VEHICLE => current == TripStatus.DELIVERED,
                TripStatus.VEHICLE_RETURNED => current == TripStatus.RETURNING_VEHICLE,

                // Giai đoạn 6 (Thanh toán)
                TripStatus.AWAITING_FINAL_PROVIDER_PAYOUT => current == TripStatus.VEHICLE_RETURNED,
                TripStatus.AWAITING_FINAL_DRIVER_PAYOUT => current == TripStatus.AWAITING_FINAL_PROVIDER_PAYOUT,
                TripStatus.COMPLETED => current == TripStatus.AWAITING_FINAL_DRIVER_PAYOUT,

                // Default case
                _ => false
            };
        }
        public async Task<ResponseDTO> GetAllTripsByOwnerAsync(int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                // 🔹 1. Lấy ID và Role từ Token
                var ownerId = _userUtility.GetUserIdFromToken();
                var userRole = _userUtility.GetUserRoleFromToken(); // Giả sử hàm này tồn tại

                // 🔹 2. Kiểm tra quyền
                if (userRole != "Owner")
                    return new ResponseDTO("Forbidden: Chỉ 'Owner' mới có thể truy cập.", 403, false);
                if (ownerId == Guid.Empty)
                    return new ResponseDTO("Unauthorized: Token không hợp lệ.", 401, false);

                // 🔹 3. Lấy danh sách Trip (Vẫn lấy tất cả vì Repo không hỗ trợ Paging)
                var trips = await _unitOfWork.TripRepo.GetAllAsync(
          filter: t => t.OwnerId == ownerId && t.Status != Common.Enums.Status.TripStatus.DELETED,
                    // Owner lấy tất cả contracts
                    includeProperties: "Vehicle,Vehicle.VehicleType,Owner,Packages,ShippingRoute,ShippingRoute.StartLocation,ShippingRoute.EndLocation,TripRoute,DriverAssignments.Driver,DriverContracts,TripProviderContract"
        );

                if (trips == null || !trips.Any())
                    return new ResponseDTO("No trips found for this owner.", 404, false);

                // 🔹 4. Phân trang (In-Memory Paging - Cần cải thiện ở Repository)
                var totalCount = trips.Count();
                var pagedTrips = trips
                  .OrderByDescending(t => t.CreateAt) // Sắp xếp để phân trang ổn định
                            .Skip((pageNumber - 1) * pageSize)
                  .Take(pageSize)
                  .ToList();

                // 🔹 5. Map sang DTO (Thêm kiểm tra null)
                var mappedData = pagedTrips.Select(t => new TripDetailDTO
                {
                    TripId = t.TripId,
                    TripCode = t.TripCode,
                    Status = t.Status.ToString(),
                    CreateAt = t.CreateAt,
                    UpdateAt = t.UpdateAt,

                    VehicleId = t.VehicleId,
                    VehicleModel = t.Vehicle?.Model ?? "N/A",
                    VehiclePlate = t.Vehicle?.PlateNumber ?? "N/A",
                    VehicleType = t.Vehicle?.VehicleType?.VehicleTypeName ?? "N/A",

                    OwnerId = t.OwnerId,
                    OwnerName = t.Owner?.FullName ?? "N/A",
                    OwnerCompany = t.Owner?.CompanyName ?? "N/A",

                    StartAddress = t.ShippingRoute?.StartLocation?.Address ?? string.Empty,
                    EndAddress = t.ShippingRoute?.EndLocation?.Address ?? string.Empty,

                    EstimatedDuration = (t.ShippingRoute != null &&
          t.ShippingRoute.ExpectedDeliveryDate > t.ShippingRoute.ExpectedPickupDate)
          ? t.ShippingRoute.ExpectedDeliveryDate - t.ShippingRoute.ExpectedPickupDate
          : TimeSpan.Zero,

                    PackageCodes = t.Packages.Select(p => p.PackageCode).ToList(),
                    DriverNames = t.DriverAssignments.Select(a => a.Driver?.FullName ?? "N/A").ToList(),
                    TripRouteSummary = t.TripRoute != null
                 ? $"Distance: {t.TripRoute.DistanceKm} km, Duration: {t.TripRoute.Duration.TotalMinutes:F0} minutes"
  : string.Empty,

                    // (DTO này không có Contracts, giữ nguyên)

                }).ToList();

                // 🔹 6. Trả về kết quả phân trang
                var paginatedResult = new PaginatedDTO<TripDetailDTO>(
          mappedData,
          totalCount,
          pageNumber,
          pageSize
        );

                return new ResponseDTO("Get trips successfully", 200, true, paginatedResult);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error getting trips: {ex.Message}", 500, false);
            }
        }
        public async Task<ResponseDTO> GetAllTripsByDriverAsync(int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                // 🔹 1. Lấy ID và Role từ Token
                var driverId = _userUtility.GetUserIdFromToken();
                var userRole = _userUtility.GetUserRoleFromToken(); // Giả sử hàm này tồn tại

                // 🔹 2. Kiểm tra quyền
                if (userRole != "Driver")
                    return new ResponseDTO("Forbidden: Chỉ 'Driver' mới có thể truy cập.", 403, false);
                if (driverId == Guid.Empty)
                    return new ResponseDTO("Unauthorized: Token không hợp lệ.", 401, false);

                // 🔹 3. Lấy tất cả các TripDriverAssignment của tài xế này
                var assignments = await _unitOfWork.TripDriverAssignmentRepo.GetAllAsync(
          filter: a => a.DriverId == driverId
               //&& a.AssignmentStatus != AssignmentStatus.REJECTED
               && a.Trip.Status != TripStatus.DELETED,
                    // Chỉ include những gì Driver cần (BỎ ProviderContracts)
                    includeProperties: "Trip,Trip.Vehicle,Trip.Vehicle.VehicleType,Trip.Owner,Trip.ShippingRoute,Trip.ShippingRoute.StartLocation,Trip.ShippingRoute.EndLocation,Trip.TripRoute,Trip.Packages,Trip.DriverAssignments.Driver,Trip.DriverContracts"
        );

                if (assignments == null || !assignments.Any())
                    return new ResponseDTO("No trips found for this driver.", 404, false);

                // 🔹 4. Lấy danh sách các trip duy nhất và phân trang (In-Memory)
                var trips = assignments.Select(a => a.Trip).Distinct();
                var totalCount = trips.Count();
                var pagedTrips = trips
                  .OrderByDescending(t => t.CreateAt)
                  .Skip((pageNumber - 1) * pageSize)
                  .Take(pageSize)
                  .ToList();

                // 🔹 5. Map dữ liệu sang DTO (Thêm kiểm tra null)
                var mappedData = pagedTrips.Select(t =>
                {
                    var currentAssign = t.DriverAssignments.FirstOrDefault(a => a.DriverId == driverId);

                    return new DriverTripDetailDTO
                    {
                        TripId = t.TripId,
                        TripCode = t.TripCode,
                        Status = t.Status.ToString(),
                        CreateAt = t.CreateAt,
                        UpdateAt = t.UpdateAt,
                        VehicleId = t.VehicleId,
                        VehicleModel = t.Vehicle?.Model ?? "N/A",
                        VehiclePlate = t.Vehicle?.PlateNumber ?? "N/A",
                        VehicleType = t.Vehicle?.VehicleType?.VehicleTypeName ?? "N/A",
                        OwnerId = t.OwnerId,
                        OwnerName = t.Owner?.FullName ?? "N/A",
                        OwnerCompany = t.Owner?.CompanyName ?? "N/A",
                        StartAddress = t.ShippingRoute?.StartLocation?.Address ?? string.Empty,
                        EndAddress = t.ShippingRoute?.EndLocation?.Address ?? string.Empty,
                        EstimatedDuration = (t.ShippingRoute != null &&
                                t.ShippingRoute.ExpectedDeliveryDate > t.ShippingRoute.ExpectedPickupDate)
                                ? t.ShippingRoute.ExpectedDeliveryDate - t.ShippingRoute.ExpectedPickupDate
                                : TimeSpan.Zero,
                        TripRouteSummary = t.TripRoute != null
                        ? $"Distance: {t.TripRoute.DistanceKm} km, Duration: {t.TripRoute.Duration.TotalMinutes:F0} minutes"
                        : string.Empty,
                        PackageCodes = t.Packages.Select(p => p.PackageCode).ToList(),
                        DriverNames = t.DriverAssignments.Select(d => d.Driver?.FullName ?? "N/A").ToList(),
                        AssignmentType = currentAssign?.Type.ToString() ?? "",
                        AssignmentStatus = currentAssign?.AssignmentStatus.ToString() ?? "",
                        //DriverPaymentStatus = currentAssign?.PaymentStatus.ToString() ?? ""
                    };
                }).ToList();

                // 🔹 6. Tạo kết quả phân trang
                var paginatedResult = new PaginatedDTO<DriverTripDetailDTO>(
          mappedData,
          totalCount,
          pageNumber,
          pageSize
        );

                return new ResponseDTO("Get trips successfully", 200, true, paginatedResult);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error getting trips by driver: {ex.Message}", 500, false);
            }
        }

        public async Task<ResponseDTO> GetAllTripsByProviderAsync(int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                // 🔹 1. Lấy ID và Role từ Token
                var providerId = _userUtility.GetUserIdFromToken();
                var userRole = _userUtility.GetUserRoleFromToken();

                // 🔹 2. Kiểm tra quyền
                if (userRole != "Provider")
                    return new ResponseDTO("Forbidden: Chỉ 'Provider' mới có thể truy cập.", 403, false);
                if (providerId == Guid.Empty)
                    return new ResponseDTO("Unauthorized: Token không hợp lệ.", 401, false);

                // 🔹 3. Lấy tất cả các Hợp đồng (Contract) mà Provider này tham gia
                var contracts = await _unitOfWork.TripProviderContractRepo.GetAllAsync(
                    filter: c => c.CounterpartyId == providerId
                           && c.Trip.Status != TripStatus.CANCELLED
                           && c.Trip.Status != TripStatus.DELETED,
                    // Include các thông tin mà Provider cần xem
                    includeProperties: "Trip,Trip.Vehicle,Trip.Vehicle.VehicleType,Trip.Owner,Trip.ShippingRoute,Trip.ShippingRoute.StartLocation,Trip.ShippingRoute.EndLocation,Trip.TripRoute,Trip.Packages,Trip.DriverAssignments.Driver,Trip.DriverContracts"
                );

                if (contracts == null || !contracts.Any())
                    return new ResponseDTO("No trips found for this provider.", 404, false);

                // 🔹 4. Lấy danh sách các trip duy nhất và phân trang (In-Memory)
                var trips = contracts.Select(c => c.Trip).Distinct();
                var totalCount = trips.Count();
                var pagedTrips = trips
                    .OrderByDescending(t => t.CreateAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // 🔹 5. Map sang DTO (Dùng chung TripDetailDTO với Owner)
                var mappedData = pagedTrips.Select(t => new TripDetailDTO
                {
                    TripId = t.TripId,
                    TripCode = t.TripCode,
                    Status = t.Status.ToString(),
                    CreateAt = t.CreateAt,
                    UpdateAt = t.UpdateAt,

                    VehicleId = t.VehicleId,
                    VehicleModel = t.Vehicle?.Model ?? "N/A",
                    VehiclePlate = t.Vehicle?.PlateNumber ?? "N/A",
                    VehicleType = t.Vehicle?.VehicleType?.VehicleTypeName ?? "N/A",

                    OwnerId = t.OwnerId,
                    OwnerName = t.Owner?.FullName ?? "N/A",
                    OwnerCompany = t.Owner?.CompanyName ?? "N/A",

                    StartAddress = t.ShippingRoute?.StartLocation?.Address ?? string.Empty,
                    EndAddress = t.ShippingRoute?.EndLocation?.Address ?? string.Empty,

                    EstimatedDuration = (t.ShippingRoute != null &&
                        t.ShippingRoute.ExpectedDeliveryDate > t.ShippingRoute.ExpectedPickupDate)
                        ? t.ShippingRoute.ExpectedDeliveryDate - t.ShippingRoute.ExpectedPickupDate
                        : TimeSpan.Zero,

                    PackageCodes = t.Packages.Select(p => p.PackageCode).ToList(),
                    DriverNames = t.DriverAssignments.Select(a => a.Driver?.FullName ?? "N/A").ToList(),
                    TripRouteSummary = t.TripRoute != null
                        ? $"Distance: {t.TripRoute.DistanceKm} km, Duration: {t.TripRoute.Duration.TotalMinutes:F0} minutes"
                        : string.Empty,

                }).ToList();

                // 🔹 6. Trả về kết quả phân trang
                var paginatedResult = new PaginatedDTO<TripDetailDTO>(
                    mappedData,
                    totalCount,
                    pageNumber,
                    pageSize
                );

                return new ResponseDTO("Get trips successfully", 200, true, paginatedResult);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error getting trips by provider: {ex.Message}", 500, false);
            }
        }
        public async Task<ResponseDTO> GetAllAsync(int pageNumber, int pageSize)
        {
            try
            {
                // 1. Chỉ Admin mới có quyền
                var userRole = _userUtility.GetUserRoleFromToken();
                if (userRole != "Admin")
                {
                    return new ResponseDTO("Forbidden: Chỉ 'Admin' mới có thể truy cập.", 403, false);
                }

                // 2. Lấy IQueryable (đã lọc DELETED)
                var query = _unitOfWork.TripRepo.GetAll()
                    .AsNoTracking()
                    .Where(t => t.Status != TripStatus.DELETED);

                // 3. Include dữ liệu
                query = IncludeTripDetails(query);

                // 4. Đếm tổng số
                var totalCount = await query.CountAsync();

                // 5. Lấy dữ liệu của trang
                var pagedTrips = await query
                    .OrderByDescending(t => t.CreateAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // 6. Map (Dùng chung DTO với Owner)
                var mappedData = pagedTrips.Select(t => new TripDetailDTO
                {
                    TripId = t.TripId,
                    TripCode = t.TripCode,
                    Status = t.Status.ToString(),
                    CreateAt = t.CreateAt,
                    UpdateAt = t.UpdateAt,
                    VehicleId = t.VehicleId,
                    VehicleModel = t.Vehicle?.Model ?? "N/A",
                    VehiclePlate = t.Vehicle?.PlateNumber ?? "N/A",
                    VehicleType = t.Vehicle?.VehicleType?.VehicleTypeName ?? "N/A",
                    OwnerId = t.OwnerId,
                    OwnerName = t.Owner?.FullName ?? "N/A",
                    OwnerCompany = t.Owner?.CompanyName ?? "N/A",
                    StartAddress = t.ShippingRoute?.StartLocation?.Address ?? string.Empty,
                    EndAddress = t.ShippingRoute?.EndLocation?.Address ?? string.Empty,
                    EstimatedDuration = (t.ShippingRoute != null && t.ShippingRoute.ExpectedDeliveryDate > t.ShippingRoute.ExpectedPickupDate)
                                        ? t.ShippingRoute.ExpectedDeliveryDate - t.ShippingRoute.ExpectedPickupDate : TimeSpan.Zero,
                    PackageCodes = t.Packages.Select(p => p.PackageCode).ToList(),
                    DriverNames = t.DriverAssignments.Select(a => a.Driver?.FullName ?? "N/A").ToList(),
                    TripRouteSummary = t.TripRoute != null
                                       ? $"Distance: {t.TripRoute.DistanceKm} km, Duration: {t.TripRoute.Duration.TotalMinutes:F0} minutes" : string.Empty,
                }).ToList();

                // 7. Trả về
                var paginatedResult = new PaginatedDTO<TripDetailDTO>(mappedData, totalCount, pageNumber, pageSize);
                return new ResponseDTO("Get all trips successfully (Admin)", 200, true, paginatedResult);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error getting all trips: {ex.Message}", 500, false);
            }
        }


        public async Task<ResponseDTO> GetTripByIdAsync(Guid tripId)
        {
            try
            {
                // 🔹 1. Lấy ID và Role từ Token
                var userId = _userUtility.GetUserIdFromToken();
                var userRole = _userUtility.GetUserRoleFromToken();
                if (userId == Guid.Empty)
                    return new ResponseDTO("Unauthorized or invalid token", 401, false);

                // 🔹 2. TRUY VẤN SƠ BỘ (CHỈ ĐỂ XÁC THỰC)
                var tripForAuth = await _unitOfWork.TripRepo.FirstOrDefaultAsync(
                    filter: t => t.TripId == tripId,
                    // ⚠️ SỬA ĐỔI: Include thêm Hợp đồng Provider để kiểm tra
                    includeProperties: "DriverAssignments,TripProviderContract"
                );

                if (tripForAuth == null)
                    return new ResponseDTO("Trip not found", 404, false);

                // 🔹 3. Kiểm tra quyền (Authorization)
                bool isOwner = (userRole == "Owner" && tripForAuth.OwnerId == userId);

                bool isAssignedDriver = (userRole == "Driver" &&
                                       tripForAuth.DriverAssignments.Any(a => a.DriverId == userId));

                // ⚠️ THÊM MỚI: Kiểm tra Provider (người ký HĐ)
                bool isProvider = (userRole == "Provider" &&
                                     tripForAuth.TripProviderContract != null &&
                                     tripForAuth.TripProviderContract.CounterpartyId == userId);

                // ⚠️ SỬA ĐỔI: Thêm logic isProvider
                //if (!isOwner && !isAssignedDriver && !isProvider)
                //    return new ResponseDTO("Forbidden: Bạn không có quyền xem chuyến đi này.", 403, false);   // NHỚ LÀ PHẢI SỬA CHỖ NÀY NHÉ

                // 🔹 4. TÁCH TRUY VẤN (SPLIT QUERY)

                // --- TRUY VẤN 4.1: Tải Dữ liệu Chính (Không có Collection) ---
                var query = _unitOfWork.TripRepo.GetAll().Where(t => t.TripId == tripId);

                var dto = await query.Select(trip => new TripDetailFullDTO
                {
                    TripId = trip.TripId,
                    TripCode = trip.TripCode,
                    Status = trip.Status.ToString(),
                    CreateAt = trip.CreateAt,
                    UpdateAt = trip.UpdateAt,

                    // --- Vehicle (Safer) ---
                    Vehicle = trip.Vehicle == null ? new() : new VehicleSummaryDTO
                    {
                        VehicleId = trip.Vehicle.VehicleId,
                        PlateNumber = trip.Vehicle.PlateNumber,
                        Model = trip.Vehicle.Model,
                        VehicleTypeName = trip.Vehicle.VehicleType != null ? trip.Vehicle.VehicleType.VehicleTypeName : "N/A",
                        ImageUrls = trip.Vehicle.VehicleImages != null ?
                                        trip.Vehicle.VehicleImages
                                        .Select(img => img.ImageURL)
                                        .ToList() : new List<string>()
                    },

                    // --- Owner (Safer) ---
                    Owner = trip.Owner == null ? new() : new OwnerSummaryDTO
                    {
                        OwnerId = trip.OwnerId,
                        FullName = trip.Owner.FullName,
                        CompanyName = trip.Owner.CompanyName,
                        PhoneNumber = trip.Owner.PhoneNumber
                    },

                    // --- Shipping Route (Safer) ---
                    ShippingRoute = trip.ShippingRoute == null ? new() : new RouteDetailDTO
                    {
                        StartAddress = trip.ShippingRoute.StartLocation != null ? trip.ShippingRoute.StartLocation.Address : string.Empty,
                        EndAddress = trip.ShippingRoute.EndLocation != null ? trip.ShippingRoute.EndLocation.Address : string.Empty,
                        EstimatedDuration = trip.ShippingRoute.ExpectedDeliveryDate - trip.ShippingRoute.ExpectedPickupDate
                    },

                    // --- Trip Route (Safer) ---
                    TripRoute = trip.TripRoute == null ? new() : new TripRouteSummaryDTO
                    {
                        DistanceKm = trip.TripRoute.DistanceKm,
                        DurationMinutes = trip.TripRoute.Duration.TotalMinutes,
                        RouteData = trip.TripRoute.RouteData
                    },

                    // --- Provider (Safer) ---
                    Provider = (trip.Type == Common.Enums.Type.TripType.FROM_PROVIDER && trip.PostTrip != null && trip.PostTrip.Owner != null)
                        ? new ProviderSummaryDTO
                        {
                            ProviderId = trip.PostTrip.OwnerId,
                            CompanyName = trip.PostTrip.Owner.CompanyName,
                            TaxCode = trip.PostTrip.Owner.TaxCode,
                            AverageRating = trip.PostTrip.Owner.AverageRating ?? 0
                        } : null,

                    // QUAN TRỌNG: Khởi tạo rỗng các List, chúng ta sẽ tải chúng sau
                    Packages = new List<PackageSummaryDTO>(),
                    Drivers = new List<TripDriverAssignmentDTO>(),
                    Contacts = new List<TripContactDTO>(),
                    DriverContracts = new List<ContractSummaryDTO>(),
                    ProviderContracts = new ContractSummaryDTO(), // Sẽ tải sau
                    DeliveryRecords = new List<TripDeliveryRecordDTO>(),
                    Compensations = new List<TripCompensationDTO>(),
                    Issues = new List<TripDeliveryIssueDTO>()

                }).FirstOrDefaultAsync();

                if (dto == null)
                    return new ResponseDTO("Trip not found after main query.", 404, false);


                // --- TRUY VẤN 4.2 -> 4.N: Tải riêng từng Collection (Siêu nhanh) ---

                // Tải Packages (bao gồm Items)
                dto.Packages = await _unitOfWork.PackageRepo.GetAll()
                    .Where(p => p.TripId == tripId)
                    .Select(p => new PackageSummaryDTO
                    {
                        PackageId = p.PackageId,
                        PackageCode = p.PackageCode,
                        Weight = p.WeightKg,
                        Volume = p.VolumeM3,
                        ImageUrls = p.PackageImages != null ?
                                        p.PackageImages
                                        .Select(img => img.PackageImageURL)
                                        .ToList() : new List<string>(),
                        Items = (p.Item == null)
                            ? new List<ItemSummaryDTO>()
                            : new List<ItemSummaryDTO>
                            {
                        new ItemSummaryDTO
                        {
                            ItemId = p.Item.ItemId,
                            ItemName = p.Item.ItemName,
                            Description = p.Item.Description,
                            DeclaredValue = p.Item.DeclaredValue ?? 0,
                            Images = p.Item.ItemImages != null ?
                                        p.Item.ItemImages
                                        .Select(img => img.ItemImageURL)
                                        .ToList() : new List<string>()
                        }
                            }
                    }).ToListAsync();

                // Tải Drivers
                dto.Drivers = await _unitOfWork.TripDriverAssignmentRepo.GetAll()
                    .Where(d => d.TripId == tripId)
                    .Select(d => new TripDriverAssignmentDTO
                    {
                        DriverId = d.DriverId,
                        FullName = d.Driver != null ? d.Driver.FullName : "N/A",
                        Type = d.Type.ToString(),
                        AssignmentStatus = d.AssignmentStatus.ToString(),
                        //PaymentStatus = d.PaymentStatus.ToString()
                    }).ToListAsync();

                // Tải Contacts
                dto.Contacts = await _unitOfWork.TripContactRepo.GetAll()
                    .Where(c => c.TripId == tripId)
                    .Select(c => new TripContactDTO
                    {
                        TripContactId = c.TripContactId,
                        Type = c.Type.ToString(),
                        FullName = c.FullName,
                        PhoneNumber = c.PhoneNumber,
                        Note = c.Note
                    }).ToListAsync();

                // Tải Driver Contracts (với Terms và Chữ ký)
                dto.DriverContracts = await _unitOfWork.TripDriverContractRepo.GetAll()
                    .Where(c => c.TripId == tripId)
                    .Select(c => new ContractSummaryDTO
                    {
                        ContractId = c.ContractId,
                        ContractCode = c.ContractCode,
                        Status = c.Status.ToString(),
                        Type = c.Type.ToString(),
                        ContractValue = c.ContractValue ?? 0,
                        Currency = c.Currency,
                        EffectiveDate = c.EffectiveDate,
                        ExpirationDate = c.ExpirationDate,
                        FileURL = c.FileURL,
                        OwnerSignAt = c.OwnerSignAt,
                        OwnerSigned = c.OwnerSigned,
                        CounterpartySignAt = c.CounterpartySignAt,
                        CounterpartySigned = c.CounterpartySigned,
                        CounterpartyId = c.CounterpartyId,
                        Terms = (c.ContractTemplate != null && c.ContractTemplate.ContractTerms != null) ?
                                    c.ContractTemplate.ContractTerms
                                    .Select(t => new ContractTermInTripDTO
                                    {
                                        ContractTermId = t.ContractTermId,
                                        Content = t.Content,
                                        Order = t.Order,
                                        ContractTemplateId = t.ContractTemplateId
                                    })
                                    .OrderBy(t => t.Order)
                                    .ToList() : new List<ContractTermInTripDTO>()
                    }).ToListAsync();

                // Tải Provider Contract (với Terms và Chữ ký)
                // ⚠️ SỬA ĐỔI: Cho phép Owner HOẶC Provider tải hợp đồng này
                if (isOwner || isProvider)
                {
                    dto.ProviderContracts = await _unitOfWork.TripProviderContractRepo.GetAll()
                        .Where(c => c.TripId == tripId)
                        .Select(c => new ContractSummaryDTO
                        {
                            ContractId = c.ContractId,
                            ContractCode = c.ContractCode,
                            Status = c.Status.ToString(),
                            Type = c.Type.ToString(),
                            ContractValue = c.ContractValue ?? 0,
                            Currency = c.Currency,
                            EffectiveDate = c.EffectiveDate,
                            ExpirationDate = c.ExpirationDate,
                            FileURL = c.FileURL,
                            OwnerSignAt = c.OwnerSignAt,
                            OwnerSigned = c.OwnerSigned,
                            CounterpartySignAt = c.CounterpartySignAt,
                            CounterpartySigned = c.CounterpartySigned,
                            Terms = (c.ContractTemplate != null && c.ContractTemplate.ContractTerms != null) ?
                                        c.ContractTemplate.ContractTerms
                                        .Select(t => new ContractTermInTripDTO
                                        {
                                            ContractTermId = t.ContractTermId,
                                            Content = t.Content,
                                            Order = t.Order,
                                            ContractTemplateId = t.ContractTemplateId
                                        })
                                        .OrderBy(t => t.Order)
                                        .ToList() : new List<ContractTermInTripDTO>()
                        }).FirstOrDefaultAsync() ?? new ContractSummaryDTO();
                }

                // Tải Delivery Records (với Terms)
                dto.DeliveryRecords = await _unitOfWork.TripDeliveryRecordRepo.GetAll()
                    .Where(r => r.TripId == tripId)
                    .Select(r => new TripDeliveryRecordDTO
                    {
                        TripDeliveryRecordId = r.DeliveryRecordId,
                        RecordType = r.Type.ToString(),
                        Note = r.Notes,
                        CreateAt = r.CreatedAt,
                        ContactSigned = r.ContactSigned,
                        ContactSignedAt = r.ContactSignedAt,
                        DriverId = r.DriverId,
                        DriverSigned = r.DriverSigned,
                        DriverSignedAt = r.DriverSignedAt,
                        TripContactId = r.TripContactId,
                        Status = r.Status.ToString(),
                        Terms = (r.DeliveryRecordTemplate != null && r.DeliveryRecordTemplate.DeliveryRecordTerms != null) ?
                                    r.DeliveryRecordTemplate.DeliveryRecordTerms
                                    .Select(t => new DeliveryRecordTermInTripDTO
                                    {
                                        DeliveryRecordTermId = t.DeliveryRecordTermId,
                                        Content = t.Content,
                                        DisplayOrder = t.DisplayOrder
                                    })
                                    .OrderBy(t => t.DisplayOrder)
                                    .ToList() : new List<DeliveryRecordTermInTripDTO>()
                    }).ToListAsync();

                // Tải Compensations
                dto.Compensations = await _unitOfWork.TripCompensationRepo.GetAll()
                     .Where(cp => cp.TripId == tripId)
                     .Select(cp => new TripCompensationDTO
                     {
                         TripCompensationId = cp.TripCompensationId,
                         Reason = cp.Reason,
                         Amount = cp.Amount
                     }).ToListAsync();

                // Tải Delivery Issues
                dto.Issues = await _unitOfWork.TripDeliveryIssueRepo.GetAll()
                    .Where(i => i.TripId == tripId)
                    .Select(i => new TripDeliveryIssueDTO
                    {
                        TripDeliveryIssueId = i.TripDeliveryIssueId,
                        IssueType = i.IssueType.ToString(),
                        Description = i.Description,
                        Status = i.Status.ToString()
                    }).ToListAsync();


                // 🔹 5. Trả về DTO đã được điền đầy đủ
                return new ResponseDTO("Get trip successfully", 200, true, dto);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching trip detail: {ex.Message} \n {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                return new ResponseDTO($"Error fetching trip detail: {ex.Message}", 500, false);
            }
        }


        public async Task<ResponseDTO> GetAllAsync(
     int pageNumber,
     int pageSize,
     string search = null,
     string sortField = null,
     string sortDirection = "DESC"
 )
        {
            try
            {
              
                // 2. Base query
                var query = _unitOfWork.TripRepo.GetAll()
                    .AsNoTracking()
                    .Where(t => t.Status != TripStatus.DELETED);

                // 3. Include
                query = IncludeTripDetails(query);

                // ==========================
                // 🔎 SEARCH
                // ==========================
                if (!string.IsNullOrWhiteSpace(search))
                {
                    string keyword = search.Trim().ToLower();

                    query = query.Where(t =>
                        (t.TripCode != null && t.TripCode.ToLower().Contains(keyword)) ||
                        (t.Owner != null && t.Owner.FullName.ToLower().Contains(keyword)) ||
                        (t.Vehicle != null && t.Vehicle.PlateNumber.ToLower().Contains(keyword)) ||
                        (t.ShippingRoute.StartLocation.Address.ToLower().Contains(keyword)) ||
                        (t.ShippingRoute.EndLocation.Address.ToLower().Contains(keyword))
                    );
                }

                // ==========================
                // 🔽 SORT
                // ==========================
                bool desc = sortDirection?.ToUpper() == "DESC";

                query = sortField?.ToLower() switch
                {
                    "tripcode" => desc ? query.OrderByDescending(t => t.TripCode)
                                       : query.OrderBy(t => t.TripCode),

                    "owner" => desc ? query.OrderByDescending(t => t.Owner.FullName)
                                    : query.OrderBy(t => t.Owner.FullName),

                    "vehicle" => desc ? query.OrderByDescending(t => t.Vehicle.PlateNumber)
                                      : query.OrderBy(t => t.Vehicle.PlateNumber),

                    "status" => desc ? query.OrderByDescending(t => t.Status)
                                     : query.OrderBy(t => t.Status),

                    "createdat" => desc ? query.OrderByDescending(t => t.CreateAt)
                                        : query.OrderBy(t => t.CreateAt),

                    _ => query.OrderByDescending(t => t.CreateAt) // default
                };

                // ==========================
                // 📌 PAGING
                // ==========================
                var totalCount = await query.CountAsync();

                var trips = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // ==========================
                // 📌 MAP DTO
                // ==========================
                var dtoList = trips.Select(t => new TripDetailDTO
                {
                    TripId = t.TripId,
                    TripCode = t.TripCode,
                    Status = t.Status.ToString(),
                    CreateAt = t.CreateAt,
                    UpdateAt = t.UpdateAt,
                    VehiclePlate = t.Vehicle?.PlateNumber ?? "N/A",
                    VehicleModel = t.Vehicle?.Model ?? "N/A",
                    OwnerName = t.Owner?.FullName ?? "N/A",
                    StartAddress = t.ShippingRoute?.StartLocation?.Address ?? string.Empty,
                    EndAddress = t.ShippingRoute?.EndLocation?.Address ?? string.Empty,
                    DriverNames = t.DriverAssignments.Select(a => a.Driver.FullName).ToList(),
                    PackageCodes = t.Packages.Select(p => p.PackageCode).ToList()
                }).ToList();

                var paginated = new PaginatedDTO<TripDetailDTO>(dtoList, totalCount, pageNumber, pageSize);

                return new ResponseDTO("Get all trips successfully", 200, true, paginated);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error getting all trips: {ex.Message}", 500, false);
            }
        }



        private IQueryable<Trip> IncludeTripDetails(IQueryable<Trip> query)
        {
            return query
                .Include(t => t.Vehicle).ThenInclude(v => v.VehicleType)
                .Include(t => t.Owner)
                .Include(t => t.Packages)
                .Include(t => t.ShippingRoute).ThenInclude(sr => sr.StartLocation)
                .Include(t => t.ShippingRoute).ThenInclude(sr => sr.EndLocation)
                .Include(t => t.TripRoute)
                .Include(t => t.DriverAssignments).ThenInclude(da => da.Driver)
                .Include(t => t.DriverContracts)
                .Include(t => t.TripProviderContract);
        }

    }
}

    
