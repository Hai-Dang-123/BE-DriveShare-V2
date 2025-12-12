using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class DriverAvailabilityInPostTripDTO
    {
        public Guid DriverId { get; set; }

        // Thông số Ngày (Luật 10h)
        public double DrivenHoursToday { get; set; }
        public double RemainingHoursToday { get; set; }

        // Thông số Tuần (Luật 48h)
        public double DrivenHoursThisWeek { get; set; }
        public double RemainingHoursThisWeek { get; set; }

        public bool IsBanned { get; set; } // True nếu đã vi phạm vượt mức
        public string Message { get; set; }
    }

    public class DriverSuitabilityDTO
    {
        public bool IsSuitable { get; set; }     // Có phù hợp không
        public string Reason { get; set; }       // Lý do (Thành công hoặc Lỗi)
        public double RequiredHours { get; set; } // Chuyến này cần bao nhiêu giờ
        public double DriverRemainingHours { get; set; } // Tài xế còn bao nhiêu giờ
    }
}
