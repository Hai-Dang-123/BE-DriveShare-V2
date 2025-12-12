using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    // 3. DTO Phân tích Gap (Dùng khi chuẩn bị PostTrip)
    public class TripDriverAnalysisDTO
    {
        public DriverSuggestionDTO Suggestion { get; set; } // Lý thuyết
        public int TotalAssigned { get; set; }              // Hiện tại
        public bool HasMainDriver { get; set; }
        public int AssistantCount { get; set; }
        public int RemainingSlots { get; set; }             // Còn thiếu
        public string Recommendation { get; set; }          // Lời khuyên

        public double DrivingHoursRequired { get; set; }
    }
}
