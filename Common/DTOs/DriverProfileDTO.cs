using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class DriverProfileDTO : BaseProfileDTO
    {
        // Thông tin riêng của Driver
        public string LicenseNumber { get; set; } = null!;
        public string LicenseClass { get; set; } = null!;
        public DateTime? LicenseExpiryDate { get; set; }
        public bool IsLicenseVerified { get; set; }
        public bool IsInTrip { get; set; }

        // Thông tin "Analysis"
        public int TotalTripsAssigned { get; set; }
        public int LinkedOwnersCount { get; set; }

        // [MỚI] Check xem đã có GPLX được duyệt chưa
        public bool HasVerifiedDriverLicense { get; set; }

        // [MỚI] Check xem đã khai báo lịch sử chạy ban đầu chưa
        public bool HasDeclaredInitialHistory { get; set; }
    }
}
