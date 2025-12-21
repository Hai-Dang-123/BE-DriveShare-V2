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
        private readonly TimeUtil _timeUtil;

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
            IUserDocumentService userDocumentService,
            TimeUtil timeUtil)
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
            _timeUtil = timeUtil;
        }

        // =========================================================================================================
        // 1. CREATE TRIP FROM POST (NHẬN CHUYẾN TỪ BÀI ĐĂNG)
        // =========================================================================================================
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
                var postPackage = await _unitOfWork.PostPackageRepo.GetAllQueryable()
                    .Include(p => p.ShippingRoute).ThenInclude(sr => sr.StartLocation)
                    .Include(p => p.ShippingRoute).ThenInclude(sr => sr.EndLocation)
                    .Include(p => p.PostContacts)
                    .Include(p => p.Provider)
                    // [CẬP NHẬT] Include thêm HandlingDetail để check tính chất hàng
                    .Include(p => p.Packages).ThenInclude(pkg => pkg.HandlingDetail)
                    .FirstOrDefaultAsync(p => p.PostPackageId == dto.PostPackageId);

                if (postPackage == null) return new ResponseDTO("Không tìm thấy Bài đăng.", 404, false);
                if (postPackage.Status != PostStatus.OPEN) return new ResponseDTO("Bài đăng này đã đóng hoặc đã được nhận.", 400, false);
                if (postPackage.ShippingRoute == null) return new ResponseDTO("Bài đăng thiếu thông tin Lộ trình.", 400, false);
                if (postPackage.Provider == null) return new ResponseDTO("Bài đăng thiếu thông tin Nhà cung cấp.", 400, false);

                // 2. VALIDATE VEHICLE
                var vehicle = await _unitOfWork.VehicleRepo.GetAll()
                    .Include(v => v.VehicleType)
                    .FirstOrDefaultAsync(v => v.VehicleId == dto.VehicleId && v.OwnerId == ownerId);

                if (vehicle == null) return new ResponseDTO("Xe không tìm thấy hoặc không thuộc về bạn.", 404, false);

                // =======================================================================
                // 2.1. CHANGE STATUS CỦA XE
                // =======================================================================

                vehicle.Status = VehicleStatus.IN_USE;
                await _unitOfWork.VehicleRepo.UpdateAsync(vehicle);

                // =======================================================================
                // [MỚI THÊM] 2.1. VALIDATE XE ĐÔNG LẠNH (Check theo yêu cầu)
                // =======================================================================
                bool requiresRefrigeration = postPackage.Packages
                    .Any(p => p.HandlingDetail != null && p.HandlingDetail.IsRefrigerated);

                if (requiresRefrigeration)
                {
                    // Kiểm tra tên loại xe có chứa từ khóa liên quan đến lạnh không
                    // (Ví dụ: "Xe tải thùng lạnh", "Xe đông lạnh")
                    bool isRefrigeratedVehicle = vehicle.VehicleType != null &&
                                                 (vehicle.VehicleType.VehicleTypeName.ToLower().Contains("lạnh") ||
                                                  vehicle.VehicleType.VehicleTypeName.ToLower().Contains("đông"));

                    if (!isRefrigeratedVehicle)
                    {
                        return new ResponseDTO("Đơn hàng yêu cầu bảo quản lạnh (đông lạnh) nhưng xe bạn chọn không phải là xe thùng lạnh.", 400, false);
                    }
                }
                // =======================================================================

                // 2.2. VALIDATE SỨC CHỨA (Đã đổi số thứ tự từ 2.1 sang 2.2)
                var capacityCheck = ValidateVehicleCapacity(vehicle, postPackage.Packages);
                if (!capacityCheck.IsSuccess)
                {
                    return capacityCheck;
                }

                // 3. CHECK SCHEDULE CONFLICT
                // Lưu ý: Check dựa trên thời gian của Route gốc
                await ValidateVehicleScheduleAsync(dto.VehicleId, postPackage.ShippingRoute);

                // =======================================================================
                // [FIX QUAN TRỌNG] 3.5. CLONE SHIPPING ROUTE
                // Để tránh lỗi 1-1 Constraint khi Trip cũ đã bị hủy nhưng vẫn giữ Route cũ
                // =======================================================================
                var originalRoute = postPackage.ShippingRoute;

                var clonedShippingRoute = new ShippingRoute
                {
                    ShippingRouteId = Guid.NewGuid(),

                    // Clone Value Objects (Tạo instance mới để tránh reference cũ)
                    StartLocation = new Common.ValueObjects.Location(
                        originalRoute.StartLocation.Address,
                        originalRoute.StartLocation.Latitude ?? 0,
                        originalRoute.StartLocation.Longitude ?? 0),

                    EndLocation = new Common.ValueObjects.Location(
                        originalRoute.EndLocation.Address,
                        originalRoute.EndLocation.Latitude ?? 0,
                        originalRoute.EndLocation.Longitude ?? 0),

                    // Copy Data
                    ExpectedPickupDate = originalRoute.ExpectedPickupDate,
                    ExpectedDeliveryDate = originalRoute.ExpectedDeliveryDate,

                    // Clone TimeWindow
                    PickupTimeWindow = new Common.ValueObjects.TimeWindow(
                        originalRoute.PickupTimeWindow?.StartTime,
                        originalRoute.PickupTimeWindow?.EndTime),

                    DeliveryTimeWindow = new Common.ValueObjects.TimeWindow(
                        originalRoute.DeliveryTimeWindow?.StartTime,
                        originalRoute.DeliveryTimeWindow?.EndTime),

                    // Copy Stats
                    EstimatedDistanceKm = originalRoute.EstimatedDistanceKm,
                    EstimatedDurationHours = originalRoute.EstimatedDurationHours,
                    TravelTimeHours = originalRoute.TravelTimeHours,
                    WaitTimeHours = originalRoute.WaitTimeHours,
                    RestrictionNote = originalRoute.RestrictionNote
                };

                // Lưu Route mới vào DB (Cần có Repo cho ShippingRoute, hoặc Add qua Generic)
                await _unitOfWork.ShippingRouteRepo.AddAsync(clonedShippingRoute);
                // =======================================================================

                // 4. TẠO TRIP ROUTE (GỌI VIETMAP)
                // Dùng clonedRoute để tính toán và liên kết
                var newTripRoute = await _tripRouteService.CreateAndAddTripRouteAsync(clonedShippingRoute, vehicle);
                
                // 5. TẠO TRIP
                var trip = new Trip
                {
                    TripId = Guid.NewGuid(),
                    TripCode = GenerateTripCode(),
                    Status = TripStatus.AWAITING_OWNER_CONTRACT,
                    Type = TripType.FROM_PROVIDER,
                    CreateAt = TimeUtil.NowVN(),
                    UpdateAt = TimeUtil.NowVN(),
                    VehicleId = dto.VehicleId,
                    OwnerId = ownerId,
                    TripRouteId = newTripRoute.TripRouteId,

                    // [QUAN TRỌNG]: Dùng ID của Route mới vừa Clone, KHÔNG dùng postPackage.ShippingRouteId
                    ShippingRouteId = clonedShippingRoute.ShippingRouteId,

                    TotalFare = postPackage.OfferedPrice,
                    ActualDistanceKm = newTripRoute.DistanceKm,
                    ActualDuration = newTripRoute.Duration,
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
                postPackage.Updated = TimeUtil.NowVN();
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
                //if (_userUtility.GetUserRoleFromToken() != "Admin" ) return new ResponseDTO("Forbidden.", 403, false);

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

        // --- Get Detail By ID (Optimized High Performance) ---
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

                    LiquidationReportJson = trip.LiquidationReportJson,



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

                        AssignmentId = d.TripDriverAssignmentId,

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
                // 1. Lấy thông tin Trip kèm ShippingRoute
                var trip = await _unitOfWork.TripRepo.GetAll()
                    .Include(t => t.DriverAssignments)
                    .Include(t => t.ShippingRoute) // Quan trọng: Cần Include để lấy dữ liệu quãng đường
                    .AsNoTracking()
                    .FirstOrDefaultAsync(t => t.TripId == tripId);

                if (trip == null) return new ResponseDTO("Trip not found", 404, false);

                // 2. Lấy dữ liệu từ Trip và ShippingRoute
                // Ưu tiên lấy từ ShippingRoute (dữ liệu dự tính chuẩn), nếu không có thì fallback sang Actual (thực tế)

                double distance = (trip.ShippingRoute?.EstimatedDistanceKm > 0)
                    ? trip.ShippingRoute.EstimatedDistanceKm
                    : (double)trip.ActualDistanceKm;

                double rawDrivingHours = (trip.ShippingRoute?.EstimatedDurationHours > 0)
                    ? trip.ShippingRoute.EstimatedDurationHours
                    : trip.ActualDuration.TotalHours;

                // [QUAN TRỌNG] FIX LOGIC: Kẹp tốc độ trần (Speed Clamping)
                // Đồng bộ logic với GetPostPackageDetailsAsync để tránh việc tính ra 27 tiếng cho HN-SG
                if (distance > 0 && rawDrivingHours > 0)
                {
                    double avgSpeed = distance / rawDrivingHours;
                    const double REALISTIC_TRUCK_SPEED = 50.0; // Tốc độ trung bình xe tải Bắc Nam (50km/h)

                    // Nếu vận tốc > 55km/h -> Dữ liệu đang là của xe con hoặc lý thuyết
                    if (avgSpeed > 55.0)
                    {
                        // Tính lại giờ lái thực tế
                        rawDrivingHours = distance / REALISTIC_TRUCK_SPEED;
                    }
                }
                else if (distance > 0 && rawDrivingHours <= 0)
                {
                    // Fallback: Nếu không có thời gian, tự tính theo tốc độ 50km/h
                    rawDrivingHours = distance / 50.0;
                }

                // [FIX LỖI] Lấy WaitTime & Tính Buffer
                // Dùng toán tử ?. và ?? để handle null an toàn
                double waitTimeHours = trip.ShippingRoute?.WaitTimeHours ?? 0;

                // Tính Buffer: 15% thời gian lái + 30 phút rủi ro (Đồng bộ logic)
                double bufferHours = (rawDrivingHours * 0.15) + 0.5;

                // 3. Gọi Helper tính toán
                // rawDrivingHours lúc này đã là con số thực tế (ví dụ ~34h cho HN-SG)
                var suggestion = TripCalculationHelper.CalculateScenarios(
                    distance,
                    rawDrivingHours,
                    waitTimeHours,
                    bufferHours,
                    trip.ShippingRoute?.ExpectedPickupDate ?? TimeUtil.NowVN(),
                    trip.ShippingRoute?.ExpectedDeliveryDate ?? TimeUtil.NowVN().AddHours(24)
                );

                // 4. Xác định Số lượng tài & Giờ lái yêu cầu
                int targetDrivers = 1;
                string mode = suggestion.SystemRecommendation;
                double requiredHoursPerDriver = 0;

                switch (mode)
                {
                    case "SOLO":
                        targetDrivers = 1;
                        requiredHoursPerDriver = suggestion.SoloScenario.DrivingHoursPerDriver;
                        break;
                    case "TEAM":
                        targetDrivers = 2;
                        requiredHoursPerDriver = suggestion.TeamScenario.DrivingHoursPerDriver;
                        break;
                    case "EXPRESS":
                        targetDrivers = 3;
                        requiredHoursPerDriver = suggestion.ExpressScenario.DrivingHoursPerDriver;
                        break;
                    default: // IMPOSSIBLE hoặc khác
                        targetDrivers = 2;
                        mode = "TEAM (Overdue)";
                        requiredHoursPerDriver = suggestion.TeamScenario.DrivingHoursPerDriver;
                        break;
                }

                // 5. Kiểm tra hiện trạng
                var assignedList = trip.DriverAssignments
                    .Where(a => a.AssignmentStatus == AssignmentStatus.ACCEPTED)
                    .ToList();

                int remaining = targetDrivers - assignedList.Count;
                if (remaining < 0) remaining = 0;

                // 6. Đóng gói kết quả
                var analysis = new TripDriverAnalysisDTO
                {
                    Suggestion = suggestion,
                    TotalAssigned = assignedList.Count,
                    HasMainDriver = assignedList.Any(a => a.Type == DriverType.PRIMARY),
                    AssistantCount = assignedList.Count(a => a.Type == DriverType.SECONDARY),
                    RemainingSlots = remaining,
                    DrivingHoursRequired = requiredHoursPerDriver,
                    Recommendation = remaining == 0
                        ? "Đã đủ tài xế."
                        : $"Cần tuyển thêm {remaining} tài xế ({mode}). Điều kiện: Còn dư ít nhất {requiredHoursPerDriver:F1} giờ lái/tuần."
                };

                return new ResponseDTO("Success", 200, true, analysis);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Lỗi phân tích chuyến: {ex.Message}", 500, false);
            }
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


        // =========================================================================
        // 2. THAY ĐỔI TRẠNG THÁI CHUYẾN ĐI (ChangeTripStatus)
        // =========================================================================
        public async Task<ResponseDTO> ChangeTripStatusAsync(ChangeTripStatusDTO dto)
        {
            // Bắt đầu Transaction để đảm bảo tính toàn vẹn dữ liệu (Tiền + Trạng thái)
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                // 1. Load Trip (Include đủ để tránh query nhiều lần)
                // 1. Load Trip (CẬP NHẬT: Include Package và Item để update status)
                var trip = await _unitOfWork.TripRepo.GetAll()
                    .Include(t => t.Packages).ThenInclude(p => p.Item) // <--- QUAN TRỌNG: Load gói hàng kèm theo
                    .FirstOrDefaultAsync(t => t.TripId == dto.TripId);
                if (trip == null) return new ResponseDTO("Không tìm thấy chuyến đi.", 404, false);

                // --- VALIDATE ---
                if (trip.Status == TripStatus.COMPLETED || trip.Status == TripStatus.CANCELLED)
                    return new ResponseDTO("Chuyến đi đã kết thúc, không thể thay đổi trạng thái.", 400, false);

                // =====================================================================
                // CASE A: TRẠNG THÁI THÔNG THƯỜNG (LOADING, UNLOADING...)
                // =====================================================================
                if (dto.NewStatus != TripStatus.COMPLETED)
                {
                    trip.Status = dto.NewStatus;
                    trip.UpdateAt = TimeUtil.NowVN();

                    // Logic phụ: Ghi nhận thời gian Pickup/Dropoff thực tế
                    if (dto.NewStatus == TripStatus.LOADING && trip.ActualPickupTime == null)
                    {
                        trip.ActualPickupTime = TimeUtil.NowVN();
                        // Hàm gửi link ký tên (nếu có)
                         await SendSignatureLinkAsync(trip.TripId, DeliveryRecordType.PICKUP);
                    }
                    else if (dto.NewStatus == TripStatus.UNLOADING)
                    {
                        // Hàm gửi link ký tên (nếu có)
                         await SendSignatureLinkAsync(trip.TripId, DeliveryRecordType.DROPOFF);
                    }

                    await _unitOfWork.TripRepo.UpdateAsync(trip);
                    await _unitOfWork.SaveChangeAsync();
                    await transaction.CommitAsync();

                    return new ResponseDTO($"Đã chuyển trạng thái sang {dto.NewStatus}.", 200, true);
                }

                // =====================================================================
                // CASE B: HOÀN TẤT CHUYẾN ĐI (COMPLETED) - XỬ LÝ TIỀN NONG
                // =====================================================================

                // [BƯỚC 1]: AUTO CHECK-OUT CHO TÀI XẾ (Nếu quên checkout)
                var activeAssignments = await _unitOfWork.TripDriverAssignmentRepo.GetAll()
                    .Where(a => a.TripId == trip.TripId && !a.IsFinished)
                    .ToListAsync();

                if (activeAssignments.Any())
                {
                    foreach (var assign in activeAssignments)
                    {
                        assign.IsFinished = true; // Kết thúc phân công
                        assign.AssignmentStatus = AssignmentStatus.COMPLETED;

                        // Chỉ set thông tin xuống xe nếu họ thực sự ĐÃ LÊN XE
                        if (assign.IsOnBoard)
                        {
                            assign.OffBoardTime = TimeUtil.NowVN();
                            assign.OffBoardLocation = "Auto-checkout (Trip Completed)";
                            assign.CheckOutNote = "Hệ thống tự động check-out.";
                        }
                        else
                        {
                            // Nếu chưa lên xe mà chuyến đã xong -> Đánh dấu note
                            assign.CheckOutNote = "Kết thúc chuyến khi chưa Check-in (Không trả lương).";
                        }

                        await _unitOfWork.TripDriverAssignmentRepo.UpdateAsync(assign);
                    }
                    // Save ngay để hàm tính tiền bên dưới thấy được assignment đã completed
                    await _unitOfWork.SaveChangeAsync();
                }

                // [BƯỚC 2]: THANH LÝ HỢP ĐỒNG (Tính toán ví Owner, Driver, Provider)
                var liquidationResult = await ProcessTripLiquidationAsync(trip.TripId);

                if (!liquidationResult.Success)
                {
                    await transaction.RollbackAsync();
                    return new ResponseDTO($"Lỗi thanh lý hợp đồng: {liquidationResult.Message}", 500, false);
                }

                // [QUAN TRỌNG] Serialize kết quả báo cáo thành JSON và lưu vào Trip
                var reportData = liquidationResult.Result;
                trip.LiquidationReportJson = System.Text.Json.JsonSerializer.Serialize(reportData);


                // [BƯỚC 3 - MỚI]: CẬP NHẬT TRẠNG THÁI PACKAGE VÀ ITEM -> COMPLETED
                if (trip.Packages != null && trip.Packages.Any())
                {
                    foreach (var pkg in trip.Packages)
                    {
                        // Update Package
                        pkg.Status = PackageStatus.COMPLETED; // Đảm bảo Enum PackageStatus có giá trị này
                        pkg.UpdatedAt = TimeUtil.NowVN();

                        // Update Item bên trong (nếu có)
                        if (pkg.Item != null)
                        {
                            pkg.Item.Status = ItemStatus.COMPLETED; // Đảm bảo Enum ItemStatus có giá trị này
                            //pkg.Item.UpdatedAt = DateTime.UtcNow;

                            // Cập nhật ItemRepo (nếu Entity Framework không tự track cascade)
                            await _unitOfWork.ItemRepo.UpdateAsync(pkg.Item);
                        }

                        // Cập nhật PackageRepo
                        await _unitOfWork.PackageRepo.UpdateAsync(pkg);
                    }
                }

                // [BƯỚC 3]: CẬP NHẬT TRẠNG THÁI TRIP CUỐI CÙNG
                trip.Status = TripStatus.COMPLETED;
                trip.ActualCompletedTime = TimeUtil.NowVN();
                trip.UpdateAt = TimeUtil.NowVN();

                await _unitOfWork.TripRepo.UpdateAsync(trip);

                // CHANGE STATUS VEHICLE TRỞ LẠI ACTIVE
                var vehicle = await _unitOfWork.VehicleRepo.GetByIdAsync(trip.VehicleId);
                if (vehicle != null)
                {
                    vehicle.Status = VehicleStatus.ACTIVE;
                    await _unitOfWork.VehicleRepo.UpdateAsync(vehicle);
                }

                // Lưu toàn bộ thay đổi (Trip + Wallet + Transaction)
                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync();

                // [BƯỚC 4]: GỬI EMAIL (Chạy ngầm, không block response)
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
        // 3. CORE LOGIC: TÍNH TOÁN TIỀN NONG & QUYẾT TOÁN (LIQUIDATION)
        // =========================================================================================================
        private async Task<(bool Success, LiquidationResultModel Result, string Message)> ProcessTripLiquidationAsync(Guid tripId)
        {
            // CẤU HÌNH HARDCODE ID ADMIN (Dùng để hứng tiền phí sàn)
            var SystemAdminId = Guid.Parse("D4DAB1C3-6D48-4B23-8369-2D1C9C828F22");

            var result = new LiquidationResultModel();
            try
            {
                // --------------------------------------------------------------------------
                // 1. LOAD DATA
                // --------------------------------------------------------------------------
                var trip = await _unitOfWork.TripRepo.GetAll()
                    .Include(t => t.ShippingRoute)
                    .Include(t => t.TripProviderContract)
                    .Include(t => t.Packages)
                    .FirstOrDefaultAsync(t => t.TripId == tripId);

                if (trip == null) return (false, null, "Trip not found");

                result.TripId = trip.TripId;
                result.TripCode = trip.TripCode;

                // --------------------------------------------------------------------------
                // 2. LẤY WALLET & USER (Provider, Owner, ADMIN)
                // --------------------------------------------------------------------------
                Guid providerId = trip.TripProviderContract?.CounterpartyId ?? trip.Packages.FirstOrDefault()?.ProviderId ?? Guid.Empty;
                var providerUser = await _unitOfWork.BaseUserRepo.GetByIdAsync(providerId);
                var providerWallet = await _unitOfWork.WalletRepo.FirstOrDefaultAsync(w => w.UserId == providerId);

                var ownerUser = await _unitOfWork.BaseUserRepo.GetByIdAsync(trip.OwnerId);
                var ownerWallet = await _unitOfWork.WalletRepo.FirstOrDefaultAsync(w => w.UserId == trip.OwnerId);

                // Ví Admin Hệ Thống
                var adminWallet = await _unitOfWork.WalletRepo.FirstOrDefaultAsync(w => w.UserId == SystemAdminId);

                if (providerWallet == null || ownerWallet == null) return (false, null, "Lỗi: Không tìm thấy ví Provider hoặc Owner.");

                var ownerReport = new ParticipantFinancialReport { UserId = trip.OwnerId, FullName = ownerUser.FullName, Email = ownerUser.Email, Role = "Owner" };
                var providerReport = new ParticipantFinancialReport { UserId = providerId, FullName = providerUser.FullName, Email = providerUser.Email, Role = "Provider" };

                // --------------------------------------------------------------------------
                // 3. TÍNH TOÁN PHẠT (SURCHARGES)
                // --------------------------------------------------------------------------
                var allSurcharges = await _unitOfWork.TripSurchargeRepo.GetAll()
                    .Where(s => s.TripId == tripId && s.Status == SurchargeStatus.PENDING)
                    .ToListAsync();

                decimal totalAmountDueToProvider = allSurcharges.Where(s => IsSurchargeForProvider(s.Type)).Sum(s => s.Amount);

                decimal collectedForProvider = 0;
                decimal collectedForOwner = 0;

                // ==========================================================================
                // PHẦN A: TÍNH CHO DRIVER (QUAN TRỌNG: CHECK IS ON BOARD)
                // ==========================================================================
                var assignments = await _unitOfWork.TripDriverAssignmentRepo.GetAll()
                    .Where(a => a.TripId == tripId && a.AssignmentStatus == AssignmentStatus.COMPLETED)
                    .Include(a => a.Driver)
                    .ToListAsync();

                decimal totalSalaryAndBonusExpense = 0;

                foreach (var assign in assignments)
                {
                    var driverWallet = await _unitOfWork.WalletRepo.FirstOrDefaultAsync(w => w.UserId == assign.DriverId);
                    if (driverWallet == null) continue;

                    var dReport = new ParticipantFinancialReport
                    {
                        UserId = assign.DriverId,
                        FullName = assign.Driver?.FullName ?? "Tài xế",
                        Email = assign.Driver?.Email,
                        Role = "Tài xế"
                    };

                    // [LOGIC MỚI] VALIDATE CHECK-IN
                    // Nếu chưa Check-in (IsOnBoard == false) -> Lương = 0, Không chịu phạt
                    // ... Bên trong foreach (var assign in assignments) ...

                    // [LOGIC MỚI] VALIDATE CHECK-IN
                    if (!assign.IsOnBoard)
                    {
                        // 1. BÁO CÁO LƯƠNG = 0
                        dReport.AddItem("⚠️ Không Check-in (Không trả lương)", 0, false);
                        assign.PaymentStatus = DriverPaymentStatus.UN_PAID;

                        // 2. XỬ LÝ TIỀN CỌC (FIX: Tịch thu cọc chuyển cho Owner)
                        if (assign.DepositStatus == DepositStatus.DEPOSITED && assign.DepositAmount > 0)
                        {
                            decimal penaltyAmount = assign.DepositAmount;

                            // A. Cộng tiền vào ví Owner (Bồi thường)
                            ownerWallet.Balance += penaltyAmount;

                            // B. Tạo Transaction cho Owner
                            await CreateIncomeTransactionAsync(ownerWallet, tripId, penaltyAmount,
                                TransactionType.COMPENSATION,
                                $"Nhận phạt cọc từ {assign.Driver?.FullName ?? "Tài xế"} (Bỏ chuyến)");

                            // C. Cập nhật trạng thái cọc -> TỊCH THU
                            assign.DepositStatus = DepositStatus.SEIZED; // Cần đảm bảo Enum có trạng thái này
                            //assign.Note = "Tài xế không check-in. Cọc đã chuyển cho Owner.";

                            // D. Cập nhật biến tổng để hiển thị trong Report của Owner (Phần Thu phạt nội bộ)
                            collectedForOwner += penaltyAmount;

                            // E. Log vào Report của Driver để họ thấy
                            dReport.AddItem("⛔ Bị tịch thu cọc (Bỏ chuyến)", penaltyAmount, true);

                            // Trừ tiền hiển thị trong report (thực tế tiền đã nằm ở hệ thống/ví driver từ trước, giờ chỉ là đổi chủ)
                            dReport.FinalWalletChange = -penaltyAmount;
                        }
                        else
                        {
                            dReport.FinalWalletChange = 0;
                        }

                        result.DriverReports.Add(dReport);
                        continue; // -> BỎ QUA CÁC BƯỚC TÍNH LƯƠNG BÊN DƯỚI, SANG TÀI XẾ KHÁC
                    }

                    // --- NẾU ĐÃ CHECK-IN THÌ TÍNH TOÁN BÌNH THƯỜNG ---

                    // A1. Thu nhập (Lương + Thưởng)
                    decimal totalIncome = assign.BaseAmount + (assign.BonusAmount ?? 0);
                    totalSalaryAndBonusExpense += totalIncome;
                    dReport.AddItem("Lương & Thưởng", totalIncome, false);

                    // A2. Tính Phạt (Chỉ phạt nếu tài xế CÓ MẶT trên xe)
                    decimal payToProvider = 0;
                    decimal payToOwner = 0;

                    foreach (var sur in allSurcharges)
                    {
                        // Chỉ tính phạt nếu tài xế chịu trách nhiệm
                        if (IsDriverResponsible(assign, sur))
                        {
                            // Đếm số lượng tài xế chịu trách nhiệm VÀ CÓ check-in (Công bằng)
                            int responsibleCount = assignments.Count(a => IsDriverResponsible(a, sur) && a.IsOnBoard);

                            // Tránh chia cho 0
                            decimal share = sur.Amount / (responsibleCount > 0 ? responsibleCount : 1);

                            if (IsSurchargeForProvider(sur.Type))
                            {
                                payToProvider += share;
                                dReport.AddItem($"Phạt Hàng hóa: {sur.Description}", share, true);
                            }
                            else
                            {
                                payToOwner += share;
                                dReport.AddItem($"Phạt Xe/Khác: {sur.Description}", share, true);
                            }
                        }
                    }

                    // A3. TRỪ TIỀN PHẠT (Dùng Helper Ghi Nợ nếu thiếu)
                    if (payToProvider > 0)
                    {
                        await DeductMoneyWithDebtLogAsync(driverWallet, tripId, payToProvider, $"Bồi thường hàng hóa (Trip {trip.TripCode})");

                        providerWallet.Balance += payToProvider;
                        collectedForProvider += payToProvider;
                        await CreateIncomeTransactionAsync(providerWallet, tripId, payToProvider, TransactionType.COMPENSATION, $"Nhận bồi thường từ {dReport.FullName}");
                    }

                    if (payToOwner > 0)
                    {
                        await DeductMoneyWithDebtLogAsync(driverWallet, tripId, payToOwner, $"Bồi thường hư xe (Trip {trip.TripCode})");

                        ownerWallet.Balance += payToOwner;
                        collectedForOwner += payToOwner;
                        await CreateIncomeTransactionAsync(ownerWallet, tripId, payToOwner, TransactionType.COMPENSATION, $"Nhận bồi thường từ {dReport.FullName}");
                    }

                    // A4. Hoàn cọc (nếu có)
                    decimal refundAmount = 0;
                    if (assign.DepositStatus == DepositStatus.DEPOSITED && assign.DepositAmount > 0)
                    {
                        refundAmount = assign.DepositAmount;
                        assign.DepositStatus = DepositStatus.REFUNDED;
                        dReport.AddItem("Hoàn cọc", refundAmount, false);

                        driverWallet.Balance += refundAmount;
                        await CreateIncomeTransactionAsync(driverWallet, tripId, refundAmount, TransactionType.REFUND, "Hoàn cọc");
                    }

                    // A5. Cộng Lương (Sau khi đã trừ phạt xong xuôi)
                    if (totalIncome > 0)
                    {
                        driverWallet.Balance += totalIncome;
                        await CreateIncomeTransactionAsync(driverWallet, tripId, totalIncome, TransactionType.DRIVER_PAYOUT, "Thanh toán lương");
                    }

                    assign.PaymentStatus = DriverPaymentStatus.PAID;
                    dReport.FinalWalletChange = totalIncome + refundAmount - (payToProvider + payToOwner);
                    result.DriverReports.Add(dReport);
                }

                // ==========================================================================
                // PHẦN B: TÍNH CHO OWNER & ADMIN (PLATFORM FEE)
                // ==========================================================================

                // B1. Cộng Doanh thu & Tính Phí Sàn
                decimal originalFare = trip.TotalFare;
                decimal platformFee = 0;

                if (originalFare > 0)
                {
                    platformFee = originalFare * 0.10m; // 10% phí sàn
                }

                decimal netRevenue = originalFare - platformFee;

                // Log Report Owner
                ownerReport.AddItem("Doanh thu chuyến", originalFare, false);

                if (platformFee > 0)
                {
                    ownerReport.AddItem("Phí sàn (10%)", platformFee, true);
                    // CỘNG TIỀN VÀO VÍ ADMIN
                    if (adminWallet != null)
                    {
                        adminWallet.Balance += platformFee;
                        await CreateIncomeTransactionAsync(adminWallet, tripId, platformFee, TransactionType.PLATFORM_PAYMENT, $"Phí sàn 10% - {trip.TripCode}");
                    }
                }

                // Cộng tiền thực nhận vào ví Owner
                ownerWallet.Balance += netRevenue;
                await CreateIncomeTransactionAsync(ownerWallet, tripId, netRevenue, TransactionType.OWNER_PAYOUT, "Doanh thu vận hành (đã trừ phí)");

                // B2. Trừ Lương Tài xế (Chỉ trừ phần lương của những người ĐÃ Check-in)
                if (totalSalaryAndBonusExpense > 0)
                {
                    ownerReport.AddItem("Chi phí lương", totalSalaryAndBonusExpense, true);
                    await DeductMoneyWithDebtLogAsync(ownerWallet, tripId, totalSalaryAndBonusExpense, "Thanh toán lương tài xế");
                }

                // B3. Bù lỗ cho Provider (Nếu không tìm được tài xế chịu trách nhiệm)
                decimal gap = totalAmountDueToProvider - collectedForProvider;
                if (gap > 0)
                {
                    await DeductMoneyWithDebtLogAsync(ownerWallet, tripId, gap, "Bù lỗ bồi thường hàng hóa");

                    providerWallet.Balance += gap;
                    await CreateIncomeTransactionAsync(providerWallet, tripId, gap, TransactionType.COMPENSATION, "Nhận bồi thường (Owner bù)");

                    ownerReport.AddItem("Bù lỗ hàng hóa", gap, true);
                }

                // B4. Log tiền phạt nội bộ nhận được (Chỉ report)
                if (collectedForOwner > 0) ownerReport.AddItem("Thu phạt nội bộ", collectedForOwner, false);

                ownerReport.FinalWalletChange = netRevenue - totalSalaryAndBonusExpense - (gap > 0 ? gap : 0) + collectedForOwner;

                // ==========================================================================
                // PHẦN C: TÍNH CHO PROVIDER
                // ==========================================================================
                providerReport.AddItem("Thanh toán cước", trip.TotalFare, true); // Report only

                decimal totalCompReceived = collectedForProvider + (gap > 0 ? gap : 0);
                if (totalCompReceived > 0) providerReport.AddItem("Nhận bồi thường", totalCompReceived, false);

                providerReport.FinalWalletChange = totalCompReceived - trip.TotalFare;

                // ==========================================================================
                // FINAL SAVE
                // ==========================================================================
                foreach (var s in allSurcharges) { s.Status = SurchargeStatus.PAID; s.PaidAt = TimeUtil.NowVN(); }

                await _unitOfWork.WalletRepo.UpdateAsync(ownerWallet);
                await _unitOfWork.WalletRepo.UpdateAsync(providerWallet);
                if (adminWallet != null) await _unitOfWork.WalletRepo.UpdateAsync(adminWallet);

                result.OwnerReport = ownerReport;
                result.ProviderReport = providerReport;

                return (true, result, "Thành công.");
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        // -----------------------------------------------------------
        // HELPER METHODS
        // -----------------------------------------------------------
        private bool IsSurchargeForProvider(SurchargeType type)
        {
            // Chỉ những lỗi này tiền mới về Provider
            return type == SurchargeType.CARGO_DAMAGE ||
                   type == SurchargeType.CARGO_LOSS ||
                   type == SurchargeType.LATE_DELIVERY ||
                   type == SurchargeType.MISDELIVERY;
        }

        private bool IsDriverResponsible(TripDriverAssignment assign, TripSurcharge surcharge)
        {
            if (!assign.IsOnBoard || !assign.OnBoardTime.HasValue) return false;
            DateTime incidentTime = surcharge.CreatedAt;

            if (assign.OnBoardTime.Value > incidentTime) return false;

            // [TÀI PHỤ] Check-out trước khi sự cố xảy ra -> Vô can
            if (assign.IsFinished && assign.OffBoardTime.HasValue)
            {
                if (assign.OffBoardTime.Value < incidentTime) return false;
            }
            return true;
        }

        //private async Task CreateTransactionAsync(Wallet wallet, Guid tripId, decimal amount, TransactionType type, string desc)
        //{
        //    // 1. Ghi nhận Balance Before
        //    decimal balanceBefore = wallet.Balance;

        //    // 2. Cập nhật Balance mới
        //    wallet.Balance += amount;
        //    wallet.LastUpdatedAt = DateTime.UtcNow;

        //    // 3. Ghi nhận Balance After
        //    decimal balanceAfter = wallet.Balance;

        //    await _unitOfWork.TransactionRepo.AddAsync(new Transaction
        //    {
        //        TransactionId = Guid.NewGuid(),
        //        WalletId = wallet.WalletId,
        //        TripId = tripId,
        //        Amount = amount,
        //        Type = type,
        //        Status = TransactionStatus.SUCCEEDED,
        //        Description = desc,
        //        CreatedAt = DateTime.UtcNow,
        //        BalanceAfter = balanceAfter,
        //        BalanceBefore = balanceBefore
        //    });
        //}

        // Helper 1: Trừ tiền (Cho phép âm -> Ghi nợ OUTSTANDING_PAYMENT)
        private async Task DeductMoneyWithDebtLogAsync(Wallet wallet, Guid tripId, decimal amount, string description)
        {
            if (amount <= 0) return;

            decimal balanceBefore = wallet.Balance;
            bool isDebt = wallet.Balance < amount;

            // Trừ tiền (Cho phép số dư âm)
            wallet.Balance -= amount;
            wallet.LastUpdatedAt = TimeUtil.NowVN();

            TransactionType type;
            string finalDesc;

            if (isDebt)
            {
                type = TransactionType.OUTSTANDING_PAYMENT;
                decimal available = balanceBefore > 0 ? balanceBefore : 0;
                decimal debtAmount = amount - available;
                finalDesc = $"[GHI NỢ] {description} (Ví có: {available:N0}, Nợ thêm: {debtAmount:N0})";
            }
            else
            {
                type = TransactionType.COMPENSATION;
                finalDesc = description;
            }

            await _unitOfWork.TransactionRepo.AddAsync(new Transaction
            {
                TransactionId = Guid.NewGuid(),
                WalletId = wallet.WalletId,
                TripId = tripId,
                Amount = -amount,
                Type = type,
                Status = TransactionStatus.SUCCEEDED,
                Description = finalDesc,
                CreatedAt = TimeUtil.NowVN(),
                BalanceBefore = balanceBefore,
                BalanceAfter = wallet.Balance
            });
        }

        // Helper 2: Cộng tiền (Income - Không bao giờ nợ)
        private async Task CreateIncomeTransactionAsync(Wallet wallet, Guid tripId, decimal amount, TransactionType type, string desc)
        {
            if (amount <= 0) return;

            // Giả định wallet.Balance đã được += amount ở bên ngoài hàm này (theo code cũ của bạn)
            // Hoặc ta += luôn trong này cho an toàn. 
            // Tuy nhiên theo luồng code của bạn thì bạn hay += ở ngoài.
            // Để an toàn, tôi sẽ sửa lại logic: += ở ngoài, hàm này chỉ log transaction.

            // Logic tính ngược BalanceBefore
            decimal balanceBefore = wallet.Balance - amount;

            await _unitOfWork.TransactionRepo.AddAsync(new Transaction
            {
                TransactionId = Guid.NewGuid(),
                WalletId = wallet.WalletId,
                TripId = tripId,
                Amount = amount,
                Type = type,
                Status = TransactionStatus.SUCCEEDED,
                Description = desc,
                CreatedAt = TimeUtil.NowVN(),
                BalanceBefore = balanceBefore,
                BalanceAfter = wallet.Balance
            });
        }

        // Helper gửi mail background (giữ nguyên logic cũ của bạn)
        public async Task SendCompletionEmailsBackground(Guid tripId, LiquidationResultModel result)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            // Gửi Owner
            await emailService.SendTripLiquidationEmailAsync(result.OwnerReport, result.TripCode);

            // Gửi Provider
            await emailService.SendTripLiquidationEmailAsync(result.ProviderReport, result.TripCode);

            // Gửi từng Driver
            foreach (var dReport in result.DriverReports)
            {
                await emailService.SendTripLiquidationEmailAsync(dReport, result.TripCode);
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

        // =========================================================================================================
        // 4. CANCEL TRIP BY OWNER (HỦY CHUYẾN & BỒI THƯỜNG)
        // =========================================================================================================

        // =========================================================================================================
        // 4. CANCEL TRIP BY OWNER (HỦY CHUYẾN & BỒI THƯỜNG)
        // =========================================================================================================
        public async Task<ResponseDTO> CancelTripByOwnerAsync(CancelTripDTO dto)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                // 1. Validate & Load Data (Include Packages để truy ngược về Post)
                var ownerId = _userUtility.GetUserIdFromToken();
                var trip = await _unitOfWork.TripRepo.GetAll()
                    .Include(t => t.ShippingRoute)
                    .Include(t => t.TripProviderContract)
                    .Include(t => t.DriverAssignments) // Load assignment để check
                    .Include(t => t.Owner)
                    .Include(t => t.Packages)
                    .Include(t => t.Vehicle)
                    .FirstOrDefaultAsync(t => t.TripId == dto.TripId);

                if (trip == null) return new ResponseDTO("Không tìm thấy chuyến đi.", 404, false);
                if (trip.OwnerId != ownerId) return new ResponseDTO("Bạn không có quyền hủy chuyến đi này.", 403, false);

                // =======================================================================
                // [MỚI] 1.5. VALIDATE DRIVER ASSIGNMENT
                // Nếu đã có tài xế được gán (Chưa hoàn thành, chưa hủy, chưa từ chối) -> CHẶN
                // =======================================================================
                bool hasActiveDriver = trip.DriverAssignments.Any(da =>
                    !da.IsFinished &&
                    da.AssignmentStatus != AssignmentStatus.CANCELLED);

                if (hasActiveDriver)
                {
                    return new ResponseDTO("Không thể hủy chuyến vì đang có Tài xế được gán. Vui lòng gỡ Tài xế trước khi hủy.", 400, false);
                }
                // =======================================================================

                // 2. Check Status
                var allowedStatuses = new List<TripStatus>
                {
                    TripStatus.AWAITING_PROVIDER_CONTRACT,
                    TripStatus.AWAITING_PROVIDER_PAYMENT,
                    TripStatus.PENDING_DRIVER_ASSIGNMENT,
                    TripStatus.AWAITING_OWNER_CONTRACT,

                    // Đang tìm tài, chưa có tài -> OK
                    // Lưu ý: Các trạng thái như DONE_ASSIGNING_DRIVER thường đã có tài xế, 
                    // nên sẽ bị chặn bởi logic check ở bước 1.5 bên trên.
                };

                if (!allowedStatuses.Contains(trip.Status))
                {
                    // Nếu status là DONE_ASSIGNING_DRIVER hoặc READY_FOR_HANDOVER mà không có tài active (hiếm gặp), code vẫn chạy tiếp.
                    // Nhưng thường sẽ bị chặn ở bước 1.5 hoặc ở đây.
                    // Cho phép mềm dẻo: Nếu status không nằm trong list này nhưng cũng không có tài xế thì vẫn xem xét logic dưới.
                    // Tuy nhiên để an toàn, giữ nguyên check status chặt chẽ.
                    if (trip.Status == TripStatus.DONE_ASSIGNING_DRIVER || trip.Status == TripStatus.READY_FOR_VEHICLE_HANDOVER)
                    {
                        // Case đặc biệt: Status báo đã gán, nhưng check Assignments lại rỗng (dữ liệu lỗi), cho phép hủy để cleanup.
                        if (hasActiveDriver) return new ResponseDTO($"Không thể hủy ở trạng thái {trip.Status}.", 400, false);
                    }
                    else
                    {
                        return new ResponseDTO($"Không thể hủy chuyến ở trạng thái {trip.Status}.", 400, false);
                    }
                }

                // 3. Tính toán Bồi thường (Logic cũ)
                decimal contractValue = trip.TotalFare;
                decimal penaltyAmount = 0;
                string penaltyReason = "Hủy sớm (Miễn phí)";

                if (trip.ShippingRoute != null)
                {
                    var pickupTime = trip.ShippingRoute.ExpectedPickupDate.Date
                        .Add(trip.ShippingRoute.PickupTimeWindow?.StartTime?.ToTimeSpan() ?? TimeSpan.Zero);
                    var timeUntilPickup = pickupTime - TimeUtil.NowVN();

                    if (timeUntilPickup.TotalHours < 24)
                    {
                        penaltyAmount = contractValue * 0.30m;
                        penaltyReason = $"Hủy gấp (< 24h)";
                    }
                    else if (timeUntilPickup.TotalHours < 72)
                    {
                        penaltyAmount = contractValue * 0.10m;
                        penaltyReason = $"Hủy cận giờ (24h-72h)";
                    }
                }

                // 4. Xử lý Tài chính (Logic cũ)
                if (penaltyAmount > 0)
                {
                    var ownerWallet = await _unitOfWork.WalletRepo.FirstOrDefaultAsync(w => w.UserId == ownerId);
                    Guid providerId = trip.TripProviderContract?.CounterpartyId ?? Guid.Empty;
                    var providerWallet = await _unitOfWork.WalletRepo.FirstOrDefaultAsync(w => w.UserId == providerId);

                    if (ownerWallet != null && providerWallet != null)
                    {
                        // Trừ tiền Owner (Cho phép nợ)
                        await DeductMoneyWithDebtLogAsync(ownerWallet, trip.TripId, penaltyAmount, $"Phạt hủy chuyến {trip.TripCode}: {penaltyReason}");

                        // Cộng tiền Provider
                        providerWallet.Balance += penaltyAmount;
                        await CreateIncomeTransactionAsync(providerWallet, trip.TripId, penaltyAmount, TransactionType.COMPENSATION, $"Nhận bồi thường hủy chuyến {trip.TripCode}");

                        // Gửi email bồi thường
                        var providerUser = await _unitOfWork.BaseUserRepo.GetByIdAsync(providerId);
                        if (providerUser != null)
                        {
                            _ = Task.Run(() => _emailService.SendCancellationCompensationEmailAsync(
                                providerUser.Email, providerUser.FullName, trip.TripCode, penaltyAmount, penaltyReason, trip.Owner.FullName));
                        }
                    }
                }

                // =======================================================================
                // 5. UPDATE TRIP STATUS
                // =======================================================================
                trip.Status = TripStatus.CANCELLED;
                trip.UpdateAt = TimeUtil.NowVN();
                if (trip.TripProviderContract != null) trip.TripProviderContract.Status = ContractStatus.CANCELLED;

                // =======================================================================
                // 5.5 RE-OPEN POST & PACKAGES
                // =======================================================================
                if (trip.Packages != null && trip.Packages.Any())
                {
                    var postPackageId = trip.Packages.First().PostPackageId;

                    if (postPackageId != null)
                    {
                        var postPackage = await _unitOfWork.PostPackageRepo.GetByIdAsync(postPackageId.Value);
                        if (postPackage != null && postPackage.Status == PostStatus.DONE)
                        {
                            postPackage.Status = PostStatus.OPEN;
                            postPackage.Updated = TimeUtil.NowVN();
                            await _unitOfWork.PostPackageRepo.UpdateAsync(postPackage);
                        }
                    }

                    foreach (var pkg in trip.Packages)
                    {
                        pkg.TripId = null;
                        pkg.OwnerId = null;
                        pkg.Status = PackageStatus.PENDING;
                        await _unitOfWork.PackageRepo.UpdateAsync(pkg);
                    }
                }

                // 6. Release Drivers (Đoạn này sẽ ít khi chạy vào nếu Validation 1.5 hoạt động tốt, 
                // nhưng giữ lại để cleanup các record rác nếu có)
                if (trip.DriverAssignments != null)
                {
                    foreach (var assign in trip.DriverAssignments.Where(a => !a.IsFinished))
                    {
                        assign.AssignmentStatus = AssignmentStatus.CANCELLED;
                        assign.IsFinished = true;
                        assign.CheckOutNote = "Chuyến đi bị hủy bởi nhà xe.";
                    }
                }
                if (trip.Vehicle != null)
                {
                    trip.Vehicle.Status = VehicleStatus.ACTIVE;
                    await _unitOfWork.VehicleRepo.UpdateAsync(trip.Vehicle);
                }
                // 7. Save & Commit
                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync();

                return new ResponseDTO($"Hủy chuyến thành công. Bài đăng đã được mở lại cho các nhà xe khác. {penaltyReason}", 200, true);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO($"Lỗi: {ex.Message}", 500, false);
            }
        }
    }
}