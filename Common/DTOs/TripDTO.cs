using Common.Enums.Status;
using Common.Enums.Type;
using System.ComponentModel.DataAnnotations;

namespace Common.DTOs
{
    //public class TripCreateDTO
    //{
    //    public Guid VehicleId { get; set; }
    //    public Guid ShippingRouteId { get; set; }
    //    public Guid TripRouteId { get; set; }

    //    public decimal TotalFare { get; set; }
    //    public decimal ActualDistanceKm { get; set; }
    //    public TimeSpan ActualDuration { get; set; }

    //    public DateTime? ActualPickupTime { get; set; }
    //    public DateTime? ActualCompletedTime { get; set; }
    //}

    public class TripCreateFromPostDTO
    {
        [Required]
        public Guid PostPackageId { get; set; } // Bài đăng họ chấp nhận

        [Required]
        public Guid VehicleId { get; set; } // Xe họ sẽ dùng


    }



    //public class TripContactInputDTO
    //{
    //    [Required]
    //    public string FullName { get; set; } = string.Empty;
    //    [Required]
    //    public string PhoneNumber { get; set; } = string.Empty;
    //    public string? Email { get; set; }
    //    public string? Note { get; set; }
    //}
    public class TripCreatedResultDTO
    {
        public Guid TripId { get; set; }
        public string TripCode { get; set; } = string.Empty;
        public TripStatus Status { get; set; }
        public TripType Type { get; set; }
    }
    public class ChangeTripStatusDTO
    {
        public Guid TripId { get; set; }
        public TripStatus NewStatus { get; set; }
        public string? Reason { get; set; } // lý do nếu hủy hoặc chuyển trạng thái bất thường
    }
    public class TripDetailDTO
    {
        public Guid TripId { get; set; }
        public string TripCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreateAt { get; set; }
        public DateTime UpdateAt { get; set; }

        // --- Vehicle Info ---
        public Guid VehicleId { get; set; }
        public string VehicleModel { get; set; } = string.Empty;
        public string VehiclePlate { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty;

        // --- Owner Info ---
        public Guid OwnerId { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public string OwnerCompany { get; set; } = string.Empty;

        // --- Shipping Route ---
        public string? StartAddress { get; set; }
        public string? EndAddress { get; set; }
        public decimal EstimatedDistanceKm { get; set; }
        public TimeSpan EstimatedDuration { get; set; }

        // --- Packages (nếu có) ---
        public List<string> PackageCodes { get; set; } = new List<string>();

        // --- Driver Assignments ---
        public List<string> DriverNames { get; set; } = new List<string>();

        // --- TripRoute (chi tiết GPS / tuyến đường) ---
        public string? TripRouteSummary { get; set; }

        // --- Contract liên quan ---
        public List<string> ContractCodes { get; set; } = new List<string>();

        public Guid? ProviderId { get; set; }
        public string? ProviderCompany { get; set; }
        public string? ProviderTaxCode { get; set; }
        public decimal? ProviderRating { get; set; }
    }
}

//}
