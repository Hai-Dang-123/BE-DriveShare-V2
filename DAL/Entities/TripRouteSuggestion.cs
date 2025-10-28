using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    // Tên class của bạn đã đúng (Suggestion)
    public class TripRouteSuggestion
    {
        public Guid TripRouteSuggestionId { get; set; }
        // --- GỢI Ý BẮT BUỘC (Khóa ngoại - Dựa trên sơ đồ 1-n) ---
        // Gợi ý này dành cho Chuyến đi nào?
        public Guid TripId { get; set; } // FK to Trip
        // --- Chi tiết Gợi ý ---
        // GỢI Ý (Nghiệp vụ): Tên của gợi ý
        public string SuggestionName { get; set; } = string.Empty;
        // Dữ liệu tuyến đường (JSON/polyline bao gồm các điểm dừng nghỉ đã tính toán)
        public string RouteData { get; set; } = string.Empty;
        public decimal DistanceKm { get; set; }  // Quãng đường dự kiến
        public TimeSpan Duration { get; set; }    // Thời gian dự kiến (đã bao gồm nghỉ)
        public DateTime CreateAt { get; set; } = DateTime.Now;
        public DateTime UpdateAt { get; set; } = DateTime.Now;
        // --- GỢI Ý BẮT BUỘC (Thuộc tính điều hướng) ---
        public virtual Trip Trip { get; set; } = null!;
    }
}