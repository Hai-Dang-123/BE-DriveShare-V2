using Common.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class BaseProfileDTO
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Status { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? AvatarUrl { get; set; }
        public bool? IsEmailVerified { get; set; }
        public bool? IsPhoneVerified { get; set; }
        public Location Address { get; set; } // Giả sử Location là an toàn để expose
        public string Role { get; set; } = null!;
    }



    public class UserDocumentInDashboardDTO
    {
        public Guid UserDocumentId { get; set; }
        public string DocumentType { get; set; }
        public string FrontImageUrl { get; set; }
        public string? BackImageUrl { get; set; }
        public string Status { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? VerifiedAt { get; set; }
    }

    public class UserViolationInDashboardDTO
    {
        public Guid UserViolationId { get; set; }
        public Guid? TripId { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public DateTime CreateAt { get; set; }
        public string Status { get; set; }
        public string Severity { get; set; }
    }

    public class TransactionInDashboardDTO
    {
        public Guid TransactionId { get; set; }
        public string Type { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
        public DateTime CreateAt { get; set; }
        public Guid? PaymentId { get; set; }
    }

    public class DriverWorkSessionInDashboardDTO
    {
        public Guid DriverWorkSessionId { get; set; }
        public Guid TripId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string Status { get; set; }
    }

    public class OwnerDriverLinkInDashboardDTO
    {
        public Guid OwnerDriverLinkId { get; set; }
        public string Status { get; set; }
        public Guid OwnerId { get; set; }
        public Guid DriverId { get; set; }
        // (Có thể thêm Tên Owner/Driver nếu cần)
    }

    public class TripDriverAssignmentInDashboardDTO
    {
        public Guid TripDriverAssignmentId { get; set; }
        public Guid TripId { get; set; }
        public string Type { get; set; }
        public decimal BaseAmount { get; set; }
        public string AssignmentStatus { get; set; }
    }

    public class DriverActivityLogInDashboardDTO
    {
        public Guid DriverActivityLogId { get; set; }
        public string Description { get; set; }
        public DateTime CreateAt { get; set; }
    }

    public class VehicleSummaryInDashboardDTO
    {
        public Guid VehicleId { get; set; }
        public string PlateNumber { get; set; }
        public string Model { get; set; }
    }

    public class TripSummaryInDashboardDTO
    {
        public Guid TripId { get; set; }
        public string TripCode { get; set; }
        public string Status { get; set; }
    }

    public class ItemSummaryInDashboardDTO
    {
        public Guid ItemId { get; set; }
        public string ItemName { get; set; }
    }

    public class PackageSummaryInDashboardDTO
    {
        public Guid PackageId { get; set; }
        public string PackageCode { get; set; }
    }


    // ---------------------------------
    // DTOs Profile Chi Tiết (Kế thừa)
    // ---------------------------------

    // (Dùng lại BaseProfileDTO từ code cũ của bạn)
    // public class BaseProfileDTO { ... }

    public class AdminView_BaseUserDTO : BaseProfileDTO
    {
        // Yêu cầu 1: Thông tin cơ bản (đã có từ BaseProfileDTO)
        // Yêu cầu 2: Collections chung
        public List<UserDocumentInDashboardDTO> UserDocuments { get; set; } = new List<UserDocumentInDashboardDTO>();
        public List<UserViolationInDashboardDTO> UserViolations { get; set; } = new List<UserViolationInDashboardDTO>();
        public List<TransactionInDashboardDTO> Transactions { get; set; } = new List<TransactionInDashboardDTO>();
    }

    public class AdminView_DriverDTO : AdminView_BaseUserDTO
    {
        // Thông tin riêng của Driver
        public string LicenseNumber { get; set; }
        public string LicenseClass { get; set; }
        public DateTime? LicenseExpiryDate { get; set; }
        public bool IsLicenseVerified { get; set; }
        public string DriverStatus { get; set; }

        // Yêu cầu 3: Collections của Driver
        public List<DriverWorkSessionInDashboardDTO> DriverWorkSessions { get; set; } = new List<DriverWorkSessionInDashboardDTO>();
        public List<OwnerDriverLinkInDashboardDTO> OwnerDriverLinks { get; set; } = new List<OwnerDriverLinkInDashboardDTO>();
        public List<TripDriverAssignmentInDashboardDTO> TripDriverAssignments { get; set; } = new List<TripDriverAssignmentInDashboardDTO>();
        public List<DriverActivityLogInDashboardDTO> ActivityLogs { get; set; } = new List<DriverActivityLogInDashboardDTO>();
    }

    public class AdminView_OwnerDTO : AdminView_BaseUserDTO
    {
        // Thông tin riêng của Owner
        public string? CompanyName { get; set; }
        public string? TaxCode { get; set; }
        public decimal? AverageRating { get; set; }

        // Yêu cầu 3: Collections của Owner
        public List<VehicleSummaryInDashboardDTO> Vehicles { get; set; } = new List<VehicleSummaryInDashboardDTO>();
        public List<TripSummaryInDashboardDTO> Trips { get; set; } = new List<TripSummaryInDashboardDTO>();
        // (Thêm các list khác nếu Admin cần xem)
    }

    public class AdminView_ProviderDTO : AdminView_BaseUserDTO
    {
        // Thông tin riêng của Provider
        public string? CompanyName { get; set; }
        public string? TaxCode { get; set; }
        public decimal AverageRating { get; set; }

        // Yêu cầu 3: Collections của Provider
        public List<ItemSummaryInDashboardDTO> Items { get; set; } = new List<ItemSummaryInDashboardDTO>();
        public List<PackageSummaryInDashboardDTO> Packages { get; set; } = new List<PackageSummaryInDashboardDTO>();
        // (Thêm các list khác nếu Admin cần xem)
    }
}
