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
                                                            // Nếu bạn muốn hỗ trợ "Khung giờ lấy hàng" (Window)
                                                            // Ví dụ: Lấy ngày 20/12, trong khung 08:00 - 10:00
        public TimeOnly? PickupTimeWindowStart { get; set; }
        public TimeOnly? PickupTimeWindowEnd { get; set; }
    }
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

        // BỔ SUNG MỚI
        public double WaitTimeHours { get; set; }   // Thời gian nằm chờ (quan trọng)
        public double TravelTimeHours { get; set; } // Thời gian chạy xe thực tế
    // 1. Quãng đường (Map từ VietMap)
    public double EstimatedDistanceKm { get; set; }

    // 5. Ghi chú lý do cấm (Để hiển thị: "Chờ 4h do cấm tải chiều")
    public string RestrictionNote { get; set; }

}

