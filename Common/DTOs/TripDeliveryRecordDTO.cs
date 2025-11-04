using Common.Enums.Type;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{

    public class TripDeliveryRecordCreateDTO
    {
        public Guid TripId { get; set; }
        public Guid DeliveryRecordTempalteId { get; set; }
        public Guid StripContractId { get; set; }
        public DeliveryRecordType type { get; set; }
        public string? Notes { get; set; }

    }
    public class TripDeliveryRecordReadDTO
    {
        public Guid TripId { get; set; }
        public Guid? DeliveryRecordTempalteId { get; set; }
        public Guid? StripContractId { get; set; }
        public string type { get; set; }
        public string? Status { get; set; }
        public string? Notes { get; set; }
        public string ? DriverSignatureUrl { get; set; } // Ảnh chữ ký của Driver
        public DateTime? DriverSignedAt { get; set; }
        public string? ContactSignatureUrl { get; set; } // Ảnh chữ ký của Người gửi/nhận
        public DateTime? ContactSignedAt { get; set; }
        public TripContactDTO tripContact { get; set; }
        public DeliveryRecordTemplateDTO deliveryRecordTemplate { get; set; }

    }
}
