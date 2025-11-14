using System;
using System.Collections.Generic;

namespace Common.DTOs
{
    public class DriverTripDetailDTO
    {
        public Guid TripId { get; set; }
        public string TripCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreateAt { get; set; }
        public DateTime UpdateAt { get; set; }

        // --- Vehicle Info ---
        public Guid VehicleId { get; set; }
        public string VehicleModel { get; set; } = string.Empty;
        public string VehiclePlate { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty;

        // --- Owner Info ---
        public Guid OwnerId { get; set; }
        public string OwnerName { get; set; } = string.Empty;
        public string OwnerCompany { get; set; } = string.Empty;

        // --- Route Info ---
        public string StartAddress { get; set; } = string.Empty;
        public string EndAddress { get; set; } = string.Empty;
        public decimal EstimatedDistanceKm { get; set; }
        public TimeSpan EstimatedDuration { get; set; }

        // --- Trip Route ---
        public string TripRouteSummary { get; set; } = string.Empty;

        // --- Packages & Contracts ---
        public List<string> PackageCodes { get; set; } = new();
        public List<string> ContractCodes { get; set; } = new();

        // --- All assigned drivers ---
        public List<string> DriverNames { get; set; } = new();

        // --- Current Driver’s Assignment Info ---
        public string? AssignmentType { get; set; } = string.Empty;          // Chính / Phụ
        public string? AssignmentStatus { get; set; } = string.Empty;        // Offered / Accepted / Completed
        //public string? DriverPaymentStatus { get; set; } = string.Empty;     // Paid / Unpaid
    }
}
