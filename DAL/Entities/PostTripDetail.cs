using Common.Enums.Type;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class PostTripDetail
    {
        public Guid PostTripDetailId { get; set; }
        public Guid PostTripId { get; set; }
        public virtual PostTrip PostTrip { get; set; } = null!;

        public DriverType Type { get; set; } // Chính / Phụ
        public int RequiredCount { get; set; } = 1; // Số lượng cần
        public decimal PricePerPerson { get; set; } // Giá cho mỗi người
        public decimal? TotalBudget => PricePerPerson * RequiredCount;

        // Điểm đón / trả riêng cho loại tài xế
        public string PickupLocation { get; set; } = string.Empty;
        public string DropoffLocation { get; set; } = string.Empty;

        // Quy tắc đặc thù
        public bool MustPickAtGarage { get; set; } = false;
        public bool MustDropAtGarage { get; set; } = false;
    }
}
