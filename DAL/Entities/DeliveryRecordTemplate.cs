using Common.Enums.Type;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class DeliveryRecordTemplate
    {
        public Guid DeliveryRecordTemplateId { get; set; }

        public string TemplateName { get; set; } = null!;
        public string Version { get; set; } = null!;

        // Mẫu này dùng cho loại biên bản nào (Nhận hàng hay Trả hàng)
        public DeliveryRecordType Type { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // --- GỢI Ý BẮT BUỘC (Navigation Properties - Dựa trên sơ đồ) ---

        // Mối quan hệ 1-n với các Điều khoản
        public virtual ICollection<DeliveryRecordTerm> DeliveryRecordTerms { get; set; } = new List<DeliveryRecordTerm>();

        // Mối quan hệ 1-n với các Biên bản (sử dụng mẫu này)
        public virtual ICollection<DeliveryRecord> DeliveryRecords { get; set; } = new List<DeliveryRecord>();
    }
}
