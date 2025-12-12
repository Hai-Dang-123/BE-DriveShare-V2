using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class TripVehicleHandoverReadDTO
    {
        public Guid TripVehicleHandoverRecordId { get; set; }
        public Guid TripId { get; set; }
        public Guid VehicleId { get; set; }
        public string Type { get; set; } // PICKUP / DROPOFF
        public string Status { get; set; } // DRAFT / COMPLETED

        // Thông tin người giao/nhận
        public Guid HandoverUserId { get; set; }
        public string HandoverUserName { get; set; }
        public Guid ReceiverUserId { get; set; }
        public string ReceiverUserName { get; set; }

        // Thông số xe
        public double CurrentOdometer { get; set; }
        public double FuelLevel { get; set; }
        public bool IsEngineLightOn { get; set; }
        public string? Notes { get; set; }

        // Chữ ký
        public bool HandoverSigned { get; set; }
        public DateTime? HandoverSignedAt { get; set; }
        public string? HandoverSignatureUrl { get; set; }

        public bool ReceiverSigned { get; set; }
        public DateTime? ReceiverSignedAt { get; set; }
        public string? ReceiverSignatureUrl { get; set; }

        // Checklist & Issues
        public List<HandoverTermResultDTO> TermResults { get; set; } = new();
        public List<HandoverIssueDTO> Issues { get; set; } = new();

        public List<HandoverSurchargeDTO> Surcharges { get; set; } = new List<HandoverSurchargeDTO>();
    }

    // DTO con cho khoản phạt
    public class HandoverSurchargeDTO
    {
        public Guid TripSurchargeId { get; set; }
        public string Type { get; set; }        // Loại (Trầy xước, Dơ bẩn...)
        public decimal Amount { get; set; }     // Số tiền
        public string Description { get; set; } // Mô tả
        public string Status { get; set; }      // Trạng thái (PENDING/PAID)
    }

    public class HandoverTermResultDTO
    {
        public Guid TripVehicleHandoverTermResultId { get; set; }
        public string TermContent { get; set; } // Nội dung câu hỏi
        public bool IsPassed { get; set; }
        public string? Note { get; set; }
        public string? EvidenceImageUrl { get; set; }
    }

    public class HandoverIssueDTO
    {
        public Guid TripVehicleHandoverIssueId { get; set; }
        public string IssueType { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public decimal? EstimatedCompensationAmount { get; set; }
        public List<string> ImageUrls { get; set; } = new();
    }

    // DTO gửi lên để ký
    public class SignVehicleHandoverDTO
    {
        public Guid RecordId { get; set; }
        public string Otp { get; set; }
        public string? SignatureUrl { get; set; } // URL ảnh chữ ký (nếu có)
    }
}
