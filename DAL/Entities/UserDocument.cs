using Common.Enums.Status; // Giả sử có Enum 'KycStatus'
using Common.Enums.Type;   // Giả sử có Enum 'DocumentType' (CCCD, DRIVER_LICENSE, PORTRAIT)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace DAL.Entities
{
    public class UserDocument
    {
        public Guid UserDocumentId { get; set; }

        // --- GỢI Ý BẮT BUỘC (Khóa ngoại) ---
        public Guid UserId { get; set; } // FK to BaseUser

        // --- GỢI Ý (Nghiệp vụ eKYC) ---

        // Loại tài liệu: Enum (CCCD, DRIVER_LICENSE, PORTRAIT, ...)
        public DocumentType DocumentType { get; set; }

        // --- Mặt trước ---
        public string FrontImageUrl { get; set; } = null!;
        public string? FrontImageHash { get; set; } // Hash của mặt trước

        // --- Mặt sau (Nullable, vì ảnh chân dung/passport không có mặt sau) ---
        public string? BackImageUrl { get; set; }
        public string? BackImageHash { get; set; } // Hash của mặt sau

        // --- Trạng thái ---
        // Trạng thái chung của giấy tờ này (ví dụ: CCCD đã được duyệt chưa)
        public VerifileStatus Status { get; set; }

        // Lý do từ chối (nếu Status là REJECTED)
        public string? RejectionReason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? VerifiedAt { get; set; } // Thời điểm Admin duyệt/từ chối

        // --- GỢI Ý BẮT BUỘC (Thuộc tính điều hướng) ---
        public virtual BaseUser User { get; set; } = null!;
    }
}