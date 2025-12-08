using Common.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    // Input từ Frontend gửi lên
    public class RouteCalculationRequestDTO
    {
        public Location StartLocation { get; set; }
        public Location EndLocation { get; set; }
        public DateTime ExpectedPickupDate { get; set; }
        public DateTime? ExpectedDeliveryDate { get; set; } // Có thể null nếu chỉ muốn lấy gợi ý
    }

    // Output trả về cho Frontend
    public class RouteCalculationResultDTO
    {
        public bool IsValid { get; set; } // Nếu người dùng có gửi DeliveryDate thì check xem hợp lệ ko
        public string Message { get; set; } // Thông báo (VD: "Hợp lệ" hoặc "Thời gian quá ngắn")

        public double DistanceKm { get; set; }
        public double EstimatedDurationHours { get; set; } // Thời gian chạy + Buffer an toàn

        // Gợi ý quan trọng cho Frontend điền vào ô "Ngày giao hàng"
        public DateTime SuggestedMinDeliveryDate { get; set; }
    }
}
