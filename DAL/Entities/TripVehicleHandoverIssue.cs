using Common.Enums.Status;
using Common.Enums.Type;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class TripVehicleHandoverIssue
    {
        public Guid TripVehicleHandoverIssueId { get; set; }

        public Guid TripVehicleHandoverRecordId { get; set; } // FK

        // Loại vấn đề: Trầy xước, Móp, Bẩn, Mất đồ...
        public VehicleIssueType IssueType { get; set; }

        public string Description { get; set; } = string.Empty; // Mô tả chi tiết
        public IssueStatus Status { get; set; } // Reported, Confirmed...

        // Chi phí đền bù ước tính (nếu chốt nóng tại chỗ)
        public decimal? EstimatedCompensationAmount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // --- NAV ---
        public virtual TripVehicleHandoverRecord TripVehicleHandoverRecord { get; set; } = null!;

        // Ảnh chụp bằng chứng vết xước/hỏng
        public virtual ICollection<TripVehicleHandoverIssueImage> Images { get; set; } = new List<TripVehicleHandoverIssueImage>();
        // --- BỔ SUNG: LIÊN KẾT NGƯỢC TỚI SURCHARGE ---
        // Một sự cố có thể sinh ra 1 hoặc nhiều khoản phạt
        public virtual ICollection<TripSurcharge> Surcharges { get; set; } = new List<TripSurcharge>();
    }
}
