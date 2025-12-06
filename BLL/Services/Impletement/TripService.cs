using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.Enums.Type;
using Common.Helpers;
using DAL.Entities;
using DAL.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly IUserDocumentService _userDocumentService;

        public TripService(
            IUnitOfWork unitOfWork,
            UserUtility userUtility,
            IVietMapService vietMapService,
            ITripRouteService tripRouteService,
            ITripContactService tripContactService,
            ITripProviderContractService tripProviderContractService,
            ITripDeliveryRecordService tripDeliveryRecordService,
            IEmailService emailService,
            IServiceScopeFactory serviceScopeFactory,
            IUserDocumentService userDocumentService)
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
            _userDocumentService = userDocumentService;
        }

        // =========================================================================================================
        // 1. CREATE TRIP FROM POST (NHẬN CHUYẾN TỪ BÀI ĐĂNG)
        // =========================================================================================================
        public async Task<ResponseDTO> CreateTripFromPostAsync(TripCreateFromPostDTO dto)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var ownerId = _userUtility.GetUserIdFromToken();
                if (ownerId == Guid.Empty) return new ResponseDTO("Unauthorized or invalid token", 401, false);

                // --- BƯỚC 0: CHECK GIẤY TỜ ---
                var verifyCheck = await _userDocumentService.ValidateUserDocumentsAsync(ownerId);
                if (!verifyCheck.IsValid) return new ResponseDTO(verifyCheck.Message, 403, false);

                // 1. VALIDATE POST PACKAGE
                // Include: ShippingRoute, PostContacts, Provider, Packages
                var postPackage = await _unitOfWork.PostPackageRepo.GetAllQueryable()
                    .Include(p => p.ShippingRoute).ThenInclude(sr => sr.StartLocation)
                    .Include(p => p.ShippingRoute).ThenInclude(sr => sr.EndLocation)
                    .Include(p => p.PostContacts)
                    .Include(p => p.Provider)
                    .Include(p => p.Packages)
                    .FirstOrDefaultAsync(p => p.PostPackageId == dto.PostPackageId);

                if (postPackage == null) return new ResponseDTO("Không tìm thấy Bài đăng.", 404, false);
                if (postPackage.Status != PostStatus.OPEN) return new ResponseDTO("Bài đăng này đã đóng hoặc đã được nhận.", 400, false);
                if (postPackage.ShippingRoute == null) return new ResponseDTO("Bài đăng thiếu thông tin Lộ trình.", 400, false);
                if (postPackage.Provider == null) return new ResponseDTO("Bài đăng thiếu thông tin Nhà cung cấp.", 400, false);

                // 2. VALIDATE VEHICLE
                // Include: VehicleType
                var vehicle = await _unitOfWork.VehicleRepo.GetAll()
                    .Include(v => v.VehicleType)
                    .FirstOrDefaultAsync(v => v.VehicleId == dto.VehicleId && v.OwnerId == ownerId);

                if (vehicle == null) return new ResponseDTO("Xe không tìm thấy hoặc không thuộc về bạn.", 404, false);

                // 3. CHECK SCHEDULE CONFLICT (TRÙNG LỊCH)
                await ValidateVehicleScheduleAsync(dto.VehicleId, postPackage.ShippingRoute);

                // 4. TẠO TRIP ROUTE (GỌI VIETMAP)
                var newTripRoute = await _tripRouteService.CreateAndAddTripRouteAsync(postPackage.ShippingRoute, vehicle);

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
                    // ActualPickupTime, ActualCompletedTime để null
                };
                await _unitOfWork.TripRepo.AddAsync(trip);

                // 6. TẠO CONTRACT (PROVIDER - OWNER)
                await _tripProviderContractService.CreateAndAddContractAsync(
                    trip.TripId, ownerId, postPackage.ProviderId, postPackage.OfferedPrice
                );

                // 7. COPY CONTACTS
                await _tripContactService.CopyContactsFromPostAsync(trip.TripId, postPackage.PostContacts);

                // 8. CẬP NHẬT TRẠNG THÁI POST & PACKAGES
                postPackage.Status = PostStatus.DONE;
                postPackage.Updated = DateTime.UtcNow;
                await _unitOfWork.PostPackageRepo.UpdateAsync(postPackage);

                foreach (var pkg in postPackage.Packages)
                {
                    pkg.TripId = trip.TripId;
                    pkg.OwnerId = ownerId;
                    pkg.Status = PackageStatus.IN_PROGRESS;
                    await _unitOfWork.PackageRepo.UpdateAsync(pkg);
                }

                // 9. COMMIT
                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync();

                return new ResponseDTO("Nhận chuyến thành công!", 201, true, new { TripId = trip.TripId, TripCode = trip.TripCode });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO($"Lỗi nhận chuyến: {ex.Message}", 500, false);
            }
        }

        // =========================================================================================================
        // 2. CHANGE TRIP STATUS (LOGIC "CUỐN CHIẾU" - WATERFALL)
        // =========================================================================================================
        public async Task<ResponseDTO> ChangeTripStatusAsync(ChangeTripStatusDTO dto)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();

            // Biến để lưu kết quả cuối cùng trả về cho Client
            string finalMessage = "";
            bool isPartialSuccess = false; // Đánh dấu nếu dừng giữa đường

            // Biến tài chính để gửi mail
            Trip tripForMail = null;
            decimal ownerReceived = 0;
            decimal providerPaid = 0;
            var paidDriversMap = new Dictionary<Guid, decimal>();

            try
            {
                var trip = await _unitOfWork.TripRepo.GetByIdAsync(dto.TripId);
                if (trip == null) return new ResponseDTO("Trip not found.", 404, false);

                // =====================================================================
                // CASE 1: XỬ LÝ CÁC TRẠNG THÁI THÔNG THƯỜNG (LOADING, UNLOADING...)
                // =====================================================================
                if (dto.NewStatus != TripStatus.COMPLETED)
                {
                    //// Validate chặt chẽ cho các bước thường
                    //if (!IsValidTransition(trip.Status, dto.NewStatus))
                    //    return new ResponseDTO($"Invalid transition from {trip.Status} to {dto.NewStatus}.", 400, false);

                    trip.Status = dto.NewStatus;
                    trip.UpdateAt = DateTime.UtcNow;

                    if (dto.NewStatus == TripStatus.LOADING)
                    {
                        trip.ActualPickupTime = DateTime.UtcNow;
                        await SendSignatureLinkAsync(trip.TripId, DeliveryRecordType.PICKUP);
                    }
                    else if (dto.NewStatus == TripStatus.UNLOADING)
                    {
                        await SendSignatureLinkAsync(trip.TripId, DeliveryRecordType.DROPOFF);
                    }

                    await _unitOfWork.TripRepo.UpdateAsync(trip);
                    await _unitOfWork.SaveChangeAsync();
                    await transaction.CommitAsync();

                    return new ResponseDTO($"Status changed to {dto.NewStatus} successfully.", 200, true);
                }

                // =====================================================================
                // CASE 2: XỬ LÝ LOGIC "HOÀN TẤT" (COMPLETED) - CHẠY TỰ ĐỘNG TỪNG BƯỚC
                // =====================================================================
                if (dto.NewStatus == TripStatus.COMPLETED)
                {
                    providerPaid = trip.TotalFare; // Lưu số liệu ban đầu

                    // --- BƯỚC 1: THANH TOÁN CHO OWNER (Nếu chưa làm) ---
                    // Điều kiện: Trạng thái hiện tại là VEHICLE_RETURNED (vừa trả xe xong)
                    if (trip.Status == TripStatus.DONE_TRIP_AND_WATING_FOR_PAYOUT)
                    {
                        var payOwnerResult = await ProcessOwnerPaymentAsync(trip);

                        if (payOwnerResult.Success)
                        {
                            // Thành công -> Nâng cấp trạng thái lên bước tiếp theo
                            trip.Status = TripStatus.AWAITING_FINAL_DRIVER_PAYOUT;
                            trip.UpdateAt = DateTime.UtcNow;
                            ownerReceived = payOwnerResult.Amount;

                            // Lưu tạm vào DB (để nếu bước 2 lỗi thì bước 1 vẫn được ghi nhận trong Transaction này)
                            await _unitOfWork.TripRepo.UpdateAsync(trip);
                            await _unitOfWork.SaveChangeAsync();
                        }
                        else
                        {
                            // Thất bại bước 1 -> Rollback toàn bộ và báo lỗi ngay
                            await transaction.RollbackAsync();
                            return new ResponseDTO($"Lỗi thanh toán Owner: {payOwnerResult.Message}", 500, false);
                        }
                    }

                    // --- BƯỚC 2: THANH TOÁN CHO DRIVER (Nếu bước 1 xong hoặc đã xong từ trước) ---
                    // Điều kiện: Trạng thái hiện tại đã là AWAITING_FINAL_DRIVER_PAYOUT
                    if (trip.Status == TripStatus.AWAITING_FINAL_DRIVER_PAYOUT)
                    {
                        var payDriverResult = await ProcessDriverPaymentsAsync(trip);

                        if (payDriverResult.Success)
                        {
                            // Thành công -> Nâng cấp trạng thái lên CAO NHẤT (COMPLETED)
                            trip.Status = TripStatus.COMPLETED;
                            trip.ActualCompletedTime = DateTime.UtcNow;
                            trip.UpdateAt = DateTime.UtcNow;

                            paidDriversMap = payDriverResult.PaidMap;

                            // Lưu trạng thái cuối cùng
                            await _unitOfWork.TripRepo.UpdateAsync(trip);
                            await _unitOfWork.SaveChangeAsync();

                            finalMessage = "Chuyến đi đã hoàn tất thành công (Đã trả tiền Owner & Driver).";
                            tripForMail = trip; // Đánh dấu để gửi mail
                        }
                        else
                        {
                            // ⚠️ QUAN TRỌNG: Thất bại bước 2 -> KHÔNG ROLLBACK BƯỚC 1
                            // Ta chỉ dừng lại ở trạng thái AWAITING_FINAL_DRIVER_PAYOUT
                            // Commit transaction để lưu lại việc "Đã trả tiền Owner"

                            isPartialSuccess = true;
                            finalMessage = $"Đã thanh toán Owner thành công, nhưng lỗi thanh toán Driver: {payDriverResult.Message}. Trạng thái dừng ở: {trip.Status}.";
                        }
                    }

                    // --- COMMIT TRANSACTION ---
                    // Dù thành công hết hay chỉ thành công một nửa (Partial), ta đều Commit 
                    // những gì đã làm được (được lưu trong SaveChangeAsync phía trên).
                    await transaction.CommitAsync();
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO($"Critical Error: {ex.Message}", 500, false);
            }

            // 6. Gửi Email (Chỉ gửi nếu thực sự COMPLETED)
            if (tripForMail != null && !isPartialSuccess)
            {
                _ = Task.Run(() => SendCompletionEmailsBackground(tripForMail.TripId, tripForMail.OwnerId, providerPaid, ownerReceived, paidDriversMap));
            }

            // Trả về kết quả tùy tình huống
            if (isPartialSuccess)
            {
                // Trả về 206 Partial Content hoặc 200 kèm warning
                return new ResponseDTO(finalMessage, 206, true);
            }

            return new ResponseDTO(finalMessage, 200, true);
        }

        // =========================================================================================================
        // 3. GET TRIPS (QUERY METHODS)
        // =========================================================================================================

        // =========================================================================================================
        // 3. GET TRIPS (QUERY METHODS - FULL SEARCH & SORT)
        // =========================================================================================================

        // --- Get All (Admin) ---
        public async Task<ResponseDTO> GetAllAsync(int pageNumber, int pageSize, string? search, string? sortField, string? sortDirection)
        {
            try
            {
                if (_userUtility.GetUserRoleFromToken() != "Admin") return new ResponseDTO("Forbidden.", 403, false);

                // 1. Base Query
                var query = _unitOfWork.TripRepo.GetAll()
                    .AsNoTracking()
                    .Where(t => t.Status != TripStatus.DELETED);

                query = IncludeTripDetails(query);

                // 2. Search & Sort
                query = ApplyTripFilter(query, search);
                query = ApplyTripSort(query, sortField, sortDirection);

                // 3. Paging
                var totalCount = await query.CountAsync();
                var trips = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

                var dtos = trips.Select(MapToTripDetailDTO).ToList();
                return new ResponseDTO("Success", 200, true, new PaginatedDTO<TripDetailDTO>(dtos, totalCount, pageNumber, pageSize));
            }
            catch (Exception ex) { return new ResponseDTO(ex.Message, 500, false); }
        }

        // --- Get By Owner ---
        public async Task<ResponseDTO> GetAllTripsByOwnerAsync(int pageNumber, int pageSize, string? search, string? sortField, string? sortDirection)
        {
            try
            {
                var ownerId = _userUtility.GetUserIdFromToken();
                if (ownerId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                // 1. Base Query
                var query = _unitOfWork.TripRepo.GetAll()
                    .AsNoTracking()
                    .Where(t => t.OwnerId == ownerId && t.Status != TripStatus.DELETED);

                query = IncludeTripDetails(query);

                // 2. Search & Sort
                query = ApplyTripFilter(query, search);
                query = ApplyTripSort(query, sortField, sortDirection);

                // 3. Paging
                var totalCount = await query.CountAsync();
                var trips = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

                var dtos = trips.Select(MapToTripDetailDTO).ToList();
                return new ResponseDTO("Success", 200, true, new PaginatedDTO<TripDetailDTO>(dtos, totalCount, pageNumber, pageSize));
            }
            catch (Exception ex) { return new ResponseDTO(ex.Message, 500, false); }
        }

        // --- Get By Driver ---
        public async Task<ResponseDTO> GetAllTripsByDriverAsync(int pageNumber, int pageSize, string? search, string? sortField, string? sortDirection)
        {
            try
            {
                var driverId = _userUtility.GetUserIdFromToken();
                if (driverId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                // 1. Base Query (Từ Assignment -> Trip)
                // Lưu ý: Để Search/Sort được trên Trip, ta nên query từ TripRepo và Join/Any với Assignment
                var query = _unitOfWork.TripRepo.GetAll()
                    .AsNoTracking()
                    .Where(t => t.Status != TripStatus.DELETED && t.DriverAssignments.Any(a => a.DriverId == driverId));

                query = IncludeTripDetails(query);

                // 2. Search & Sort
                query = ApplyTripFilter(query, search);
                query = ApplyTripSort(query, sortField, sortDirection);

                // 3. Paging
                var totalCount = await query.CountAsync();
                var trips = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

                // 4. Map (Lấy thêm thông tin Assignment cụ thể của Driver này)
                // Vì trips đã load về RAM, ta có thể tìm Assignment trong list con
                var dtos = trips.Select<Trip, DriverTripDetailDTO>(t =>
                {
                    var myAssign = t.DriverAssignments.FirstOrDefault(a => a.DriverId == driverId);
                    // Fallback nếu include bị thiếu (dù đã include ở trên)
                    if (myAssign == null)
                    {
                        // MapToTripDetailDTO trả về TripDetailDTO, cần chuyển sang DriverTripDetailDTO
                        var baseDto = MapToTripDetailDTO(t);
                        return new DriverTripDetailDTO
                        {
                            TripId = baseDto.TripId,
                            TripCode = baseDto.TripCode,
                            Status = baseDto.Status,
                            CreateAt = baseDto.CreateAt,
                            UpdateAt = baseDto.UpdateAt,
                            VehicleId = baseDto.VehicleId,
                            VehicleModel = baseDto.VehicleModel,
                            VehiclePlate = baseDto.VehiclePlate,
                            VehicleType = baseDto.VehicleType,
                            OwnerId = baseDto.OwnerId,
                            OwnerName = baseDto.OwnerName,
                            OwnerCompany = baseDto.OwnerCompany,
                            StartAddress = baseDto.StartAddress,
                            EndAddress = baseDto.EndAddress,
                            PackageCodes = baseDto.PackageCodes,
                            DriverNames = baseDto.DriverNames,
                            AssignmentType = null,
                            AssignmentStatus = null
                        };
                    }
                    return MapToDriverTripDetailDTO(t, myAssign);       // Map chuyên sâu cho driver
                }).ToList();

                return new ResponseDTO("Success", 200, true, new PaginatedDTO<DriverTripDetailDTO>(dtos, totalCount, pageNumber, pageSize));
            }
            catch (Exception ex) { return new ResponseDTO(ex.Message, 500, false); }
        }

        // --- Get By Provider ---
        public async Task<ResponseDTO> GetAllTripsByProviderAsync(int pageNumber, int pageSize, string? search, string? sortField, string? sortDirection)
        {
            try
            {
                var providerId = _userUtility.GetUserIdFromToken();
                if (providerId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                // 1. Base Query (Từ Contract -> Trip)
                // Tương tự Driver, query từ TripRepo và check Contract
                var query = _unitOfWork.TripRepo.GetAll()
                    .AsNoTracking()
                    .Where(t => t.Status != TripStatus.DELETED &&
                                t.TripProviderContract != null &&
                                t.TripProviderContract.CounterpartyId == providerId);

                query = IncludeTripDetails(query);

                // 2. Search & Sort
                query = ApplyTripFilter(query, search);
                query = ApplyTripSort(query, sortField, sortDirection);

                // 3. Paging
                var totalCount = await query.CountAsync();
                var trips = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

                var dtos = trips.Select(MapToTripDetailDTO).ToList();
                return new ResponseDTO("Success", 200, true, new PaginatedDTO<TripDetailDTO>(dtos, totalCount, pageNumber, pageSize));
            }
            catch (Exception ex) { return new ResponseDTO(ex.Message, 500, false); }
        }

        // ==================================================================================
        // PRIVATE HELPERS (SEARCH & SORT)
        // ==================================================================================

        private IQueryable<Trip> ApplyTripFilter(IQueryable<Trip> query, string? search)
        {
            if (!string.IsNullOrWhiteSpace(search))
            {
                string k = search.Trim().ToLower();
                query = query.Where(t =>
                    (t.TripCode != null && t.TripCode.ToLower().Contains(k)) ||
                    (t.Owner != null && t.Owner.FullName.ToLower().Contains(k)) ||
                    (t.Vehicle != null && t.Vehicle.PlateNumber.ToLower().Contains(k)) ||
                    (t.ShippingRoute != null && t.ShippingRoute.StartLocation != null && t.ShippingRoute.StartLocation.Address.ToLower().Contains(k)) ||
                    (t.ShippingRoute != null && t.ShippingRoute.EndLocation != null && t.ShippingRoute.EndLocation.Address.ToLower().Contains(k))
                );
            }
            return query;
        }

        private IQueryable<Trip> ApplyTripSort(IQueryable<Trip> query, string? field, string? direction)
        {
            bool desc = direction?.ToUpper() == "DESC";
            return field?.ToLower() switch
            {
                "tripcode" => desc ? query.OrderByDescending(t => t.TripCode) : query.OrderBy(t => t.TripCode),
                "status" => desc ? query.OrderByDescending(t => t.Status) : query.OrderBy(t => t.Status),
                "createdat" => desc ? query.OrderByDescending(t => t.CreateAt) : query.OrderBy(t => t.CreateAt),
                "ownername" => desc ? query.OrderByDescending(t => t.Owner.FullName) : query.OrderBy(t => t.Owner.FullName),
                "plate" => desc ? query.OrderByDescending(t => t.Vehicle.PlateNumber) : query.OrderBy(t => t.Vehicle.PlateNumber),
                _ => query.OrderByDescending(t => t.CreateAt) // Default
            };
        }

        // --- Get Detail By ID ---
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
                    includeProperties: "DriverAssignments,TripProviderContract"
                );

                if (tripForAuth == null)
                    return new ResponseDTO("Trip not found", 404, false);

                // 🔹 3. Kiểm tra quyền (Authorization)
                bool isOwner = (userRole == "Owner" && tripForAuth.OwnerId == userId);
                bool isAssignedDriver = (userRole == "Driver" &&
                                       tripForAuth.DriverAssignments.Any(a => a.DriverId == userId));
                bool isProvider = (userRole == "Provider" &&
                                     tripForAuth.TripProviderContract != null &&
                                     tripForAuth.TripProviderContract.CounterpartyId == userId);

                // (Bạn có thể bật lại nếu muốn)
                //if (!isOwner && !isAssignedDriver && !isProvider)
                //    return new ResponseDTO("Forbidden: Bạn không có quyền xem chuyến đi này.", 403, false);


                // 🔹 4. TÁCH TRUY VẤN (SPLIT QUERY)

                // --- TRUY VẤN 4.1: Tải Dữ liệu Chính ---
                var query = _unitOfWork.TripRepo.GetAll().Where(t => t.TripId == tripId);

                var dto = await query.Select(trip => new TripDetailFullDTO
                {
                    TripId = trip.TripId,
                    TripCode = trip.TripCode,
                    Status = trip.Status.ToString(),
                    CreateAt = trip.CreateAt,
                    UpdateAt = trip.UpdateAt,

                    // [MAPPING MỚI] Lấy thông tin điểm lấy/trả xe từ ShippingRoute
                    // (Vì logic hệ thống là Xe đi theo Hàng, nên điểm lấy xe = điểm bắt đầu Route)

                    // 1. Lấy điểm LẤY XE (StartLocation của Primary)
                    VehiclePickupAddress = trip.DriverAssignments
        .Where(a => a.Type == Common.Enums.Type.DriverType.PRIMARY)
        .Select(a => a.StartLocation.Address)
        .FirstOrDefault() ?? "", // Nếu chưa có tài chính thì để rỗng

                    VehiclePickupLat = trip.DriverAssignments
        .Where(a => a.Type == Common.Enums.Type.DriverType.PRIMARY)
        .Select(a => a.StartLocation.Latitude ?? 0)
        .FirstOrDefault(),

                    VehiclePickupLng = trip.DriverAssignments
        .Where(a => a.Type == Common.Enums.Type.DriverType.PRIMARY)
        .Select(a => a.StartLocation.Longitude ?? 0)
        .FirstOrDefault(),

                    // 2. Lấy điểm TRẢ XE (EndLocation của Primary)
                    VehicleDropoffAddress = trip.DriverAssignments
        .Where(a => a.Type == Common.Enums.Type.DriverType.PRIMARY)
        .Select(a => a.EndLocation.Address)
        .FirstOrDefault() ?? "",

                    VehicleDropoffLat = trip.DriverAssignments
        .Where(a => a.Type == Common.Enums.Type.DriverType.PRIMARY)
        .Select(a => a.EndLocation.Latitude ?? 0)
        .FirstOrDefault(),

                    VehicleDropoffLng = trip.DriverAssignments
        .Where(a => a.Type == Common.Enums.Type.DriverType.PRIMARY)
        .Select(a => a.EndLocation.Longitude ?? 0)
        .FirstOrDefault(),

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

                    Owner = trip.Owner == null ? new() : new OwnerSummaryDTO
                    {
                        OwnerId = trip.OwnerId,
                        FullName = trip.Owner.FullName,
                        CompanyName = trip.Owner.CompanyName,
                        PhoneNumber = trip.Owner.PhoneNumber
                    },

                    ShippingRoute = trip.ShippingRoute == null ? new() : new RouteDetailDTO
                    {
                        StartAddress = trip.ShippingRoute.StartLocation != null ? trip.ShippingRoute.StartLocation.Address : string.Empty,
                        EndAddress = trip.ShippingRoute.EndLocation != null ? trip.ShippingRoute.EndLocation.Address : string.Empty,
                        EstimatedDuration = trip.ShippingRoute.ExpectedDeliveryDate - trip.ShippingRoute.ExpectedPickupDate
                    },

                    TripRoute = trip.TripRoute == null ? new() : new TripRouteSummaryDTO
                    {
                        DistanceKm = trip.TripRoute.DistanceKm,
                        DurationMinutes = trip.TripRoute.Duration.TotalMinutes,
                        RouteData = trip.TripRoute.RouteData
                    },

                    Provider = (trip.Type == Common.Enums.Type.TripType.FROM_PROVIDER && trip.PostTrip != null && trip.PostTrip.Owner != null)
                        ? new ProviderSummaryDTO
                        {
                            ProviderId = trip.PostTrip.OwnerId,
                            CompanyName = trip.PostTrip.Owner.CompanyName,
                            TaxCode = trip.PostTrip.Owner.TaxCode,
                            AverageRating = trip.PostTrip.Owner.AverageRating ?? 0
                        } : null,

                    Packages = new List<PackageSummaryDTO>(),
                    Drivers = new List<TripDriverAssignmentDTO>(),
                    Contacts = new List<TripContactDTO>(),
                    DriverContracts = new List<ContractSummaryDTO>(),
                    ProviderContracts = new ContractSummaryDTO(),
                    DeliveryRecords = new List<TripDeliveryRecordDTO>(),
                    Compensations = new List<TripCompensationDTO>(),
                    Issues = new List<TripDeliveryIssueDTO>(),
                    handoverReadDTOs = new List<TripVehicleHandoverReadDTO>()

                }).FirstOrDefaultAsync();

                if (dto == null)
                    return new ResponseDTO("Trip not found after main query.", 404, false);


                // --- TRUY VẤN 4.2 -> 4.N: Tải riêng từng Collection ---

                // Packages
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
                                  p.Item.ItemImages.Select(img => img.ItemImageURL).ToList()
                                  : new List<string>()
                  }
                            }
                    }).ToListAsync();

                // Drivers
                dto.Drivers = await _unitOfWork.TripDriverAssignmentRepo.GetAll()
                    .Where(d => d.TripId == tripId)
                    .Select(d => new TripDriverAssignmentDTO
                    {
                        DriverId = d.DriverId,
                        FullName = d.Driver != null ? d.Driver.FullName : "N/A",
                        Type = d.Type.ToString(),
                        AssignmentStatus = d.AssignmentStatus.ToString(),
                    }).ToListAsync();

                // Contacts
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

                // Driver Contracts
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

                // Provider Contract
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

                // Delivery Records
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

                // Compensations
                dto.Compensations = await _unitOfWork.TripCompensationRepo.GetAll()
                     .Where(cp => cp.TripId == tripId)
                     .Select(cp => new TripCompensationDTO
                     {
                         TripCompensationId = cp.TripCompensationId,
                         Reason = cp.Reason,
                         Amount = cp.Amount
                     }).ToListAsync();

                // Delivery Issues
                dto.Issues = await _unitOfWork.TripDeliveryIssueRepo.GetAll()
                    .Where(i => i.TripId == tripId)
                    .Select(i => new TripDeliveryIssueDTO
                    {
                        TripDeliveryIssueId = i.TripDeliveryIssueId,
                        IssueType = i.IssueType.ToString(),
                        Description = i.Description,
                        Status = i.Status.ToString()
                    }).ToListAsync();


                // 🔹 Load Trip Vehicle Handover Records (MỚI)
                dto.handoverReadDTOs = await _unitOfWork.TripVehicleHandoverRecordRepo.GetAll()
                    .Where(h => h.TripId == tripId)
                    .Select(h => new TripVehicleHandoverReadDTO
                    {
                        TripVehicleHandoverRecordId = h.DeliveryRecordId,
                        TripId = h.TripId,
                        VehicleId = h.VehicleId,
                        Type = h.Type.ToString(),
                        Status = h.Status.ToString(),

                        HandoverUserId = h.OwnerId,
                        HandoverUserName = h.Owner.FullName != null ? h.Owner.FullName : "N/A",
                        ReceiverUserId = h.DriverId,
                        ReceiverUserName = h.Driver.FullName != null ? h.Driver.FullName : "N/A",

                        CurrentOdometer = h.CurrentOdometer,
                        FuelLevel = h.FuelLevel,
                        IsEngineLightOn = h.IsEngineLightOn,
                        Notes = h.Notes,

                        HandoverSigned = h.OwnerSigned,
                        HandoverSignedAt = h.OwnerSignedAt,


                        ReceiverSigned = h.DriverSigned,
                        ReceiverSignedAt = h.DriverSignedAt,

                    })
                    .ToListAsync();


                // 🔹 5. Trả về DTO
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




        // =========================================================================================================
        // PRIVATE HELPERS (LOGIC & MAPPERS)
        // =========================================================================================================


        public async Task<ResponseDTO> GetTripDriverAnalysisAsync(Guid tripId)
        {
            try
            {
                var trip = await _unitOfWork.TripRepo.GetAll()
                    .Include(t => t.DriverAssignments)
                    .Include(t => t.ShippingRoute)
                    .FirstOrDefaultAsync(t => t.TripId == tripId);

                if (trip == null) return new ResponseDTO("Trip not found", 404, false);

                // 1. Dùng Helper tính lại nhu cầu (dựa trên thông số đã lưu)
                var suggestion = TripCalculationHelper.CalculateScenarios(
                    (double)trip.ActualDistanceKm,
                    trip.ActualDuration.TotalHours,
                    trip.ShippingRoute.ExpectedPickupDate,
                    trip.ShippingRoute.ExpectedDeliveryDate
                );

                // 2. Check hiện trạng
                var validAssigns = trip.DriverAssignments
                    .Where(a => a.AssignmentStatus == AssignmentStatus.ACCEPTED || a.AssignmentStatus == AssignmentStatus.ACCEPTED)
                    .ToList();

                var analysis = new TripDriverAnalysisDTO
                {
                    Suggestion = suggestion,
                    TotalAssigned = validAssigns.Count,
                    HasMainDriver = validAssigns.Any(a => a.Type == DriverType.PRIMARY),
                    AssistantCount = validAssigns.Count(a => a.Type == DriverType.SECONDARY)
                };

                // 3. Tính toán GAP (Còn thiếu bao nhiêu?)
                // Ưu tiên Team (2 tài) nếu Solo (1 tài) không khả thi
                int targetDrivers = 1; // Mặc định 1
                string mode = "SOLO";

                if (!suggestion.SoloScenario.IsPossible) // Nếu 1 tài không kịp
                {
                    if (suggestion.TeamScenario.IsPossible) { targetDrivers = 2; mode = "TEAM"; }
                    else { targetDrivers = 3; mode = "EXPRESS"; }
                }

                int remaining = targetDrivers - analysis.TotalAssigned;
                if (remaining < 0) remaining = 0;

                analysis.RemainingSlots = remaining;

                if (remaining == 0)
                    analysis.Recommendation = "Đã đủ người theo lộ trình.";
                else if (!analysis.HasMainDriver)
                    analysis.Recommendation = $"Cần tuyển gấp 1 Tài chính ({mode}).";
                else
                    analysis.Recommendation = $"Đã có Tài chính. Cần thêm {remaining} Tài phụ ({mode}).";

                return new ResponseDTO("Success", 200, true, analysis);
            }
            catch (Exception ex) { return new ResponseDTO(ex.Message, 500, false); }
        }

        private async Task ValidateVehicleScheduleAsync(Guid vehicleId, ShippingRoute route)
        {
            TimeSpan startTime = route.PickupTimeWindow?.StartTime?.ToTimeSpan() ?? TimeSpan.Zero;
            TimeSpan endTime = route.DeliveryTimeWindow?.EndTime?.ToTimeSpan() ?? new TimeSpan(23, 59, 59);

            var newStart = route.ExpectedPickupDate.Date.Add(startTime);
            var newEnd = route.ExpectedDeliveryDate.Date.Add(endTime);

            if (newStart >= newEnd) throw new Exception("Thời gian Lấy hàng phải trước thời gian Giao hàng.");

            var activeTrips = await _unitOfWork.TripRepo.GetAll()
                .Include(t => t.ShippingRoute)
                .Where(t => t.VehicleId == vehicleId &&
                            t.Status != TripStatus.COMPLETED &&
                            t.Status != TripStatus.CANCELLED &&
                            t.Status != TripStatus.DELETED)
                .ToListAsync();

            foreach (var t in activeTrips)
            {
                if (t.ShippingRoute == null) continue;
                var exStart = t.ShippingRoute.ExpectedPickupDate.Date.Add(t.ShippingRoute.PickupTimeWindow?.StartTime?.ToTimeSpan() ?? TimeSpan.Zero);
                var exEnd = t.ShippingRoute.ExpectedDeliveryDate.Date.Add(t.ShippingRoute.DeliveryTimeWindow?.EndTime?.ToTimeSpan() ?? new TimeSpan(23, 59, 59));

                if (exStart < newEnd && exEnd > newStart)
                {
                    throw new Exception($"Xe bị trùng lịch với chuyến {t.TripCode} ({exStart:dd/MM HH:mm} - {exEnd:dd/MM HH:mm}).");
                }
            }
        }

        private async Task SendSignatureLinkAsync(Guid tripId, DeliveryRecordType type)
        {
            var record = await _unitOfWork.TripDeliveryRecordRepo.GetAll()
                .FirstOrDefaultAsync(r => r.TripId == tripId && r.Type == type);

            // Nếu record tồn tại VÀ (ContactSigned là false HOẶC null)
            if (record != null && record.ContactSigned != true)
            {
                await _tripDeliveryRecordService.SendAccessLinkToContactAsync(record.DeliveryRecordId);
            }
        }

        private async Task<(bool Success, decimal Amount, string Message)> ProcessOwnerPaymentAsync(Trip trip)
        {
            var wallet = await _unitOfWork.WalletRepo.FirstOrDefaultAsync(w => w.UserId == trip.OwnerId);
            if (wallet == null || wallet.Status != WalletStatus.ACTIVE) return (false, 0, "Lỗi ví Owner.");

            decimal fee = trip.TotalFare * 0.1m; // Phí sàn 10%
            decimal amount = trip.TotalFare - fee;

            wallet.Balance += amount;
            wallet.LastUpdatedAt = DateTime.UtcNow;
            await _unitOfWork.WalletRepo.UpdateAsync(wallet);

            await _unitOfWork.TransactionRepo.AddAsync(new Transaction
            {
                TransactionId = Guid.NewGuid(),
                WalletId = wallet.WalletId,
                TripId = trip.TripId,
                Amount = amount,
                Type = TransactionType.OWNER_PAYOUT,
                Status = TransactionStatus.SUCCEEDED,
                Description = $"Thanh toán chuyến {trip.TripCode}",
                CreatedAt = DateTime.UtcNow
            });

            return (true, amount, "");
        }

        private async Task<(bool Success, Dictionary<Guid, decimal> PaidMap, string Message)> ProcessDriverPaymentsAsync(Trip trip)
        {
            var paidMap = new Dictionary<Guid, decimal>();
            var assignments = await _unitOfWork.TripDriverAssignmentRepo.GetAll()
                .Where(a => a.TripId == trip.TripId && a.AssignmentStatus == AssignmentStatus.ACCEPTED && a.PaymentStatus != DriverPaymentStatus.PAID)
                .ToListAsync();

            foreach (var assign in assignments)
            {
                var wallet = await _unitOfWork.WalletRepo.FirstOrDefaultAsync(w => w.UserId == assign.DriverId);
                if (wallet != null && wallet.Status == WalletStatus.ACTIVE)
                {
                    decimal amount = assign.TotalAmount; // Logic tính tổng tiền ở Property TotalAmount của Entity

                    wallet.Balance += amount;
                    wallet.LastUpdatedAt = DateTime.UtcNow;
                    await _unitOfWork.WalletRepo.UpdateAsync(wallet);

                    assign.PaymentStatus = DriverPaymentStatus.PAID;
                    assign.UpdateAt = DateTime.UtcNow;
                    await _unitOfWork.TripDriverAssignmentRepo.UpdateAsync(assign);

                    await _unitOfWork.TransactionRepo.AddAsync(new Transaction
                    {
                        TransactionId = Guid.NewGuid(),
                        WalletId = wallet.WalletId,
                        TripId = trip.TripId,
                        Amount = amount,
                        Type = TransactionType.DRIVER_PAYOUT,
                        Status = TransactionStatus.SUCCEEDED,
                        Description = $"Lương chuyến {trip.TripCode}",
                        CreatedAt = DateTime.UtcNow
                    });

                    if (!paidMap.ContainsKey(assign.DriverId)) paidMap.Add(assign.DriverId, amount);
                }
            }
            return (true, paidMap, "");
        }

        private async Task SendCompletionEmailsBackground(Guid tripId, Guid ownerId, decimal pPaid, decimal oReceived, Dictionary<Guid, decimal> drivers)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var scopedUow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var scopedEmailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            try
            {
                var tripFull = await scopedUow.TripRepo.GetAll()
                    .Include(t => t.ShippingRoute)
                    .Include(t => t.Vehicle).ThenInclude(v => v.VehicleType)
                    .Include(t => t.Packages)
                    .Include(t => t.TripProviderContract)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.TripId == tripId);

                if (tripFull == null) return;

                // Base Model
                var commonData = new TripCompletionReportModel
                {
                    TripCode = tripFull.TripCode,
                    CompletedAt = DateTime.UtcNow.AddHours(7).ToString("HH:mm dd/MM/yyyy"),
                    StartAddress = tripFull.ShippingRoute?.StartLocation?.Address ?? "N/A",
                    EndAddress = tripFull.ShippingRoute?.EndLocation?.Address ?? "N/A",
                    DistanceKm = (double)tripFull.ActualDistanceKm,
                    VehiclePlate = tripFull.Vehicle?.PlateNumber ?? "N/A",
                    VehicleType = tripFull.Vehicle?.VehicleType?.VehicleTypeName ?? "N/A",
                    PackageCount = tripFull.Packages?.Count ?? 0,
                    TotalPayload = tripFull.Packages?.Sum(p => p.WeightKg) ?? 0
                };

                // 1. PROVIDER (Giữ nguyên)
                if (tripFull.TripProviderContract != null)
                {
                    var provider = await scopedUow.BaseUserRepo.GetByIdAsync(tripFull.TripProviderContract.CounterpartyId);
                    if (provider != null)
                    {
                        var pReport = commonData.Clone();
                        pReport.RecipientName = provider.FullName;
                        pReport.Role = "Provider";
                        pReport.IsIncome = false;
                        pReport.Amount = pPaid;
                        pReport.FinancialDescription = "TỔNG CHI PHÍ VẬN CHUYỂN";
                        await scopedEmailService.SendTripCompletionEmailAsync(provider.Email, pReport);
                    }
                }

                // 2. DRIVERS (Giữ nguyên)
                // Cần lưu lại danh sách chi tiết để đưa vào báo cáo Owner
                var driverExpensesList = new List<ExpenseDetail>();

                foreach (var pd in drivers)
                {
                    var driver = await scopedUow.BaseUserRepo.GetByIdAsync(pd.Key);
                    if (driver != null)
                    {
                        // Xác định role (Tài chính/Tài phụ) để hiển thị đẹp hơn
                        var roleType = await scopedUow.TripDriverAssignmentRepo.GetAll()
                            .Where(a => a.TripId == tripId && a.DriverId == pd.Key)
                            .Select(a => a.Type)
                            .FirstOrDefaultAsync();

                        string roleName = roleType == DriverType.PRIMARY ? "Tài chính" : "Tài phụ";

                        // Add vào list chi phí của Owner
                        driverExpensesList.Add(new ExpenseDetail
                        {
                            DriverName = driver.FullName,
                            Role = roleName,
                            Amount = pd.Value
                        });

                        // Gửi mail cho Driver
                        var dReport = commonData.Clone();
                        dReport.RecipientName = driver.FullName;
                        dReport.Role = "Driver";
                        dReport.IsIncome = true;
                        dReport.Amount = pd.Value;
                        dReport.FinancialDescription = $"TIỀN CÔNG ({roleName.ToUpper()})";
                        await scopedEmailService.SendTripCompletionEmailAsync(driver.Email, dReport);
                    }
                }

                // 3. OWNER (CẬP NHẬT MỚI: QUYẾT TOÁN TỔNG HỢP)
                var owner = await scopedUow.BaseUserRepo.GetByIdAsync(ownerId);
                if (owner != null)
                {
                    var oReport = commonData.Clone();
                    oReport.RecipientName = owner.FullName;
                    oReport.Role = "Owner";

                    // Điền dữ liệu tổng hợp
                    oReport.TotalIncome = oReceived; // Tiền từ Provider (đã trừ phí sàn)
                    oReport.TotalExpense = drivers.Sum(x => x.Value); // Tổng tiền trả driver
                    oReport.DriverExpenses = driverExpensesList; // Chi tiết trả ai

                    await scopedEmailService.SendTripCompletionEmailAsync(owner.Email, oReport);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending emails: {ex.Message}");
            }
        }

        private bool IsValidTransition(TripStatus current, TripStatus next)
        {
            // (Copy nội dung hàm switch case bạn đã viết vào đây)
            if (current == TripStatus.COMPLETED || current == TripStatus.CANCELLED || current == TripStatus.DELETED) return false;
            if (next == TripStatus.CANCELLED || next == TripStatus.DELETED) return true;

            return next switch
            {
                //TripStatus.AWAITING_PROVIDER_PAYMENT => current == TripStatus.AWAITING_PROVIDER_CONTRACT,
                //TripStatus.PENDING_DRIVER_ASSIGNMENT => current == TripStatus.AWAITING_PROVIDER_PAYMENT,
               
                TripStatus.READY_FOR_VEHICLE_HANDOVER => current == TripStatus.DONE_ASSIGNING_DRIVER ,


                TripStatus.VEHICLE_HANDOVERED => current == TripStatus.READY_FOR_VEHICLE_HANDOVER,

                TripStatus.MOVING_TO_PICKUP => current == TripStatus.VEHICLE_HANDOVERED,

                TripStatus.LOADING => current == TripStatus.MOVING_TO_PICKUP,

                TripStatus.MOVING_TO_DROPOFF => current == TripStatus.LOADING,

                TripStatus.UNLOADING => current == TripStatus.MOVING_TO_DROPOFF , // Tùy flow



                //TripStatus.RETURNING_VEHICLE => current == TripStatus.DELIVERED,
                //TripStatus.VEHICLE_RETURNED => current == TripStatus.RETURNING_VEHICLE,
                //TripStatus.AWAITING_FINAL_PROVIDER_PAYOUT => current == TripStatus.VEHICLE_RETURNED,
                //TripStatus.AWAITING_FINAL_DRIVER_PAYOUT => current == TripStatus.AWAITING_FINAL_PROVIDER_PAYOUT,
                //TripStatus.COMPLETED => current == TripStatus.AWAITING_FINAL_DRIVER_PAYOUT,
                _ => false
            };
        }

        // --- MAPPER HELPERS (Giảm lặp code) ---
        private TripDetailDTO MapToTripDetailDTO(Trip t)
        {
            return new TripDetailDTO
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
                StartAddress = t.ShippingRoute?.StartLocation?.Address ?? "",
                EndAddress = t.ShippingRoute?.EndLocation?.Address ?? "",
                PackageCodes = t.Packages?.Select(p => p.PackageCode).ToList() ?? new List<string>(),
                DriverNames = t.DriverAssignments?.Select(da => da.Driver?.FullName ?? "N/A").ToList() ?? new List<string>()
            };
        }

        private DriverTripDetailDTO MapToDriverTripDetailDTO(Trip t, TripDriverAssignment assign)
        {
            var baseDto = MapToTripDetailDTO(t);
            // Map thêm các field riêng của Driver
            return new DriverTripDetailDTO
            {
                TripId = baseDto.TripId,
                TripCode = baseDto.TripCode,
                Status = baseDto.Status,
                // ... Copy các field chung ...
                AssignmentType = assign.Type.ToString(),
                AssignmentStatus = assign.AssignmentStatus.ToString()
            };
        }

        private IQueryable<Trip> IncludeTripDetails(IQueryable<Trip> query)
        {
            return query
                .Include(t => t.Vehicle).ThenInclude(v => v.VehicleType)
                .Include(t => t.Owner)
                .Include(t => t.Packages)
                .Include(t => t.ShippingRoute).ThenInclude(sr => sr.StartLocation)
                .Include(t => t.ShippingRoute).ThenInclude(sr => sr.EndLocation)
                .Include(t => t.DriverAssignments).ThenInclude(da => da.Driver);
        }

        

        private string GenerateTripCode() => $"TRIP-{Guid.NewGuid().ToString("N").ToUpper().Substring(0, 8)}";
    }
}