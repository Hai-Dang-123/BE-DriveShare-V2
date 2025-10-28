using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class TripRoute
    {
        public Guid TripRouteId { get; set; }

        // Dữ liệu tuyến đường (ví dụ: JSON hoặc polyline từ API Map)
        public string RouteData { get; set; } = string.Empty;
        public decimal DistanceKm { get; set; }  // Quãng đường dự kiến
        public TimeSpan Duration { get; set; }    // Thời gian dự kiến
        public DateTime CreateAt { get; set; } = DateTime.Now;
        public DateTime UpdateAt { get; set; } = DateTime.Now;

        // --- GỢI Ý BẮT BUỘC (Navigation Property - Dựa trên quan hệ 1-1) ---

        // Tuyến đường này được sử dụng bởi Chuyến đi nào?
        public virtual Trip Trip { get; set; } = null!;
    }
}