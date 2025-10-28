using Common.Enums.Status;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class OwnerDriverLink
    {
        public Guid OwnerDriverLinkId { get; set; }
        public FleetJoinStatus Status { get; set; } // Pending, Approved, Rejected
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ApprovedAt { get; set; }
        //
        public virtual Owner Owner { get; set; } = null!;
        public Guid OwnerId { get; set; } // FK to Owner
        public virtual Driver Driver { get; set; } = null!; 
        public Guid DriverId { get; set; } // FK to Driver

    }
}
