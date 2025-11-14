using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class LinkedDriverDTO
    {
        public Guid OwnerDriverLinkId { get; set; }
        public Guid DriverId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string LicenseNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // PENDING, APPROVED, REJECTED
        public DateTime RequestedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
    }
}
