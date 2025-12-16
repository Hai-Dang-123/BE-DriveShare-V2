using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class TruckRestriction
    {
        public Guid TruckRestrictionId { get; set; }


        public string ZoneName { get; set; } // Ví dụ: "Hồ Chí Minh", "Hà Nội"

        public string TruckType { get; set; } // Ví dụ: "truck", "container"

        // Lưu giờ bắt đầu cấm (VD: 16:00:00 là TimeSpan(16,0,0))
        public TimeSpan BanStartTime { get; set; }

        // Lưu giờ kết thúc cấm (VD: 20:00:00)
        public TimeSpan BanEndTime { get; set; }

        public string Description { get; set; } // VD: "Cấm tải nội đô chiều"
    }
}
