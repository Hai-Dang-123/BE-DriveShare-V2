using BLL.Services.Impletement;
using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.Enums.Type;
using DAL.Entities;
using DAL.UnitOfWork;
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
        private bool IsValidTransition(TripStatus current, TripStatus next)
        {

            return next switch
            {
                TripStatus.LOOKING_FOR_DRIVER => current == TripStatus.CREATED,
                TripStatus.READY_FOR_CONTRACT => current == TripStatus.LOOKING_FOR_DRIVER,
                TripStatus.AWAITING_CONTRACT_SIGNATURE => current == TripStatus.READY_FOR_CONTRACT,
                TripStatus.VEHICLE_HANDOVER => current == TripStatus.AWAITING_CONTRACT_SIGNATURE,
                TripStatus.LOADING => current == TripStatus.VEHICLE_HANDOVER,
                TripStatus.IN_TRANSIT => current == TripStatus.LOADING,
                TripStatus.UNLOADING => current == TripStatus.IN_TRANSIT,
                TripStatus.DELIVERED => current == TripStatus.UNLOADING,
                TripStatus.RETURNING_VEHICLE => current == TripStatus.DELIVERED,
                TripStatus.COMPLETED => current == TripStatus.RETURNING_VEHICLE,
                TripStatus.CANCELLED => current != TripStatus.COMPLETED && current != TripStatus.CANCELLED,
                _ => false
            };
        }
        public async Task<ResponseDTO> GetAllTripByOwnerIdAsync(Guid ownerId)
        {
            try
            {
                // 🔹 1. Lấy danh sách Trip theo OwnerId
                var trips = await _unitOfWork.TripRepo.GetAllAsync(
                    filter: t => t.OwnerId == ownerId && t.Status != Common.Enums.Status.TripStatus.DELETED,
                    includeProperties: "Vehicle,Vehicle.VehicleType,Owner,Packages,ShippingRoute,TripRoute,DriverAssignments.Driver,DriverContracts,ProviderContracts"
                );

                if (trips == null || !trips.Any())
                    return new ResponseDTO("No trips found for this owner.", 404, false);

                // 🔹 2. Map sang DTO
                var result = trips.Select(t => new TripDetailDTO
                {
                    TripId = t.TripId,
                    TripCode = t.TripCode,
                    Status = t.Status.ToString(),
                    CreateAt = t.CreateAt,
                    UpdateAt = t.UpdateAt,

                    VehicleId = t.VehicleId,
                    VehicleModel = t.Vehicle.Model,
                    VehiclePlate = t.Vehicle.PlateNumber,
                    VehicleType = t.Vehicle.VehicleType.VehicleTypeName,

                    OwnerId = t.OwnerId,
                    OwnerName = t.Owner.FullName,
                    OwnerCompany = t.Owner.CompanyName,

                    StartAddress = t.ShippingRoute?.StartLocation?.Address ?? string.Empty,
                    EndAddress = t.ShippingRoute?.EndLocation?.Address ?? string.Empty,

                    // Tính thời gian dự kiến dựa trên hai mốc ExpectedPickupDate và ExpectedDeliveryDate
                    EstimatedDuration = (t.ShippingRoute != null &&
                     t.ShippingRoute.ExpectedDeliveryDate > t.ShippingRoute.ExpectedPickupDate)
                    ? t.ShippingRoute.ExpectedDeliveryDate - t.ShippingRoute.ExpectedPickupDate
                    : TimeSpan.Zero,

                    PackageCodes = t.Packages.Select(p => p.PackageCode).ToList(),
                    DriverNames = t.DriverAssignments.Select(a => a.Driver.FullName).ToList(),
                    TripRouteSummary = t.TripRoute != null
                                  ? $"Distance: {t.TripRoute.DistanceKm} km, Duration: {t.TripRoute.Duration.TotalMinutes:F0} minutes"
    :                             string.Empty,

                    //ContractCodes = t.DriverContracts.Select(c => c.ContractCode)
                    //                  .Concat(t.ProviderContracts.Select(c => c.ContractCode))
                    //                  .ToList()
                }).ToList();

                return new ResponseDTO("Get trips successfully", 200, true, result);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error getting trips: {ex.Message}", 500, false);
            }
        }
        public async Task<ResponseDTO> GetAllTripByDriverIdAsync(Guid driverId)
        {
            try
            {
                // 🔹 B1: Lấy tất cả các TripDriverAssignment của tài xế này
                var assignments = await _unitOfWork.TripDriverAssignmentRepo.GetAllAsync(
                    filter: a => a.DriverId == driverId
                              && a.AssignmentStatus != AssignmentStatus.REJECTED
                              && a.Trip.Status != TripStatus.DELETED,
                    includeProperties: "Trip,Trip.Vehicle,Trip.Vehicle.VehicleType,Trip.Owner,Trip.ShippingRoute,Trip.TripRoute,Trip.Packages,Trip.DriverAssignments.Driver,Trip.DriverContracts,Trip.ProviderContracts"
                );

                if (assignments == null || !assignments.Any())
                    return new ResponseDTO("No trips found for this driver.", 404, false);

                // 🔹 B2: Lấy danh sách các trip duy nhất
                var trips = assignments.Select(a => a.Trip).Distinct().ToList();

                // 🔹 B3: Map dữ liệu sang DTO
                var result = trips.Select(t =>
                {
                    var currentAssign = t.DriverAssignments.FirstOrDefault(a => a.DriverId == driverId);

                    return new DriverTripDetailDTO
                    {
                        TripId = t.TripId,
                        TripCode = t.TripCode,
                        Status = t.Status.ToString(),
                        CreateAt = t.CreateAt,
                        UpdateAt = t.UpdateAt,

                        // --- Vehicle Info ---
                        VehicleId = t.VehicleId,
                        VehicleModel = t.Vehicle?.Model ?? "",
                        VehiclePlate = t.Vehicle?.PlateNumber ?? "",
                        VehicleType = t.Vehicle?.VehicleType?.VehicleTypeName ?? "",

                        // --- Owner Info ---
                        OwnerId = t.OwnerId,
                        OwnerName = t.Owner?.FullName ?? "",
                        OwnerCompany = t.Owner?.CompanyName ?? "",

                        // --- Shipping Route Info ---
                        StartAddress = t.ShippingRoute?.StartLocation?.Address ?? "",
                        EndAddress = t.ShippingRoute?.EndLocation?.Address ?? "",
                        EstimatedDuration = (t.ShippingRoute != null &&
                                             t.ShippingRoute.ExpectedDeliveryDate > t.ShippingRoute.ExpectedPickupDate)
                                            ? t.ShippingRoute.ExpectedDeliveryDate - t.ShippingRoute.ExpectedPickupDate
                                            : TimeSpan.Zero,

                        // --- Trip Route Info ---
                        TripRouteSummary = t.TripRoute != null
                            ? $"Distance: {t.TripRoute.DistanceKm} km, Duration: {t.TripRoute.Duration.TotalMinutes:F0} minutes"
                            : string.Empty,

                        // --- Packages & Contracts ---
                        PackageCodes = t.Packages.Select(p => p.PackageCode).ToList(),
                        //ContractCodes = t.DriverContracts.Select(c => c.ContractCode)
                        //                  .Concat(t.ProviderContracts.Select(c => c.ContractCode))
                        //                  .ToList(),

                        // --- All Drivers assigned to this Trip ---
                        DriverNames = t.DriverAssignments.Select(d => d.Driver.FullName).ToList(),

                        // --- Current Driver’s Assignment info ---
                        AssignmentType = currentAssign?.Type.ToString() ?? "",
                        AssignmentStatus = currentAssign?.AssignmentStatus.ToString() ?? "",
                        DriverPaymentStatus = currentAssign?.PaymentStatus.ToString() ?? ""
                    };
                }).ToList();

                return new ResponseDTO("Get trips successfully", 200, true, result);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error getting trips by driver: {ex.Message}", 500, false);
            }
        }
        public async Task<ResponseDTO> GetTripByIdAsync(Guid tripId)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                    return new ResponseDTO("Unauthorized or invalid token", 401, false);

                // Include toàn bộ liên kết liên quan
                var trips = await _unitOfWork.TripRepo.GetAllAsync(
                    filter: t => t.TripId == tripId,
                    includeProperties:
                        "Vehicle,Vehicle.VehicleType,Owner," +
                        "ShippingRoute,TripRoute," +
                        "Packages,PostTrip,DriverAssignments.Driver," +
                        "TripContacts,TripRouteSuggestions," +
                        "DriverContracts,ProviderContracts," +
                        "TripDeliveryRecords,Compensations,DeliveryIssues"
                );

                var trip = trips.FirstOrDefault();
                if (trip == null)
                    return new ResponseDTO("Trip not found", 404, false);

                // 🧩 Mapping DTO
                var dto = new TripDetailFullDTO
                {
                    TripId = trip.TripId,
                    TripCode = trip.TripCode,
                    Status = trip.Status.ToString(),
                    CreateAt = trip.CreateAt,
                    UpdateAt = trip.UpdateAt,

                    // --- Vehicle ---
                    Vehicle = new VehicleSummaryDTO
                    {
                        VehicleId = trip.Vehicle.VehicleId,
                        PlateNumber = trip.Vehicle.PlateNumber,
                        Model = trip.Vehicle.Model,
                        VehicleTypeName = trip.Vehicle.VehicleType.VehicleTypeName
                    },

                    // --- Owner ---
                    Owner = new OwnerSummaryDTO
                    {
                        OwnerId = trip.OwnerId,
                        FullName = trip.Owner.FullName,
                        CompanyName = trip.Owner.CompanyName,
                        PhoneNumber = trip.Owner.PhoneNumber
                    },

                    // --- Shipping Route ---
                    ShippingRoute = new RouteDetailDTO
                    {
                        StartAddress = trip.ShippingRoute.StartLocation.Address,
                        EndAddress = trip.ShippingRoute.EndLocation.Address,
                        EstimatedDuration = trip.ShippingRoute.ExpectedDeliveryDate - trip.ShippingRoute.ExpectedPickupDate
                    },

                    // --- Trip Route ---
                    TripRoute = new TripRouteSummaryDTO
                    {
                        DistanceKm = trip.TripRoute.DistanceKm,
                        DurationMinutes = trip.TripRoute.Duration.TotalMinutes
                    },

                    // --- Provider (nếu có PostTrip) ---
                    Provider = trip.PostTrip != null ? new ProviderSummaryDTO
                    {
                        ProviderId = trip.PostTrip.OwnerId,
                        CompanyName = trip.PostTrip.Owner.CompanyName,
                        TaxCode = trip.PostTrip.Owner.TaxCode,
                        AverageRating = trip.PostTrip.Owner.AverageRating ?? 0
                    } : null,

                    // --- Packages ---
                    PackageCodes = trip.Packages.Select(p => p.PackageCode).ToList(),

                    // --- Driver Assignments ---
                    Drivers = trip.DriverAssignments.Select(d => new TripDriverAssignmentDTO
                    {
                        DriverId = d.DriverId,
                        FullName = d.Driver.FullName,
                        Type = d.Type.ToString(),
                        AssignmentStatus = d.AssignmentStatus.ToString(),
                        PaymentStatus = d.PaymentStatus.ToString()
                    }).ToList(),

                    // --- Contacts ---
                    Contacts = trip.TripContacts.Select(c => new TripContactDTO
                    {
                        TripContactId = c.TripContactId,
                        Type = c.Type.ToString(),
                        FullName = c.FullName,
                        PhoneNumber = c.PhoneNumber,
                        Note = c.Note
                    }).ToList(),

                    // --- Driver Contracts ---
                    DriverContracts = trip.DriverContracts.Select(c => new ContractSummaryDTO
                    {
                        ContractId = c.ContractId,            
                        ContractCode = c.ContractCode,         
                        Status = c.Status.ToString(),         
                        Type = c.Type.ToString(),             
                        ContractValue = c.ContractValue ?? 0,  
                        Currency = c.Currency,                 
                        EffectiveDate = c.EffectiveDate,
                        ExpirationDate = c.ExpirationDate,
                        FileURL = c.FileURL
                    }).ToList(),

                    //// --- Provider Contracts ---
                    //ProviderContracts = trip.ProviderContracts.Select(c => new ContractSummaryDTO
                    //{
                    //    ContractId = c.ContractId,
                    //    ContractCode = c.ContractCode,
                    //    Status = c.Status.ToString(),
                    //    Type = c.Type.ToString(),              // ✅ Enum ContractType (OwnerProvider)
                    //    ContractValue = c.ContractValue ?? 0,
                    //    Currency = c.Currency,
                    //    EffectiveDate = c.EffectiveDate,
                    //    ExpirationDate = c.ExpirationDate,
                    //    FileURL = c.FileURL
                    //}).ToList(),


                    // --- Delivery Records ---
                    DeliveryRecords = trip.TripDeliveryRecords.Select(r => new TripDeliveryRecordDTO
                    {
                        TripDeliveryRecordId = r.DeliveryRecordId,
                        RecordType = r.Type.ToString(),
                        Note = r.Notes,
                        CreateAt = r.CreatedAt
                    }).ToList(),

                    // --- Compensations ---
                    Compensations = trip.Compensations.Select(cp => new TripCompensationDTO
                    {
                        TripCompensationId = cp.TripCompensationId,
                        Reason = cp.Reason,
                        Amount = cp.Amount
                    }).ToList(),

                    // --- Delivery Issues ---
                    Issues = trip.DeliveryIssues.Select(i => new TripDeliveryIssueDTO
                    {
                        TripDeliveryIssueId = i.TripDeliveryIssueId,
                        IssueType = i.IssueType.ToString(),
                        Description = i.Description,
                        Status = i.Status.ToString()
                    }).ToList()
                };

                return new ResponseDTO("Get trip successfully", 200, true, dto);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error fetching trip detail: {ex.Message}", 500, false);
            }
        }
    }
}