using BLL.Services.Impletement;
using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.Enums.Type;
using DAL.Entities;
using DAL.UnitOfWork;
using Microsoft.EntityFrameworkCore;
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


        public TripService(IUnitOfWork unitOfWork, UserUtility userUtility, IVietMapService vietMapService, ITripRouteService tripRouteService, ITripContactService tripContactService, ITripProviderContractService tripProviderContractService)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
            _vietMapService = vietMapService;
            _tripRouteService = tripRouteService;
            _tripContactService = tripContactService;
            _tripProviderContractService = tripProviderContractService;
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


        public async Task<ResponseDTO> CreateTripFromPostAsync(TripCreateFromPostDTO dto)
        {
            // Bắt đầu Transaction
            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // 1. VALIDATE OWNER (Lấy ID từ Token)
                var ownerId = _userUtility.GetUserIdFromToken();
                if (ownerId == Guid.Empty)
                    throw new Exception("Unauthorized or invalid token");

                // 2. VALIDATE POST PACKAGE (Lấy hết dữ liệu liên quan)
                var postPackage = await _unitOfWork.PostPackageRepo.FirstOrDefaultAsync(
                    p => p.PostPackageId == dto.PostPackageId,
                    // Include tất cả mọi thứ chúng ta cần
                    includeProperties: "ShippingRoute,PostContacts,Provider,Packages"
                );
                if (postPackage == null)
                    throw new Exception("Không tìm thấy Bài đăng (PostPackage).");
                if (postPackage.Status != PostStatus.OPEN)
                    throw new Exception("Bài đăng này đã đóng hoặc đã được nhận.");
                if (postPackage.ShippingRoute == null)
                    throw new Exception("Bài đăng thiếu thông tin Lộ trình (ShippingRoute).");
                if (postPackage.Provider == null)
                    throw new Exception("Bài đăng thiếu thông tin Nhà cung cấp (Provider).");

                // 3. VALIDATE VEHICLE (Kiểm tra sở hữu và lấy VehicleType)
                var vehicle = await _unitOfWork.VehicleRepo.FirstOrDefaultAsync(
                    v => v.VehicleId == dto.VehicleId && v.OwnerId == ownerId,
                    includeProperties: "VehicleType"
                );
                if (vehicle == null)
                    throw new Exception("Xe (Vehicle) không tìm thấy hoặc không thuộc về bạn.");

                // 4. GỌI SERVICE 1: TẠO TRIPROUTE
                // (Service này gọi VietMap và AddAsync)
                var newTripRoute = await _tripRouteService.CreateAndAddTripRouteAsync(
                    postPackage.ShippingRoute, vehicle
                );

                // 5. TẠO TRIP (Entity chính)
                var trip = new Trip
                {
                    TripId = Guid.NewGuid(),
                    TripCode = GenerateTripCode(),
                    Status = TripStatus.CREATED,
                    Type = TripType.FROM_PROVIDER, // (Type mới)
                    CreateAt = DateTime.UtcNow,
                    UpdateAt = DateTime.UtcNow,
                    VehicleId = dto.VehicleId,
                    OwnerId = ownerId,
                    TripRouteId = newTripRoute.TripRouteId, // Tuyến đường thực tế
                    ShippingRouteId = postPackage.ShippingRoute.ShippingRouteId,
                    TotalFare = postPackage.OfferedPrice, // Lấy giá từ bài đăng
                    ActualDistanceKm = newTripRoute.DistanceKm,
                    ActualDuration = newTripRoute.Duration,
                    ActualPickupTime = null,
                    ActualCompletedTime = null
                };
                await _unitOfWork.TripRepo.AddAsync(trip);

                // 6. GỌI SERVICE 2: TẠO CONTRACT
                // (Service này AddAsync)
                await _tripProviderContractService.CreateAndAddContractAsync(
                    trip.TripId, ownerId, postPackage.ProviderId, postPackage.OfferedPrice
                );

                // 7. GỌI SERVICE 3: SAO CHÉP CONTACTS
                // (Service này AddAsync - logic "lấy từ post contact")
                await _tripContactService.CopyContactsFromPostAsync(
                    trip.TripId, postPackage.PostContacts
                );

                // 8. CẬP NHẬT TRẠNG THÁI (PostPackage và Packages)
                postPackage.Status = PostStatus.DONE; // Đánh dấu bài đăng là "Đã nhận"
                await _unitOfWork.PostPackageRepo.UpdateAsync(postPackage);

                // Gán TripId và cập nhật trạng thái cho tất cả Package trong bài đăng
                foreach (var pkg in postPackage.Packages)
                {
                    pkg.TripId = trip.TripId;
                    pkg.OwnerId = ownerId; // Owner nhận gói hàng này
                    pkg.Status = PackageStatus.IN_PROGRESS;
                    await _unitOfWork.PackageRepo.UpdateAsync(pkg);
                }

                // 9. LƯU TẤT CẢ (COMMIT)
                await _unitOfWork.CommitTransactionAsync();

                // 10. Trả về kết quả
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
            try
            {
                var trip = await _unitOfWork.TripRepo.GetByIdAsync(dto.TripId);
                if (trip == null)
                    return new ResponseDTO("Trip not found.", 404, false);

                // Validate chuyển trạng thái
                if (!IsValidTransition(trip.Status, dto.NewStatus))
                    return new ResponseDTO($"Invalid status transition: {trip.Status} → {dto.NewStatus}", 400, false);

                trip.Status = dto.NewStatus;
                trip.UpdateAt = DateTime.UtcNow;

                await _unitOfWork.TripRepo.UpdateAsync(trip);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO($"Trip status changed to {dto.NewStatus}", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error changing trip status: {ex.Message}", 500, false);
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
                TripStatus.IN_TRANSIT => current == TripStatus.LOADING,
                TripStatus.UNLOADING => current == TripStatus.IN_TRANSIT,
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
                if (!isOwner && !isAssignedDriver && !isProvider)
                    return new ResponseDTO("Forbidden: Bạn không có quyền xem chuyến đi này.", 403, false);

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

    
