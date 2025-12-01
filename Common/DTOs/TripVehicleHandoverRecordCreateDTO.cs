using Common.Enums.Type;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class TripVehicleHandoverRecordCreateDTO
    {
        public Guid TripId { get; set; }

        // Loại biên bản: PICKUP (Giao xe) hoặc DROPOFF (Trả xe)
        public DeliveryRecordType Type { get; set; }

        // Người thực hiện giao (VD: Chủ xe khi Pickup)
        public Guid HandoverUserId { get; set; }

        // Người nhận (VD: Tài xế khi Pickup)
        public Guid ReceiverUserId { get; set; }

        public string? Notes { get; set; }
    }
}
