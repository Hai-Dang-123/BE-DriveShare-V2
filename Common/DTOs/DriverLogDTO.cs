using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    // DTO hiển thị danh sách log
    public class DriverActivityLogDTO
    {
        public Guid DriverActivityLogId { get; set; }
        public string Description { get; set; }
        public string LogLevel { get; set; } // Info, Warning, Critical
        public DateTime CreateAt { get; set; }

        // Thông tin thêm cho Admin (để biết log này của ai)
        public Guid DriverId { get; set; }
        public string DriverName { get; set; }
        public string DriverPhone { get; set; }
    }

    // DTO thống kê
    public class LogStatisticsDTO
    {
        public int TotalLogs { get; set; }
        public int InfoCount { get; set; }
        public int WarningCount { get; set; }
        public int CriticalCount { get; set; }
    }
}
