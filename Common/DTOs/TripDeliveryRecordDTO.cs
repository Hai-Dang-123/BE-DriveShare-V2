using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class TripDeliveryRecordDTO
    {
    }
    public class TripDeliveryRecordCreateDTO
    {
        public Guid TripId { get; set; }
        public Guid DeliveryRecordTempalteId { get; set; }
        
    }
}
