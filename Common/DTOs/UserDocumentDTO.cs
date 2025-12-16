using System;
using System.Text.Json.Serialization;

namespace Common.DTOs
{
    public class UserDocumentDTO
    {
        public Guid UserDocumentId { get; set; }
        public Guid UserId { get; set; } // FK to BaseUser
        public string DocumentType { get; set; } = null!; // Enum (CCCD, DRIVER_LICENSE, etc.)
        public string FrontImageUrl { get; set; } = null!;
        public string? FrontImageHash { get; set; }
        public string? BackImageUrl { get; set; }
        public string? BackImageHash { get; set; }
        public string Status { get; set; } = null!; // Enum (ACTIVE, INACTIVE, etc.)
        public string? RejectionReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? VerifiedAt { get; set; }
    }
    public class UserWithDocumentsDTO
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
        public string? AvatarUrl { get; set; }
        public string RoleName { get; set; } = null!;
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }

        public IEnumerable<UserDocumentDTO> Documents { get; set; } = new List<UserDocumentDTO>();
    }

    public class MyDocumentsResponseDTO
    {
        public bool IsDriver { get; set; }
        public DocumentDetailDTO? CCCD { get; set; }
        public DriverDocumentsDTO? DriverDocuments { get; set; }
    }

    public class DriverDocumentsDTO
    {
        public DocumentDetailDTO? DrivingLicense { get; set; }
        // [BỔ SUNG] Thêm trường này
        public DocumentDetailDTO? HealthCheck { get; set; }

    }

    public class DocumentDetailDTO
    {
        public Guid UserDocumentId { get; set; }
        public string DocumentType { get; set; }
        public string Status { get; set; }
        public string IdentityNumber { get; set; } // Số giấy tờ
        public string FullName { get; set; }
        public string? LicenseClass { get; set; } // Hạng bằng (nếu có)
        public string FrontImageUrl { get; set; }
        public string BackImageUrl { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public string? RejectionReason { get; set; }
    }

    // DTO input cho User yêu cầu duyệt
    public class RequestManualReviewDTO
    {
        public Guid UserDocumentId { get; set; }
        public string UserNote { get; set; } = string.Empty;
    }

    // DTO input cho Staff duyệt bài
    public class ReviewDocumentDTO
    {
        [JsonPropertyName("userDocumentId")]
        public Guid UserDocumentId { get; set; }

        [JsonPropertyName("isApproved")]
        public bool IsApproved { get; set; }

        // FE gửi "rejectionReason"
        // Backend dùng RejectReason
        [JsonPropertyName("rejectionReason")]
        public string? RejectReason { get; set; }
    }

    // DTO hiển thị danh sách chờ duyệt (Output)
    public class PendingReviewSummaryDTO
    {
        public Guid UserDocumentId { get; set; }
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public string UserNote { get; set; } = string.Empty; // Lý do khiếu nại (ngắn gọn)
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUpdatedAt { get; set; } // Thời điểm user gửi yêu cầu review
    }

    // DTO chi tiết cho màn hình Review (Đầy đủ thông tin)
    public class PendingReviewDetailDTO : PendingReviewSummaryDTO
    {

        public string Status { get; set; } = string.Empty;

        public string FrontImageUrl { get; set; } = string.Empty;
        public string? BackImageUrl { get; set; }
        public string? PortraitImageUrl { get; set; }
        public string? EkycLog { get; set; } // Log gốc (nếu cần debug)
        public EkycAnalysisResultDTO AnalysisResult { get; set; } // Kết quả phân tích chi tiết
        public string? RejectionReason { get; set; }
    }
}