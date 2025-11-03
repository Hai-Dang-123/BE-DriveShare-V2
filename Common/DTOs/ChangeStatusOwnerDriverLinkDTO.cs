using Common.Enums.Status;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class ChangeStatusOwnerDriverLinkDTO
    {
        public Guid OwnerDriverLinkId { get; set; }
        public FleetJoinStatus Status { get; set; }
    }
}
