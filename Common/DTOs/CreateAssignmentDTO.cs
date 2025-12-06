using Common.Enums.Type;
using Common.ValueObjects;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class CreateAssignmentDTO
    {
        [Required]
        public Guid TripId { get; set; }

        [Required]
        public Guid DriverId { get; set; }

        [Required]
        public DriverType Type { get; set; }

        [Required]
        [Range(0, (double)decimal.MaxValue)]
        public decimal BaseAmount { get; set; } // Lương cơ bản cho tài xế

        public decimal? BonusAmount { get; set; } // Thưởng/Phụ phí

        [Required]
        public string StartLocation { get; set; } = null!; // Lấy từ đâu? (VD: Bãi xe của Owner)

        [Required]
        public string EndLocation { get; set; } = null!; // Trả xe ở đâu?
    }

    public class CreateAssignmentByPostTripDTO
    {
        [Required]
        public Guid PostTripId { get; set; }
        public Guid PostTripDetailId { get; set; }

        // [Bỏ Required] Không bắt buộc, vì tài phụ có thể đi theo xe từ đầu
        // Hoặc tài chính thì luôn bị override
        public string? StartLocation { get; set; }
        public string? EndLocation { get; set; }

        // public decimal OfferedAmount { get; set; } // (Nếu bạn dùng tính năng Bid giá thì giữ, còn nếu Fixed Price thì bỏ qua)
    }
}
