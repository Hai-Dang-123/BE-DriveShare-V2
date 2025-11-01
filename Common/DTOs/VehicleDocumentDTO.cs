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
}