using Common.Enums.Status;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Entities
{
    // 1. Đánh Index cho cặp Driver + Thời gian để query nhanh
    [Index(nameof(DriverId), nameof(StartTime))]
    [Index(nameof(Status))]
    public class DriverWorkSession
    {
        public Guid DriverWorkSessionId { get; set; }

        public Guid DriverId { get; set; }

        // --- THAY ĐỔI 1: Cho phép Null (vì lịch sử nhập tay không có TripId) ---
        public Guid? TripId { get; set; }

        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime? EndTime { get; set; }

        // --- THAY ĐỔI 2: Bỏ 'private' set hoặc dùng hàm SetHistoryData bên dưới
        // Ở đây mình giữ private set để đảm bảo tính toàn vẹn dữ liệu, 
        // và thêm hàm SetHistoryData để set giá trị này.
        public double DurationInHours { get; private set; } = 0;

        public WorkSessionStatus Status { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public virtual Driver Driver { get; set; } = null!;

        // --- THAY ĐỔI 3: Navigation Property cũng phải Nullable ---
        public virtual Trip? Trip { get; set; }

        // --- Helper Methods (Domain Logic) ---

        /// <summary>
        /// Dùng cho luồng chạy thật trên app: Kết thúc tại thời điểm hiện tại
        /// </summary>
        public void CompleteSession()
        {
            EndTime = DateTime.UtcNow;
            Status = WorkSessionStatus.COMPLETED;

            // Tính toán Duration dựa trên thời gian thực
            if (EndTime.HasValue)
            {
                DurationInHours = (EndTime.Value - StartTime).TotalHours;
            }
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// [MỚI] Dùng cho luồng nhập lịch sử: Set cứng thời gian và tự tính Duration
        /// </summary>
        public void SetHistoryData(DateTime start, DateTime end)
        {
            StartTime = start;
            EndTime = end;
            Status = WorkSessionStatus.COMPLETED;

            // Tính toán Duration dựa trên thời gian nhập vào
            DurationInHours = (end - start).TotalHours;

            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Tính thời gian tạm tính nếu chuyến đi đang diễn ra (cho UI hiển thị)
        /// </summary>
        public double GetCurrentDuration()
        {
            if (EndTime.HasValue) return DurationInHours;
            return (DateTime.UtcNow - StartTime).TotalHours;
        }
    }
}