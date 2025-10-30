using Common.Enums.Status;
using Common.Enums.Type;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class VehicleDocumentDetailDTO
    {
        public Guid VehicleDocumentId { get; set; }

 

        // --- GỢI Ý BẮT BUỘC (Nghiệp vụ eKYC) ---
        // Enum: REGISTRATION (Cà vẹt), INSURANCE (Bảo hiểm), INSPECTION (Đăng kiểm)
        public DocumentType DocumentType { get; set; }

        // --- Chi tiết giấy tờ ---
        public string FrontDocumentUrl { get; set; } = null!;
        public string? BackDocumentUrl { get; set; }
        public DateTime? ExpirationDate { get; set; } // Ngày hết hạn (rất quan trọng cho bảo hiểm/đăng kiểm)

        // --- Trạng thái xác minh ---
        // Gợi ý: Đổi tên Enum 'VerifileStatus' thành 'KycStatus' cho nhất quán
        public VerifileStatus Status { get; set; } // PENDING, APPROVED, REJECTED, EXPIRED
        public string? AdminNotes { get; set; } // Ghi chú của Admin (ví dụ: lý do từ chối)
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; } // Thời điểm xác minh (Approved/Rejected)

      

    }
}
