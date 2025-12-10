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

                // =======================================================================
                // [THÊM MỚI] 2.1. VALIDATE SỨC CHỨA (TẢI TRỌNG & THỂ TÍCH)
                // =======================================================================
                var capacityCheck = ValidateVehicleCapacity(vehicle, postPackage.Packages);
                if (!capacityCheck.IsSuccess)
                {
                    // Trả về lỗi nếu xe không chở nổi
                    return capacityCheck;
                }
                // =======================================================================

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
        // PRIVATE HELPER: VALIDATE TẢI TRỌNG & THỂ TÍCH XE
        // =========================================================================================================
        private ResponseDTO ValidateVehicleCapacity(Vehicle vehicle, ICollection<Package> packages)
        {
            // 1. Tính tổng trọng lượng hàng (kg)
            // Giả sử WeightKg cũng là decimal không null
            decimal totalPackageWeight = packages.Sum(p => p.WeightKg);

            // 2. Lấy tải trọng cho phép của xe (kg)
            decimal vehicleMaxLoad = vehicle.PayloadInKg;

            // CHECK TRỌNG LƯỢNG
            if (vehicleMaxLoad < totalPackageWeight)
            {
                return new ResponseDTO($"Xe quá tải! Tổng hàng: {totalPackageWeight}kg > Tải trọng xe: {vehicleMaxLoad}kg.", 400, false);
            }

            // 3. Tính tổng thể tích hàng (m3)
            // SỬA LỖI TẠI ĐÂY: Bỏ "?? 0m" đi
            decimal totalPackageVolume = packages.Sum(p => p.VolumeM3);

            decimal vehicleMaxVolume = vehicle.VolumeInM3;

            // CHECK THỂ TÍCH (Chỉ check nếu xe có thùng > 0)
            if (vehicleMaxVolume > 0 && vehicleMaxVolume < totalPackageVolume)
            {
                return new ResponseDTO($"Hàng quá cồng kềnh! Tổng thể tích: {totalPackageVolume}m3 > Thùng xe: {vehicleMaxVolume}m3.", 400, false);
            }

            return new ResponseDTO("Sức chứa hợp lệ.", 200, true);
        }


        // =========================================================================================================
        // 2. MAIN FUNCTION: CHANGE TRIP STATUS
        // =========================================================================================================
        //public async Task<ResponseDTO> ChangeTripStatusAsync(ChangeTripStatusDTO dto)
        //{
        //    using var transaction = await _unitOfWork.BeginTransactionAsync();
        //    try
        //    {
        //        var trip = await _unitOfWork.TripRepo.GetByIdAsync(dto.TripId);
        //        if (trip == null) return new ResponseDTO("Trip not found.", 404, false);

        //        // --- VALIDATE LOGIC CHUYỂN TRẠNG THÁI ---
        //        if (trip.Status == TripStatus.COMPLETED || trip.Status == TripStatus.CANCELLED)
        //            return new ResponseDTO("Chuyến đi đã kết thúc, không thể thay đổi trạng thái.", 400, false);

        //        // =====================================================================
        //        // CASE A: TRẠNG THÁI THÔNG THƯỜNG (LOADING, UNLOADING...) - GIỮ NGUYÊN
        //        // =====================================================================
        //        if (dto.NewStatus != TripStatus.COMPLETED)
        //        {
        //            trip.Status = dto.NewStatus;
        //            trip.UpdateAt = DateTime.UtcNow;

        //            // Logic phụ: Ghi nhận thời gian Pickup/Dropoff thực tế
        //            if (dto.NewStatus == TripStatus.LOADING && trip.ActualPickupTime == null)
        //            {
        //                trip.ActualPickupTime = DateTime.UtcNow;
        //                await SendSignatureLinkAsync(trip.TripId, DeliveryRecordType.PICKUP);
        //            }
        //            else if (dto.NewStatus == TripStatus.UNLOADING)
        //            {
        //                await SendSignatureLinkAsync(trip.TripId, DeliveryRecordType.DROPOFF);
        //            }

        //            await _unitOfWork.TripRepo.UpdateAsync(trip);
        //            await _unitOfWork.SaveChangeAsync();
        //            await transaction.CommitAsync();

        //            return new ResponseDTO($"Đã chuyển trạng thái sang {dto.NewStatus}.", 200, true);
        //        }

        //        // =====================================================================
        //        // CASE B: HOÀN TẤT CHUYẾN ĐI (COMPLETED)
        //        // =====================================================================

        //        // [FIX] BƯỚC 1: AUTO CHECK-OUT TRƯỚC (Để chốt lương tài xế)
        //        var assignments = await _unitOfWork.TripDriverAssignmentRepo.GetAll()
        //            .Where(a => a.TripId == trip.TripId && a.Type == DriverType.PRIMARY && !a.IsFinished)
        //            .ToListAsync();

        //        if (assignments.Any())
        //        {
        //            foreach (var assign in assignments)
        //            {
        //                assign.IsFinished = true;
        //                assign.OffBoardTime = DateTime.UtcNow;
        //                assign.OffBoardLocation = "Auto-checkout (Trip Completed)";
        //                assign.CheckOutNote = "Hệ thống tự động check-out.";
        //                assign.AssignmentStatus = AssignmentStatus.COMPLETED;

        //                // QUAN TRỌNG: Cần tính toán lại TotalAmount ở đây nếu hệ thống tính tiền theo giờ/km
        //                // assign.TotalAmount = CalculateDriverSalary(...); 

        //                await _unitOfWork.TripDriverAssignmentRepo.UpdateAsync(assign);
        //            }
        //            // Lưu tạm xuống DB để hàm Liquidation bên dưới đọc được dữ liệu mới nhất (TotalAmount, OffBoardTime)
        //            await _unitOfWork.SaveChangeAsync();
        //        }

        //        // [FIX] BƯỚC 2: THANH LÝ HỢP ĐỒNG (Tính tiền sau khi đã chốt dữ liệu tài xế)
        //        var liquidationResult = await ProcessTripLiquidationAsync(trip.TripId);

        //        if (!liquidationResult.Success)
        //        {
        //            await transaction.RollbackAsync(); // Rollback cả việc auto-checkout nếu tính tiền lỗi
        //            return new ResponseDTO($"Lỗi thanh lý hợp đồng: {liquidationResult.Message}", 500, false);
        //        }

        //        // BƯỚC 3: CẬP NHẬT TRẠNG THÁI TRIP
        //        trip.Status = TripStatus.COMPLETED;
        //        trip.ActualCompletedTime = DateTime.UtcNow;
        //        trip.UpdateAt = DateTime.UtcNow;

        //        await _unitOfWork.TripRepo.UpdateAsync(trip);

        //        // Lưu tất cả thay đổi (Ví tiền, Transaction, Trip Status)
        //        await _unitOfWork.SaveChangeAsync();

        //        // Chốt Transaction
        //        await transaction.CommitAsync();

        //        // BƯỚC 4: GỬI EMAIL BACKGROUND
        //        _ = Task.Run(() => SendCompletionEmailsBackground(trip.TripId, liquidationResult.Result));

        //        return new ResponseDTO("Hoàn tất chuyến đi và thanh toán thành công.", 200, true);
        //    }
        //    catch (Exception ex)
        //    {
        //        await transaction.RollbackAsync();
        //        return new ResponseDTO($"System Error: {ex.Message}", 500, false);
        //    }
        //}

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
                var query = _unitOfWork.TripRepo.GetAll().AsNoTracking().Where(t => t.TripId == tripId);

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
                // Drivers
                dto.Drivers = await _unitOfWork.TripDriverAssignmentRepo.GetAll()
                    .Where(d => d.TripId == tripId)
                    .Select(d => new TripDriverAssignmentDTO
                    {
                        DriverId = d.DriverId,
                        FullName = d.Driver != null ? d.Driver.FullName : "N/A",
                        Type = d.Type.ToString(),
                        AssignmentStatus = d.AssignmentStatus.ToString(),
                        PaymentStatus = d.PaymentStatus.ToString(),

                        // [MAPPING MỚI] - Thông tin tiền nong
                        BaseAmount = d.BaseAmount,
                        DepositAmount = d.DepositAmount,
                        DepositStatus = d.DepositStatus.ToString(),

                        // -----------------------------------------------------------
                        // 🔥 [BỔ SUNG] START/END LOCATION CHO TỪNG DRIVER
                        // -----------------------------------------------------------
                        StartAddress = d.StartLocation != null ? d.StartLocation.Address : "",
                        StartLat = d.StartLocation != null ? (d.StartLocation.Latitude ?? 0) : 0,
                        StartLng = d.StartLocation != null ? (d.StartLocation.Longitude ?? 0) : 0,

                        EndAddress = d.EndLocation != null ? d.EndLocation.Address : "",
                        EndLat = d.EndLocation != null ? (d.EndLocation.Latitude ?? 0) : 0,
                        EndLng = d.EndLocation != null ? (d.EndLocation.Longitude ?? 0) : 0,
                        // -----------------------------------------------------------

                        // [MAPPING MỚI] - Check-in Info
                        IsOnBoard = d.IsOnBoard,
                        OnBoardTime = d.OnBoardTime,
                        OnBoardLocation = d.OnBoardLocation,
                        OnBoardImage = d.OnBoardImage,
                        CheckInNote = d.CheckInNote,

                        // [MAPPING MỚI] - Check-out Info
                        IsFinished = d.IsFinished,
                        OffBoardTime = d.OffBoardTime,
                        OffBoardLocation = d.OffBoardLocation,
                        OffBoardImage = d.OffBoardImage,
                        CheckOutNote = d.CheckOutNote

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
                dto.DriverContracts = await _unitOfWork.TripDriverContractRepo.GetAll().AsNoTracking()
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
                        Terms = (c.ContractTemplateId != null && c.ContractTemplate != null)
                    ? c.ContractTemplate.ContractTerms.Select(t => new ContractTermInTripDTO
                    {
                        ContractTermId = t.ContractTermId,
                        Content = t.Content,
                        Order = t.Order,
                        ContractTemplateId = t.ContractTemplateId
                    }).OrderBy(t => t.Order).ToList()
                    : new List<ContractTermInTripDTO>()
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


                // [UPDATED] Surcharges (Thay cho Compensations cũ)
                dto.Surcharges = await _unitOfWork.TripSurchargeRepo.GetAll()
                    .Where(s => s.TripId == tripId)
                    .OrderByDescending(s => s.CreatedAt)
                    .Select(s => new TripSurchargeReadDTO
                    {
                        TripSurchargeId = s.TripSurchargeId,
                        TripId = s.TripId,
                        Type = s.Type.ToString(),
                        Amount = s.Amount,
                        Description = s.Description,
                        Status = s.Status.ToString(),
                        CreatedAt = s.CreatedAt,
                        PaidAt = s.PaidAt,

                        // Link tới Issue (để Frontend biết phạt này do lỗi gì)
                        RelatedVehicleIssueId = s.TripVehicleHandoverIssueId,
                        RelatedDeliveryIssueId = s.TripDeliveryIssueId
                    })
                    .ToListAsync();



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

        //private async Task<(bool Success, decimal Amount, string Message)> ProcessOwnerPaymentAsync(Trip trip)
        //{
        //    var wallet = await _unitOfWork.WalletRepo.FirstOrDefaultAsync(w => w.UserId == trip.OwnerId);
        //    if (wallet == null || wallet.Status != WalletStatus.ACTIVE) return (false, 0, "Lỗi ví Owner.");

        //    // 1. Cộng tiền cước (Income)
        //    decimal fee = trip.TotalFare * 0.1m; // Phí sàn 10%
        //    decimal netIncome = trip.TotalFare - fee;

        //    wallet.Balance += netIncome;
        //    wallet.LastUpdatedAt = DateTime.UtcNow;
        //    await _unitOfWork.WalletRepo.UpdateAsync(wallet);

        //    await _unitOfWork.TransactionRepo.AddAsync(new Transaction
        //    {
        //        TransactionId = Guid.NewGuid(),
        //        WalletId = wallet.WalletId,
        //        TripId = trip.TripId,
        //        Amount = netIncome,
        //        Type = TransactionType.OWNER_PAYOUT,
        //        Status = TransactionStatus.SUCCEEDED,
        //        Description = $"Thanh toán chuyến {trip.TripCode}",
        //        CreatedAt = DateTime.UtcNow
        //    });

        //    // =======================================================================
        //    // 2. XỬ LÝ HOÀN TIỀN CHO PROVIDER (NẾU HÀNG HƯ)
        //    // =======================================================================
        //    var cargoSurcharges = await _unitOfWork.TripSurchargeRepo.GetAll()
        //        .Where(s => s.TripId == trip.TripId
        //                 && (s.Type == SurchargeType.CARGO_DAMAGE || s.Type == SurchargeType.CARGO_LOSS)
        //                 && s.Status == SurchargeStatus.PENDING)
        //        .ToListAsync();

        //    if (cargoSurcharges.Any())
        //    {
        //        decimal totalRefund = cargoSurcharges.Sum(s => s.Amount);

        //        // A. Trừ tiền Owner (Đền bù)
        //        wallet.Balance -= totalRefund; // Trừ trực tiếp vào số dư vừa nhận
        //                                       // (Nếu số dư < 0 thì Owner nợ sàn)

        //        await _unitOfWork.TransactionRepo.AddAsync(new Transaction
        //        {
        //            TransactionId = Guid.NewGuid(),
        //            WalletId = wallet.WalletId,
        //            TripId = trip.TripId,
        //            Amount = -totalRefund,
        //            Type = TransactionType.PENALTY, // Phạt/Đền bù
        //            Status = TransactionStatus.SUCCEEDED,
        //            Description = $"Đền bù hàng hóa chuyến {trip.TripCode}",
        //            CreatedAt = DateTime.UtcNow
        //        });

        //        // B. Cộng tiền cho Provider (Chủ hàng)
        //        // [FIX LỖI TẠI ĐÂY]: Tìm Provider thông qua Contract hoặc Package
        //        Guid providerId = Guid.Empty;

        //        // Cách 1: Lấy qua Hợp đồng (Ưu tiên - Vì Provider là người ký hợp đồng)
        //        var contract = await _unitOfWork.TripProviderContractRepo.FirstOrDefaultAsync(c => c.TripId == trip.TripId);
        //        if (contract != null)
        //        {
        //            providerId = contract.CounterpartyId;
        //        }
        //        else
        //        {
        //            // Cách 2: Fallback lấy qua Package đầu tiên trong chuyến
        //            var firstPkg = await _unitOfWork.PackageRepo.GetAll()
        //                .FirstOrDefaultAsync(p => p.TripId == trip.TripId);

        //            if (firstPkg != null && firstPkg.ProviderId.HasValue)
        //            {
        //                providerId = firstPkg.ProviderId.Value;
        //            }
        //        }

        //        // Nếu tìm thấy Provider -> Thực hiện hoàn tiền
        //        if (providerId != Guid.Empty)
        //        {
        //            var providerWallet = await _unitOfWork.WalletRepo.FirstOrDefaultAsync(w => w.UserId == providerId);
        //            if (providerWallet != null)
        //            {
        //                providerWallet.Balance += totalRefund;
        //                providerWallet.LastUpdatedAt = DateTime.UtcNow;
        //                await _unitOfWork.WalletRepo.UpdateAsync(providerWallet);

        //                await _unitOfWork.TransactionRepo.AddAsync(new Transaction
        //                {
        //                    TransactionId = Guid.NewGuid(),
        //                    WalletId = providerWallet.WalletId,
        //                    TripId = trip.TripId,
        //                    Amount = totalRefund,
        //                    Type = TransactionType.REFUND, // Hoàn tiền
        //                    Status = TransactionStatus.SUCCEEDED,
        //                    Description = $"Nhận đền bù hàng hóa chuyến {trip.TripCode}",
        //                    CreatedAt = DateTime.UtcNow
        //                });
        //            }
        //        }
        //    }

        //    return (true, netIncome, "");
        //}

        //private async Task<(bool Success, Dictionary<Guid, decimal> PaidMap, string Message)> ProcessDriverPaymentsAsync(Trip trip)
        //{
        //    var paidMap = new Dictionary<Guid, decimal>();
        //    try
        //    {
        //        // 1. Lấy TẤT CẢ các khoản phạt (Surcharge) CHƯA TRẢ của chuyến đi này
        //        // (Bao gồm cả khoản CARGO mà Owner vừa ứng tiền trả Provider ở trên, giờ Owner thu lại từ Driver)
        //        var pendingSurcharges = await _unitOfWork.TripSurchargeRepo.GetAll()
        //            .Where(s => s.TripId == trip.TripId && s.Status == SurchargeStatus.PENDING)
        //            .ToListAsync();

        //        decimal totalTripFine = pendingSurcharges.Sum(s => s.Amount);

        //        // 2. Lấy danh sách tài xế
        //        var assignments = await _unitOfWork.TripDriverAssignmentRepo.GetAll()
        //            .Where(a => a.TripId == trip.TripId && a.AssignmentStatus == AssignmentStatus.ACCEPTED && a.PaymentStatus != DriverPaymentStatus.PAID)
        //            .ToListAsync();

        //        if (!assignments.Any()) return (true, paidMap, "Không có tài xế.");

        //        // 3. Tính Tổng Quỹ Lương (để làm mẫu số chia tỷ lệ)
        //        decimal totalTripSalary = assignments.Sum(a => a.TotalAmount);

        //        // 4. Vòng lặp thanh toán
        //        foreach (var assign in assignments)
        //        {
        //            var wallet = await _unitOfWork.WalletRepo.FirstOrDefaultAsync(w => w.UserId == assign.DriverId);
        //            if (wallet != null && wallet.Status == WalletStatus.ACTIVE)
        //            {
        //                decimal driverSalary = assign.TotalAmount;
        //                decimal driverFineShare = 0;

        //                // --- [MỚI] LOGIC TRỪ PHẠT THEO TỶ TRỌNG ---
        //                if (totalTripFine > 0 && totalTripSalary > 0)
        //                {
        //                    decimal ratio = driverSalary / totalTripSalary;
        //                    driverFineShare = Math.Round(totalTripFine * ratio, 0);
        //                }

        //                decimal finalPay = driverSalary - driverFineShare;
        //                if (finalPay < 0) finalPay = 0;

        //                // Cộng tiền vào ví Driver
        //                wallet.Balance += finalPay;
        //                wallet.LastUpdatedAt = DateTime.UtcNow;
        //                await _unitOfWork.WalletRepo.UpdateAsync(wallet);

        //                // Update Assignment
        //                assign.PaymentStatus = DriverPaymentStatus.PAID;
        //                assign.UpdateAt = DateTime.UtcNow;
        //                await _unitOfWork.TripDriverAssignmentRepo.UpdateAsync(assign);

        //                // Log Transaction
        //                string desc = $"Lương chuyến {trip.TripCode}";
        //                if (driverFineShare > 0) desc += $" (Trừ phạt: {driverFineShare:N0}đ)";

        //                await _unitOfWork.TransactionRepo.AddAsync(new Transaction
        //                {
        //                    TransactionId = Guid.NewGuid(),
        //                    WalletId = wallet.WalletId,
        //                    TripId = trip.TripId,
        //                    Amount = finalPay,
        //                    Type = TransactionType.DRIVER_PAYOUT,
        //                    Status = TransactionStatus.SUCCEEDED,
        //                    Description = desc,
        //                    CreatedAt = DateTime.UtcNow
        //                });

        //                if (!paidMap.ContainsKey(assign.DriverId)) paidMap.Add(assign.DriverId, finalPay);
        //            }
        //        }

        //        // 5. [MỚI] Cập nhật trạng thái các khoản phạt -> ĐÃ TRẢ (PAID)
        //        // Vì tiền phạt đã được trừ vào lương của tài xế rồi
        //        foreach (var fine in pendingSurcharges)
        //        {
        //            fine.Status = SurchargeStatus.PAID;
        //            fine.PaidAt = DateTime.UtcNow;
        //        }

        //        // Lưu ý: Không cần gọi SaveChangeAsync ở đây nếu bên ngoài Transaction đã gọi,
        //        // Nhưng để chắc ăn trong hàm này thì cứ để SaveChangeAsync (EF Core tự quản lý transaction lồng nhau)

        //        return (true, paidMap, "");
        //    }
        //    catch (Exception ex)
        //    {
        //        return (false, paidMap, ex.Message);
        //    }
        //}

        // =========================================================================================================
        // 2. CORE LOGIC: THANH LÝ HỢP ĐỒNG (LIQUIDATION)
        // =========================================================================================================
        // =========================================================================================================
        // 2. HELPER LOGIC: TÍNH TOÁN TIỀN NONG (LIQUIDATION)
        // =========================================================================================================
        // =========================================================================================================
        // 2. MAIN FUNCTION: CHANGE TRIP STATUS
        // =========================================================================================================
        public async Task<ResponseDTO> ChangeTripStatusAsync(ChangeTripStatusDTO dto)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Load Trip (Nên Include đủ dữ liệu cần thiết nếu muốn tối ưu query sau này)
                var trip = await _unitOfWork.TripRepo.GetByIdAsync(dto.TripId);
                if (trip == null) return new ResponseDTO("Trip not found.", 404, false);

                // --- VALIDATE LOGIC CHUYỂN TRẠNG THÁI ---
                if (trip.Status == TripStatus.COMPLETED || trip.Status == TripStatus.CANCELLED)
                    return new ResponseDTO("Chuyến đi đã kết thúc, không thể thay đổi trạng thái.", 400, false);

                // =====================================================================
                // CASE A: TRẠNG THÁI THÔNG THƯỜNG (LOADING, UNLOADING...) - GIỮ NGUYÊN
                // =====================================================================
                if (dto.NewStatus != TripStatus.COMPLETED)
                {
                    trip.Status = dto.NewStatus;
                    trip.UpdateAt = DateTime.UtcNow;

                    // Logic phụ: Ghi nhận thời gian Pickup/Dropoff thực tế
                    if (dto.NewStatus == TripStatus.LOADING && trip.ActualPickupTime == null)
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

                    return new ResponseDTO($"Đã chuyển trạng thái sang {dto.NewStatus}.", 200, true);
                }

                // =====================================================================
                // CASE B: HOÀN TẤT CHUYẾN ĐI (COMPLETED)
                // =====================================================================

                // ---------------------------------------------------------------------
                // [QUAN TRỌNG - BƯỚC 1]: AUTO CHECK-OUT CHO TÀI XẾ CHÍNH TRƯỚC
                // Phải chốt thời gian và hoàn thành chuyến cho tài xế TRƯỚC khi tính tiền
                // ---------------------------------------------------------------------
                var activeAssignments = await _unitOfWork.TripDriverAssignmentRepo.GetAll()
                    .Where(a => a.TripId == trip.TripId && a.Type == DriverType.PRIMARY && !a.IsFinished)
                    .ToListAsync();

                if (activeAssignments.Any())
                {
                    foreach (var assign in activeAssignments)
                    {
                        assign.IsFinished = true;
                        assign.OffBoardTime = DateTime.UtcNow;
                        assign.OffBoardLocation = "Auto-checkout (Trip Completed)";
                        assign.CheckOutNote = "Hệ thống tự động check-out khi hoàn thành chuyến.";
                        assign.AssignmentStatus = AssignmentStatus.COMPLETED;

                        // TODO: Nếu hệ thống có tính lương theo giờ, hãy gọi hàm tính lại TotalAmount tại đây
                        // assign.TotalAmount = CalculateFinalSalary(assign);

                        await _unitOfWork.TripDriverAssignmentRepo.UpdateAsync(assign);
                    }
                    // Lưu tạm xuống DB để các hàm tính toán bên dưới nhận diện được assignment đã kết thúc
                    await _unitOfWork.SaveChangeAsync();
                }

                // ---------------------------------------------------------------------
                // [BƯỚC 2]: THANH LÝ HỢP ĐỒNG (Tính tiền Owner, Driver, Provider)
                // ---------------------------------------------------------------------
                var liquidationResult = await ProcessTripLiquidationAsync(trip.TripId);

                if (!liquidationResult.Success)
                {
                    await transaction.RollbackAsync(); // Rollback cả việc auto-checkout
                    return new ResponseDTO($"Lỗi thanh lý hợp đồng: {liquidationResult.Message}", 500, false);
                }

                // ---------------------------------------------------------------------
                // [BƯỚC 3]: CẬP NHẬT TRẠNG THÁI TRIP
                // ---------------------------------------------------------------------
                trip.Status = TripStatus.COMPLETED;
                trip.ActualCompletedTime = DateTime.UtcNow;
                trip.UpdateAt = DateTime.UtcNow;

                await _unitOfWork.TripRepo.UpdateAsync(trip);

                // Lưu tất cả thay đổi xuống DB (Trip, Wallet, Transaction, Assignment, Surcharge)
                await _unitOfWork.SaveChangeAsync();

                // Chốt Transaction
                await transaction.CommitAsync();

                // ---------------------------------------------------------------------
                // [BƯỚC 4]: GỬI EMAIL (Chạy ngầm)
                // ---------------------------------------------------------------------
                _ = Task.Run(() => SendCompletionEmailsBackground(trip.TripId, liquidationResult.Result));

                return new ResponseDTO("Hoàn tất chuyến đi và thanh toán thành công.", 200, true);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO($"System Error: {ex.Message}", 500, false);
            }
        }

        // =========================================================================================================
        // 3. HELPER LOGIC: TÍNH TOÁN TIỀN NONG (LIQUIDATION)
        // =========================================================================================================
        private async Task<(bool Success, LiquidationResultModel Result, string Message)> ProcessTripLiquidationAsync(Guid tripId)
        {
            var resultModel = new LiquidationResultModel();
            try
            {
                // 1. GET DATA (Full Include)
                var trip = await _unitOfWork.TripRepo.GetAll()
                    .Include(t => t.ShippingRoute)
                    .Include(t => t.TripProviderContract)
                    .Include(t => t.Packages)
                    .FirstOrDefaultAsync(t => t.TripId == tripId);

                if (trip == null) return (false, null, "Trip not found");

                // 2. TÌM VÍ
                Guid providerId = trip.TripProviderContract?.CounterpartyId ?? trip.Packages.FirstOrDefault()?.ProviderId ?? Guid.Empty;

                var providerWallet = await _unitOfWork.WalletRepo.FirstOrDefaultAsync(w => w.UserId == providerId);
                if (providerWallet == null) return (false, null, "Không tìm thấy ví Provider.");

                var ownerWallet = await _unitOfWork.WalletRepo.FirstOrDefaultAsync(w => w.UserId == trip.OwnerId);
                if (ownerWallet == null) return (false, null, "Không tìm thấy ví Owner.");

                // 3. TỔNG HỢP PHẠT (Đền bù hàng hóa)
                var cargoSurcharges = await _unitOfWork.TripSurchargeRepo.GetAll()
                    .Where(s => s.TripId == tripId
                            && (s.Type == SurchargeType.CARGO_DAMAGE || s.Type == SurchargeType.CARGO_LOSS)
                            && s.Status == SurchargeStatus.PENDING)
                    .ToListAsync();

                decimal totalCargoDamage = cargoSurcharges.Sum(s => s.Amount);
                decimal totalCompensatedByDrivers = 0; // Số tiền đã thu được từ tài xế

                // ==========================================================================
                // PHẦN A: TÍNH TOÁN CHO TÀI XẾ (Lương - Phạt + Hoàn Cọc)
                // ==========================================================================
                var assignments = await _unitOfWork.TripDriverAssignmentRepo.GetAll()
                    .Where(a => a.TripId == tripId && a.AssignmentStatus == AssignmentStatus.COMPLETED) // Chỉ lấy người đã completed
                    .ToListAsync();

                decimal totalDriverSalaryBase = assignments.Sum(a => a.TotalAmount);

                foreach (var assign in assignments)
                {
                    var driverWallet = await _unitOfWork.WalletRepo.FirstOrDefaultAsync(w => w.UserId == assign.DriverId);
                    if (driverWallet == null) continue;

                    decimal salary = assign.TotalAmount;
                    decimal driverShareFine = 0;

                    // --- Tính phạt theo tỷ trọng lương ---
                    foreach (var surcharge in cargoSurcharges)
                    {
                        if (IsDriverResponsible(assign, surcharge))
                        {
                            if (totalDriverSalaryBase > 0)
                            {
                                decimal ratio = salary / totalDriverSalaryBase;
                                driverShareFine += surcharge.Amount * ratio;
                            }
                        }
                    }

                    // --- Provider nhận tiền phạt ngay (trích từ lương tài xế) ---
                    if (driverShareFine > 0)
                    {
                        providerWallet.Balance += driverShareFine;
                        totalCompensatedByDrivers += driverShareFine;

                        await CreateTransactionAsync(providerWallet.WalletId, tripId, driverShareFine, TransactionType.COMPENSATION,
                            $"Đền bù từ Tài xế {assign.DriverId.ToString().Substring(0, 5)}");
                    }

                    // --- Trừ ví Tài xế ---
                    decimal netResult = salary - driverShareFine;
                    driverWallet.Balance += netResult; // Cộng (nếu dương) hoặc Trừ (nếu âm)

                    if (netResult >= 0)
                    {
                        await CreateTransactionAsync(driverWallet.WalletId, tripId, netResult, TransactionType.DRIVER_PAYOUT,
                            $"Lương chuyến {trip.TripCode} (Đã trừ phạt: {driverShareFine:N0}đ)");
                    }
                    else
                    {
                        // Âm tiền -> Ghi nợ (OUTSTANDING)
                        await CreateTransactionAsync(driverWallet.WalletId, tripId, netResult, TransactionType.OUTSTANDING_PAYMENT,
                            $"Nợ phí đền bù hàng hóa chuyến {trip.TripCode}");
                    }

                    // --- Hoàn Cọc ---
                    if (assign.DepositStatus == DepositStatus.DEPOSITED && assign.DepositAmount > 0)
                    {
                        driverWallet.Balance += assign.DepositAmount;
                        assign.DepositStatus = DepositStatus.REFUNDED;

                        await CreateTransactionAsync(driverWallet.WalletId, tripId, assign.DepositAmount, TransactionType.REFUND,
                            $"Hoàn tiền cọc chuyến {trip.TripCode}");
                    }

                    assign.PaymentStatus = DriverPaymentStatus.PAID;
                    assign.UpdateAt = DateTime.UtcNow;

                    await _unitOfWork.WalletRepo.UpdateAsync(driverWallet);
                    await _unitOfWork.TripDriverAssignmentRepo.UpdateAsync(assign);

                    resultModel.PaidDriversMap.Add(assign.DriverId, netResult);
                }

                // ==========================================================================
                // PHẦN B: TÍNH TOÁN CHO OWNER
                // ==========================================================================

                decimal fee = trip.TotalFare * 0.1m; // Phí sàn 10%
                decimal ownerRevenue = trip.TotalFare - fee; // Doanh thu sau phí sàn

                // [FIX QUAN TRỌNG]: Trừ lương tài xế (Vì Owner thuê tài xế)
                decimal finalOwnerIncome = ownerRevenue - totalDriverSalaryBase;

                // Owner bù phần đền bù còn thiếu (nếu tài xế không gánh hết hoặc lỗi vô chủ)
                decimal remainingCompensation = totalCargoDamage - totalCompensatedByDrivers;
                if (remainingCompensation < 0) remainingCompensation = 0;

                // Doanh thu thực nhận cuối cùng (Sau khi trừ lương + trừ phạt lỗi chung)
                decimal finalOwnerReceive = finalOwnerIncome - remainingCompensation;

                // Cộng tiền vào ví Owner
                ownerWallet.Balance += finalOwnerReceive;
                await CreateTransactionAsync(ownerWallet.WalletId, tripId, finalOwnerReceive, TransactionType.OWNER_PAYOUT,
                    $"Thanh toán chuyến {trip.TripCode}" + (remainingCompensation > 0 ? $" (Trừ lỗi chung: {remainingCompensation:N0}đ)" : ""));

                // Cộng phần bù vào ví Provider (Phần thiếu do Owner chịu)
                if (remainingCompensation > 0)
                {
                    providerWallet.Balance += remainingCompensation;
                    await CreateTransactionAsync(providerWallet.WalletId, tripId, remainingCompensation, TransactionType.COMPENSATION,
                        $"Đền bù từ Owner (Phần thiếu) - {trip.TripCode}");
                }

                // Cập nhật Ví Owner và Provider
                await _unitOfWork.WalletRepo.UpdateAsync(ownerWallet);
                await _unitOfWork.WalletRepo.UpdateAsync(providerWallet);

                // Đánh dấu Phạt đã xử lý xong
                foreach (var s in cargoSurcharges)
                {
                    s.Status = SurchargeStatus.PAID;
                    s.PaidAt = DateTime.UtcNow;
                }

                // ==========================================================================
                // PHẦN C: CHUẨN BỊ DỮ LIỆU GỬI MAIL
                // ==========================================================================
                resultModel.TripId = tripId;
                resultModel.TripCode = trip.TripCode;
                resultModel.OwnerId = trip.OwnerId;
                resultModel.OwnerReceived = finalOwnerReceive;
                resultModel.ProviderPaid = trip.TotalFare;

                resultModel.Surcharges = cargoSurcharges.Select(s => new SurchargeDetail
                {
                    Type = s.Type.ToString(),
                    Amount = s.Amount,
                    Description = s.Description
                }).ToList();

                return (true, resultModel, "");
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        // =========================================================================================================
        // 4. PRIVATE HELPERS
        // =========================================================================================================

        // Helper tạo transaction nhanh
        private async Task CreateTransactionAsync(Guid walletId, Guid tripId, decimal amount, TransactionType type, string desc)
        {
            await _unitOfWork.TransactionRepo.AddAsync(new Transaction
            {
                TransactionId = Guid.NewGuid(),
                WalletId = walletId,
                TripId = tripId,
                Amount = amount,
                Type = type,
                Status = TransactionStatus.SUCCEEDED,
                Description = desc,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Helper check trách nhiệm (An toàn với Null)
        private bool IsDriverResponsible(TripDriverAssignment assign, TripSurcharge surcharge)
        {
            // Chưa lên xe hoặc không có dữ liệu thời gian lên xe -> Vô can
            if (!assign.IsOnBoard || !assign.OnBoardTime.HasValue) return false;

            DateTime incidentTime = surcharge.CreatedAt;

            // Check-in SAU khi sự cố xảy ra -> Vô can
            if (assign.OnBoardTime.Value > incidentTime) return false;

            // Check-out TRƯỚC khi sự cố xảy ra -> Vô can
            // (Chỉ tính khi đã finish và có thời gian checkout hợp lệ)
            if (assign.IsFinished && assign.OffBoardTime.HasValue && assign.OffBoardTime.Value < incidentTime)
                return false;

            return true;
        }

        // Helper gửi mail background (giữ nguyên logic cũ của bạn)
        private async Task SendCompletionEmailsBackground(Guid tripId, LiquidationResultModel resultModel)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var scopedUow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var scopedEmailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            try
            {
                var tripBase = await scopedUow.TripRepo.GetAll()
                    .Include(t => t.ShippingRoute).ThenInclude(r => r.StartLocation)
                    .Include(t => t.ShippingRoute).ThenInclude(r => r.EndLocation)
                    .Include(t => t.Vehicle)
                    .Include(t => t.TripProviderContract)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.TripId == tripId);

                if (tripBase == null) return;

                var commonReport = new TripCompletionReportModel
                {
                    TripCode = tripBase.TripCode,
                    CompletedAt = DateTime.UtcNow.AddHours(7).ToString("HH:mm dd/MM/yyyy"),
                    StartAddress = tripBase.ShippingRoute.StartLocation.Address,
                    EndAddress = tripBase.ShippingRoute.EndLocation.Address,
                    VehiclePlate = tripBase.Vehicle.PlateNumber,
                    Surcharges = resultModel.Surcharges
                };

                // 1. EMAIL PROVIDER
                var providerId = tripBase.TripProviderContract?.CounterpartyId ?? Guid.Empty;
                if (providerId != Guid.Empty)
                {
                    var provider = await scopedUow.BaseUserRepo.GetByIdAsync(providerId);
                    if (provider != null)
                    {
                        var pReport = commonReport.Clone();
                        pReport.RecipientName = provider.FullName;
                        pReport.Role = "Provider";
                        pReport.Amount = resultModel.ProviderPaid;
                        pReport.IsIncome = false;
                        pReport.FinancialDescription = "TỔNG CHI PHÍ VẬN CHUYỂN";
                        await scopedEmailService.SendTripCompletionEmailAsync(provider.Email, pReport);
                    }
                }

                // 2. EMAIL OWNER
                var owner = await scopedUow.BaseUserRepo.GetByIdAsync(resultModel.OwnerId);
                if (owner != null)
                {
                    var oReport = commonReport.Clone();
                    oReport.RecipientName = owner.FullName;
                    oReport.Role = "Owner";
                    oReport.Amount = resultModel.OwnerReceived;
                    oReport.IsIncome = true;
                    oReport.FinancialDescription = "DOANH THU THỰC NHẬN";

                    // Map driver expenses
                    foreach (var driverPay in resultModel.PaidDriversMap)
                    {
                        var d = await scopedUow.BaseUserRepo.GetByIdAsync(driverPay.Key);
                        oReport.DriverExpenses.Add(new ExpenseDetail { DriverName = d?.FullName ?? "Tài xế", Amount = driverPay.Value });
                    }
                    await scopedEmailService.SendTripCompletionEmailAsync(owner.Email, oReport);
                }

                // 3. EMAIL DRIVERS
                foreach (var driverPay in resultModel.PaidDriversMap)
                {
                    var driver = await scopedUow.BaseUserRepo.GetByIdAsync(driverPay.Key);
                    if (driver != null)
                    {
                        var dReport = commonReport.Clone();
                        dReport.RecipientName = driver.FullName;
                        dReport.Role = "Driver";
                        dReport.Amount = driverPay.Value;
                        dReport.IsIncome = driverPay.Value >= 0;
                        dReport.FinancialDescription = driverPay.Value >= 0 ? "LƯƠNG THỰC NHẬN" : "KHOẢN NỢ CẦN THANH TOÁN";
                        await scopedEmailService.SendTripCompletionEmailAsync(driver.Email, dReport);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Mail Error: {ex.Message}");
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
                DriverNames = t.DriverAssignments?.Select(da => da.Driver?.FullName ?? "N/A").ToList() ?? new List<string>(),
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