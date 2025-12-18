using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class UpdateUserProfileDTO
    {
        // --- Thông tin chung (BaseUser) ---
        public string? FullName { get; set; }
        public string? AvatarUrl { get; set; }
        public DateTime? DateOfBirth { get; set; }

        // Địa chỉ cá nhân
        public LocationInputDTO? Address { get; set; }

        // --- Thông tin riêng cho Driver ---
        public string? LicenseNumber { get; set; }
        public string? LicenseClass { get; set; } // Hạng bằng (B2, C, FC...)
        public DateTime? LicenseExpiryDate { get; set; }

        // --- Thông tin riêng cho Owner & Provider ---
        public string? CompanyName { get; set; }
        public string? TaxCode { get; set; }

        // Địa chỉ doanh nghiệp/kho bãi
        public LocationInputDTO? BusinessAddress { get; set; }
    }

    // Class phụ để hứng dữ liệu Location (Address + Tọa độ)
    public class LocationInputDTO
    {
        public string? Address { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}
