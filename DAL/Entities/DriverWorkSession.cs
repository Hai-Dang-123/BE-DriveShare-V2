using Common.Enums.Status;
using Microsoft.EntityFrameworkCore; // Cần thư viện này cho [Index]
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Entities
{
    // 1. Đánh Index cho cặp Driver + Thời gian để query nhanh (Lưu ý 3: Hiệu năng)
    [Index(nameof(DriverId), nameof(StartTime))]
    [Index(nameof(Status))]
    public class DriverWorkSession
    {
        public Guid DriverWorkSessionId { get; set; }

        public Guid DriverId { get; set; }
        public Guid TripId { get; set; }

        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime? EndTime { get; set; }

        // 2. Thay đổi thành thuộc tính lưu trữ (Lưu ý 2: Fix lỗi Query)
        // Không dùng "=>" nữa. Giá trị này sẽ được cập nhật khi EndTime có dữ liệu.
        public double DurationInHours { get; private set; } = 0;

        public WorkSessionStatus Status { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public virtual Driver Driver { get; set; } = null!;
        public virtual Trip Trip { get; set; } = null!;

        // --- Helper Methods (Domain Logic) ---

        /// <summary>
        /// Gọi hàm này khi tài xế hoàn thành chuyến đi
        /// </summary>
        public void CompleteSession()
        {
            EndTime = DateTime.UtcNow;
            Status = WorkSessionStatus.COMPLETED;

            // Tính toán và lưu vào DB luôn để sau này Sum() cho nhanh
            if (EndTime.HasValue)
            {
                DurationInHours = (EndTime.Value - StartTime).TotalHours;
            }
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