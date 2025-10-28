using Common.Enums;
using Common.Enums.Type;
using Common.Enums.Status;   // Gợi ý: Thêm Using
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class UserViolation
    {
        public Guid UserViolationId { get; set; }
        public Guid UserId { get; set; } // FK to BaseUser (Người vi phạm)

        // GỢI Ý (Nghiệp vụ): Vi phạm này xảy ra trong chuyến đi nào? (Nullable)
        public Guid? TripId { get; set; } // FK to Trip

        public UserViolationType Type { get; set; } // Enum: SPEEDING, LATE_PICKUP, DAMAGED_GOODS...
        public string Description { get; set; } = string.Empty;
        public DateTime CreateAt { get; set; }

        // --- GỢI Ý (BẮT BUỘC): Bỏ comment 2 trường này ---
        public ViolationStatus Status { get; set; } // Pending, Confirmed, Appealed
        public ViolationSeverity Severity { get; set; } // Low, Medium, High

        // GỢI Ý (Nghiệp vụ): Ai là người xử lý/xác nhận? (Thường là Admin)
        public Guid? ProcessorId { get; set; } // FK to BaseUser (Admin)
        public DateTime? ProcessedAt { get; set; }

        // --- GỢI Ý BẮT BUỘC (Thuộc tính điều hướng) ---
        public virtual BaseUser User { get; set; } = null!;
        public virtual Trip? Trip { get; set; }
        public virtual BaseUser? Processor { get; set; }
    }
}