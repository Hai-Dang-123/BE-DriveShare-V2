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

        public TripService(IUnitOfWork unitOfWork, UserUtility userUtility)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
        }

        public async Task<ResponseDTO> CreateForOwnerAsync(TripCreateDTO dto)
        {
            try
            {
                var ownerId = _userUtility.GetUserIdFromToken();
                if (ownerId == Guid.Empty)
                    return new ResponseDTO("Unauthorized or invalid token", 401, false);

                var vehicle = await _unitOfWork.VehicleRepo.GetByIdAsync(dto.VehicleId);
                if (vehicle == null || vehicle.OwnerId != ownerId)
                    return new ResponseDTO("Vehicle not found or not owned by current user", 403, false);

                var trip = new Trip
                {
                    TripId = Guid.NewGuid(),
                    TripCode = GenerateTripCode(),
                    Status = TripStatus.CREATED,
                    Type = TripType.OWNER_CREATE,
                    CreateAt = DateTime.Now,
                    UpdateAt = DateTime.Now,

                    VehicleId = dto.VehicleId,
                    OwnerId = ownerId,
                    ShippingRouteId = dto.ShippingRouteId,
                    TripRouteId = dto.TripRouteId,

                    TotalFare = dto.TotalFare,
                    ActualDistanceKm = dto.ActualDistanceKm,
                    ActualDuration = dto.ActualDuration,
                    ActualPickupTime = dto.ActualPickupTime,
                    ActualCompletedTime = dto.ActualCompletedTime
                };

                await _unitOfWork.TripRepo.AddAsync(trip);
                await _unitOfWork.SaveChangeAsync();

                var result = new TripCreatedResultDTO
                {
                    TripId = trip.TripId,
                    TripCode = trip.TripCode,
                    Status = trip.Status,
                    Type = trip.Type
                };

                return new ResponseDTO("Trip created successfully", 200, true, result);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error while creating trip: {ex.Message}", 500, false);
            }
        }
        private string GenerateTripCode()
        {
            return $"TRIP-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
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