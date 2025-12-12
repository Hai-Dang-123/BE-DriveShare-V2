using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class DriverSuggestionDTO
    {
        public double DistanceKm { get; set; }
        public double EstimatedDurationHours { get; set; } // Thời gian chạy thuần + buffer

        // Kịch bản 1: Tiết kiệm (1 Tài)
        public DriverScenarioDTO SoloScenario { get; set; } = new();

        // Kịch bản 2: Tối ưu/Team (2 Tài)
        public DriverScenarioDTO TeamScenario { get; set; } = new();

        // Kịch bản 3: Siêu tốc/Express (3 Tài)
        public DriverScenarioDTO ExpressScenario { get; set; } = new();

        public string SystemRecommendation { get; set; } = string.Empty; // Lời khuyên tổng thể

        public double RequiredHoursFromQuota { get; set; }
    }

    public class DriverScenarioDTO
    {
        public bool IsPossible { get; set; } // Có kịp deadline của Provider không?
        public double TotalElapsedHours { get; set; }  // Tổng thời gian trôi qua (Lái + Nghỉ) -> Dùng để check deadline giao hàng
        public double DrivingHoursPerDriver { get; set; } // Giờ lái thực tế/người -> Dùng để check luật 48h
        public string Message { get; set; } = string.Empty; // Mô tả (VD: "Chạy 10h/ngày, nghỉ đêm")
        public string Note { get; set; } = string.Empty; // Cảnh báo (VD: "Trễ 10 tiếng")
    }
}
