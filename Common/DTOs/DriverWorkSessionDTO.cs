using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    // DTO bắt đầu: Chỉ cần biết chạy chuyến nào
    public class StartSessionDTO
    {
        [Required]
        public Guid TripId { get; set; }
    }

    // DTO kết thúc: Xác định phiên nào cần đóng
    public class EndSessionDTO
    {
        [Required]
        public Guid DriverWorkSessionId { get; set; }
    }

    // DTO kết quả kiểm tra (Output)
    public class DriverAvailabilityDTO
    {
        public bool CanDrive { get; set; }
        public double HoursDrivenToday { get; set; }
        public double HoursDrivenThisWeek { get; set; }
        public string Message { get; set; }
    }

    // 1. Input: Dùng để lọc dữ liệu
    public class DriverHistoryFilterDTO
    {
        public DateTime? FromDate { get; set; } // Lọc từ ngày
        public DateTime? ToDate { get; set; }   // Đến ngày
        public int PageIndex { get; set; } = 1; // Trang số mấy
        public int PageSize { get; set; } = 10; // Lấy bao nhiêu dòng
    }

    // 2. Output: Hiển thị chi tiết từng chuyến
    public class DriverSessionHistoryDTO
    {
        public Guid SessionId { get; set; }
        public Guid? TripId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public double DurationHours { get; set; } // Tổng giờ chạy của chuyến này
        public string Status { get; set; }
    }

    // 3. Output tổng quát: Bao gồm danh sách và tổng hợp
    public class HistoryResponseDTO
    {
        public double TotalHoursInPeriod { get; set; } // Tổng giờ trong khoảng thời gian lọc
        public int TotalSessions { get; set; }
        public List<DriverSessionHistoryDTO> Sessions { get; set; }
    }

    // 1. DTO Trả về khi Start thành công
    public class StartSessionSuccessDTO
    {
        public Guid SessionId { get; set; }
        public string Role { get; set; } // "PRIMARY" hoặc "ASSISTANT"
        public string Message { get; set; }
    }

    // 2. DTO Trả về khi bị Trùng lịch (Conflict)
    public class DriverConflictDTO
    {
        public Guid ConflictDriverId { get; set; }
        public string ConflictDriverName { get; set; }
        public string ConflictDriverPhone { get; set; }
        public string ConflictDriverRole { get; set; } // "Tài Chính" hoặc "Tài Phụ"
        public DateTime SessionStartTime { get; set; }
        public string LicensePlate { get; set; } // Bonus: Thêm biển số xe cho rõ
    }

    public class CurrentSessionDTO
    {
        public Guid SessionId { get; set; }
        public Guid DriverId { get; set; }
        public string DriverName { get; set; }
        public string DriverPhone { get; set; }
        public string Role { get; set; } // "PRIMARY" hoặc "ASSISTANT"
        public DateTime StartTime { get; set; }
        public bool IsSelf { get; set; } // True nếu là chính người đang gọi API
    }
}
