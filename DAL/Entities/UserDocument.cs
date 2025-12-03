using Common.Enums.Status;
using Common.Enums.Type;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Entities
{
    public class UserDocument
    {
        public Guid UserDocumentId { get; set; }
        public Guid UserId { get; set; }

        // --- Loại tài liệu ---
        public DocumentType DocumentType { get; set; } // CCCD, DRIVING_LICENSE...

        // --- Hình ảnh & Hash ---
        public string FrontImageUrl { get; set; } = null!;
        public string? FrontImageHash { get; set; }

        public string? BackImageUrl { get; set; }
        public string? BackImageHash { get; set; }

        public string? PortraitImageUrl { get; set; }
        public string? PortraitImageHash { get; set; }

        // --- Dữ liệu OCR chung ---
        public string? IdentityNumber { get; set; } // Số CCCD hoặc Số GPLX
        public string? FullName { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? PlaceOfOrigin { get; set; }
        public string? PlaceOfResidence { get; set; } // Với GPLX có thể là địa chỉ cư trú
        public DateTime? IssueDate { get; set; }
        public string? IssuePlace { get; set; } // Nơi cấp (Cục CSGT...)
        public DateTime? ExpiryDate { get; set; }

        // --- Dữ liệu RIÊNG cho Bằng Lái Xe (GPLX) ---
        // Có thể null nếu là CCCD
        public string? LicenseClass { get; set; } // Ví dụ: A1, B2, C, FC...

        // --- Kết quả EKYC ---
        public double? FaceMatchScore { get; set; }
        public bool IsDocumentReal { get; set; }
        public string? EkycLog { get; set; }

        // --- Trạng thái ---
        public VerifileStatus Status { get; set; }
        public string? RejectionReason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? VerifiedAt { get; set; }

        public virtual BaseUser User { get; set; } = null!;
    }
}