using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Entities
{
    public class InspectionHistory
    {
        [Key]
        public Guid InspectionHistoryId { get; set; }

        public Guid VehicleId { get; set; }
        public virtual Vehicle Vehicle { get; set; } = null!;

        public DateTime InspectionDate { get; set; } // Ngày đăng kiểm
        public DateTime ExpirationDate { get; set; } // Ngày hết hạn đăng kiểm

        public string InspectionStation { get; set; } = string.Empty; // Trạm đăng kiểm (VD: 50-05V)
        public string Result { get; set; } = "DAT"; // Đạt / Không Đạt

        public Guid? VehicleDocumentId { get; set; } // Link tới giấy tờ minh chứng (nếu có)
        public virtual VehicleDocument? Document { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}