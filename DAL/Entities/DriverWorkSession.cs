using Common.Enums.Status;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class DriverWorkSession
    {
        public Guid DriverWorkSessionId { get; set; }

        // --- Thông tin liên kết ---
        public Guid DriverId { get; set; } // FK tới Driver
        public Guid TripId { get; set; }   // FK tới Trip (1–1)

        // --- Thời gian làm việc ---
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime? EndTime { get; set; }

        public decimal DurationInHours =>
            (decimal)((EndTime ?? DateTime.UtcNow) - StartTime).TotalHours;

        // --- Trạng thái phiên làm việc ---
        public WorkSessionStatus Status { get; set; } 

        // --- Dấu thời gian ---
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // --- Điều hướng ---
        public virtual Driver Driver { get; set; } = null!;
        public virtual Trip Trip { get; set; } = null!;
    }
}
