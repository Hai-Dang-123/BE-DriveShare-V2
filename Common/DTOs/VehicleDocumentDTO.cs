using System;

namespace Common.DTOs
{
    public class VehicleDocumentDTO
    {
        public Guid VehicleDocumentId { get; set; }
        public Guid VehicleId { get; set; } // FK to Vehicle
        public string DocumentType { get; set; } = null!; // Enum (REGISTRATION, INSURANCE, etc.)
        public string FrontDocumentUrl { get; set; } = null!;
        public string? BackDocumentUrl { get; set; }
        public string? FrontFileHash { get; set; }
        public string? BackFileHash { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public string Status { get; set; } = null!; // Enum (PENDING, APPROVED, etc.)
        public string? AdminNotes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public string? RawResultJson { get; set; }
    }

    // 1. DTO cho danh sách (Nhẹ, chỉ thông tin cơ bản)
    public class VehicleDocumentPendingSummaryDTO
    {
        public Guid VehicleDocumentId { get; set; }
        public string DocumentType { get; set; }
        public string VehiclePlate { get; set; }
        public string OwnerName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // 2. DTO cho chi tiết (Đầy đủ ảnh và thông tin xe để Staff đối chiếu)
    public class VehicleDocumentPendingDetailDTO : VehicleDocumentPendingSummaryDTO
    {
        public string Status { get; set; }
        public string? AdminNotes { get; set; }
        public DateTime? ProcessedAt { get; set; }

        public string FrontDocumentUrl { get; set; }
        public string? BackDocumentUrl { get; set; }
        public DateTime? ExpirationDate { get; set; }

        public string VehicleModel { get; set; }
        public string VehicleBrand { get; set; }
        public string VehicleColor { get; set; }
        public string OwnerPhone { get; set; }
    }

    public class ReviewVehicleDocumentDTO
    {
        public Guid DocumentId { get; set; }  
        public bool IsApproved { get; set; }
        public string? RejectReason { get; set; }
    }
}