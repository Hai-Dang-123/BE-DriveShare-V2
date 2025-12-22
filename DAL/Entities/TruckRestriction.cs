using System;
using System.ComponentModel.DataAnnotations;

namespace DAL.Entities
{
    public class TruckRestriction
    {
        public Guid TruckRestrictionId { get; set; }

        public string ZoneName { get; set; } // Ví dụ: "Hà Nội - Nội Thành"

        public string TruckType { get; set; } // Ví dụ: "truck", "container"

        // Lưu giờ bắt đầu cấm (VD: 16:00:00)
        public TimeSpan BanStartTime { get; set; }

        // Lưu giờ kết thúc cấm (VD: 20:00:00)
        public TimeSpan BanEndTime { get; set; }

        public string Description { get; set; }

        // [MỚI - QUAN TRỌNG] Cột này chứa các từ khóa phân cách bằng dấu phẩy
        // Ví dụ: "hoàn kiếm,đống đa,ba đình,hai bà trưng,cầu giấy"
        public string MatchKeywords { get; set; }
    }
}