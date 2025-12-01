using Common.Enums.Type;
using System;
using System.Collections.Generic;

namespace Common.DTOs.TripVehicleHandoverRecord
{
    // DTO cập nhật checklist (lưu nháp hoặc chốt)
    public class UpdateHandoverChecklistDTO
    {
        public Guid RecordId { get; set; }
        public double CurrentOdometer { get; set; }
        public double FuelLevel { get; set; }
        public bool IsEngineLightOn { get; set; }
        public string? Notes { get; set; }

        public List<ChecklistItemInput> ChecklistItems { get; set; } = new();
    }

    public class ChecklistItemInput
    {
        public Guid TripVehicleHandoverTermResultId { get; set; }
        public bool IsPassed { get; set; }
        public string? Note { get; set; }
        public string? EvidenceImageUrl { get; set; }
    }

    // DTO báo cáo sự cố
    public class ReportHandoverIssueDTO
    {
        public Guid RecordId { get; set; }
        public VehicleIssueType IssueType { get; set; }
        public string Description { get; set; }
        public List<string> ImageUrls { get; set; } = new();
    }
}