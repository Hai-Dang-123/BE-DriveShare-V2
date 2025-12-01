using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class TripVehicleHandoverIssueImage
    {
        public Guid TripVehicleHandoverIssueImageId { get; set; }
        public Guid TripVehicleHandoverIssueId { get; set; }

        public string ImageUrl { get; set; } = null!;
        public string? Caption { get; set; }

        public virtual TripVehicleHandoverIssue TripVehicleHandoverIssue { get; set; } = null!;
    }
}
