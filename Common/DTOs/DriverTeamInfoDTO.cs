using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class DriverTeamInfoDTO
    {
        public Guid OwnerDriverLinkId { get; set; }
        public string Status { get; set; } // APPROVED, PENDING...
        public DateTime RequestedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }

        // Thông tin chủ xe (Owner)
        public Guid OwnerId { get; set; }
        public string OwnerName { get; set; }
        public string OwnerPhoneNumber { get; set; }
        public string OwnerAvatar { get; set; }
        public string OwnerEmail { get; set; }
    }
}
