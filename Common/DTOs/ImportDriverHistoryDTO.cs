using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class ImportDriverHistoryDTO
    {
        // Danh sách log của từng ngày
        public List<DailyHistoryLogDTO> DailyLogs { get; set; } = new List<DailyHistoryLogDTO>();
    }

    public class DailyHistoryLogDTO
    {
        public DateTime Date { get; set; } // Ngày nào (VD: 2023-10-25)
        public double HoursDriven { get; set; } // Tổng số giờ đã lái (VD: 8.5)
    }
}
