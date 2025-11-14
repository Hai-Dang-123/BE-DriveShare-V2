using Common.Enums.Status;
using Common.Enums.Type;
using Common.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class TripDriverAssignment
    {
        public Guid TripDriverAssignmentId { get; set; }

        // --- GỢI Ý BẮT BUỘC (Khóa ngoại - Dựa trên sơ đồ 1-n) ---

        // 1. Phân công này cho Chuyến đi nào?
        public Guid TripId { get; set; } // FK to Trip

        // 2. Phân công này cho Tài xế nào?
        public Guid DriverId { get; set; } // FK to Driver

        // --- Chi tiết phân công ---
        public DriverType Type { get; set; } // Chính hay Phụ
        public DateTime CreateAt { get; set; } = DateTime.Now;
        public DateTime UpdateAt { get; set; } = DateTime.Now;

        // --- Chi phí trả cho tài xế ---
        public decimal BaseAmount { get; set; }   // Số tiền cơ bản
        public decimal? BonusAmount { get; set; }  // Phụ phí

        // GỢI Ý: Cấu hình EF Core để bỏ qua trường này (NotMapped)
        public decimal TotalAmount => BaseAmount + (BonusAmount ?? 0);

        // --- Lộ trình của tài xế (Có thể khác lộ trình chính của Trip) ---
        public Location StartLocation { get; set; }
        public Location EndLocation { get; set; }

        // --- Trạng thái ---
        public AssignmentStatus AssignmentStatus { get; set; } // Offered, Accepted, Rejected, Completed...
        //public DriverPaymentStatus PaymentStatus { get; set; } // Unpaid, Paid

        // --- GỢI Ý BẮT BUỘC (Thuộc tính điều hướng) ---
        public virtual Trip Trip { get; set; } = null!;
        public virtual Driver Driver { get; set; } = null!;
    }
}