using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class DeliveryRecordTerm
    {
        public Guid DeliveryRecordTermId { get; set; }

        // --- GỢI Ý BẮT BUỘC (Khóa ngoại - Dựa trên sơ đồ 1-n) ---
        // Điều khoản này thuộc về Mẫu biên bản nào?
        public Guid DeliveryRecordTemplateId { get; set; } // FK to DeliveryRecordTemplate

        // --- Nội dung điều khoản ---
        public string Content { get; set; } = null!; // Nội dung (ví dụ: "Hàng hóa nguyên vẹn, không móp méo")

        // GỢI Ý (Nghiệp vụ): Thứ tự hiển thị của điều khoản
        public int DisplayOrder { get; set; }

        // --- GỢI Ý BẮT BUỘC (Thuộc tính điều hướng) ---
        public virtual DeliveryRecordTemplate DeliveryRecordTemplate { get; set; } = null!;
    }
}