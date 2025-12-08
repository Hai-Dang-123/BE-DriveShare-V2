using Common.Enums.Status;
using Common.Enums.Type;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class TripSurcharge
    {
        public Guid TripSurchargeId { get; set; }

        public Guid TripId { get; set; } // FK to Trip

        // Loại phụ phí: Xăng, Quá Km, Vệ sinh, Hư hỏng...
        public SurchargeType Type { get; set; }
        public string Description { get; set; } = string.Empty; // Vd: "Thiếu 20% xăng so với lúc nhận"

        // Số tiền phải đóng thêm
        public decimal Amount { get; set; }

        // Trạng thái thanh toán của khoản này
        public SurchargeStatus Status { get; set; } // Pending, Paid, Waived (được miễn), Cancelled

        // Nếu là phạt hư hỏng, link tới Issue nào?
        public Guid? TripVehicleHandoverIssueId { get; set; }
        public Guid? TripDeliveryIssueId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAt { get; set; }

        // --- NAV ---
        public virtual Trip Trip { get; set; } = null!;
        public virtual TripVehicleHandoverIssue? TripVehicleHandoverIssue { get; set; }
        public virtual TripDeliveryIssue? TripDeliveryIssue { get; set; }
    }
}
