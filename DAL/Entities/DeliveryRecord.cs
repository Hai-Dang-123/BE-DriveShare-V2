using Common.Enums.Status;
using Common.Enums.Type;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class DeliveryRecord
    {
        public Guid DeliveryRecordId { get; set; }

        // Biên bản này dùng template nào?
        public Guid? DeliveryRecordTemplateId { get; set; } // FK to DeliveryRecordTemplate

        // Loại biên bản: PICKUP (Biên bản nhận) hoặc DROPOFF (Biên bản trả)
        public DeliveryRecordType Type { get; set; }
        public DeliveryRecordStatus Status { get; set; }
        public string? Notes { get; set; } // Ghi chú chung
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // --- Thuộc tính điều hướng ---
        public virtual DeliveryRecordTemplate? DeliveryRecordTemplate { get; set; }
    }
}
