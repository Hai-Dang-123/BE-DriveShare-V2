using Common.Enums.Status;
using Common.Enums.Type; // Gợi ý: Thêm Using cho DocumentType
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class VehicleDocument
    {
        public Guid VehicleDocumentId { get; set; }

        // --- GỢI Ý BẮT BUỘC (Khóa ngoại - Dựa trên sơ đồ 1-n) ---
        public Guid VehicleId { get; set; } // FK to Vehicle

        // --- GỢI Ý BẮT BUỘC (Nghiệp vụ eKYC) ---
        // Enum: REGISTRATION (Cà vẹt), INSURANCE (Bảo hiểm), INSPECTION (Đăng kiểm)
        public DocumentType DocumentType { get; set; }

        // --- Chi tiết giấy tờ ---
        public string FrontDocumentUrl { get; set; } = null!;
        public string? BackDocumentUrl { get; set; }
        public string? FrontFileHash { get; set; }
        public string? BackFileHash { get; set; } 
        public DateTime? ExpirationDate { get; set; } // Ngày hết hạn (rất quan trọng cho bảo hiểm/đăng kiểm)

        // --- Trạng thái xác minh ---
        // Gợi ý: Đổi tên Enum 'VerifileStatus' thành 'KycStatus' cho nhất quán
        public VerifileStatus Status { get; set; } // PENDING, APPROVED, REJECTED, EXPIRED
        public string? AdminNotes { get; set; } // Ghi chú của Admin (ví dụ: lý do từ chối)
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; } // Thời điểm xác minh (Approved/Rejected)

        // JSON kết quả trả về từ OCR/eKYC
        public string? RawResultJson { get; set; }

        // --- GỢI Ý BẮT BUỘC (Thuộc tính điều hướng) ---
        public virtual Vehicle Vehicle { get; set; } = null!;
    }
}