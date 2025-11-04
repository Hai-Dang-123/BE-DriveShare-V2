using Common.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class CreateShippingRouteDTO
    {
        public string StartLocation { get; set; } = null!;
        public string EndLocation { get; set; } = null!;
        public DateTime ExpectedPickupDate { get; set; }
        public DateTime ExpectedDeliveryDate { get; set; }
        public TimeOnly StartTimeToPickup { get; set; }
        public TimeOnly EndTimeToPickup { get; set; }
        public TimeOnly StartTimeToDelivery { get; set; }
        public TimeOnly EndTimeToDelivery { get; set; }
    }
}
