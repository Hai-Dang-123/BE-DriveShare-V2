using System;

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
}