using Common.Enums.Status; 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class TripCompensation
    {
        public Guid TripCompensationId { get; set; }
        public Guid TripId { get; set; } // FK to Trip
        public Guid RequesterId { get; set; } // FK to BaseUser (Người yêu cầu)
        public Guid? IssueId { get; set; } // FK to TripDeliveryIssue

        public decimal Amount { get; set; } // Số tiền yêu cầu
        public string Reason { get; set; } = string.Empty;
        public CompensationStatus Status { get; set; } // Requested, Approved, Rejected, Paid

        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }

        // GỢI Ý (Nghiệp vụ): Ai là người xử lý/duyệt yêu cầu này? (Thường là Admin)
        public Guid? ProcessorId { get; set; } // FK to BaseUser (Admin)

        // --- GỢI Ý BẮT BUỘC (Navigation Properties) ---
        public virtual Trip Trip { get; set; } = null!;
        public virtual BaseUser Requester { get; set; } = null!;
        public virtual TripDeliveryIssue? Issue { get; set; }
        public virtual BaseUser? Processor { get; set; }
    }
}