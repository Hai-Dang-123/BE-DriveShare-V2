using Common.Enums.Status; 
using Common.Enums.Type;   
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class TripDeliveryIssue
    {
        public Guid TripDeliveryIssueId { get; set; }
        public Guid TripId { get; set; } // FK to Trip
        public Guid? DeliveryRecordId { get; set; } // FK to DeliveryRecord
        public Guid ReportedByUserId { get; set; } // FK to BaseUser

        // GỢI Ý: Dùng Enum thay vì string để nhất quán
        public DeliveryIssueType IssueType { get; set; } // Enum: DAMAGED, LOST, LATE, WRONG_ITEM

        public string Description { get; set; } = string.Empty;
        public IssueStatus Status { get; set; } // Reported, Investigating, Resolved
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // --- GỢI Ý BẮT BUỘC (Navigation Properties) ---

        // Liên kết n-1
        public virtual Trip Trip { get; set; } = null!;
        public virtual TripDeliveryRecord? TripDeliveryRecord { get; set; }
        public virtual BaseUser ReportedByUser { get; set; } = null!;

        // Liên kết 1-n
        public virtual ICollection<TripDeliveryIssueImage> DeliveryIssueImages { get; set; } = new List<TripDeliveryIssueImage>();
        //public virtual ICollection<TripCompensation> Compensations { get; set; } = new List<TripCompensation>();
        // --- BỔ SUNG: LIÊN KẾT NGƯỢC TỚI SURCHARGE ---
        // Một sự cố có thể sinh ra 1 hoặc nhiều khoản phạt
        public virtual ICollection<TripSurcharge> Surcharges { get; set; } = new List<TripSurcharge>();
    }
}