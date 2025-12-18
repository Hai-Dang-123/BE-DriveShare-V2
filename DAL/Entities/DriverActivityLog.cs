using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class DriverActivityLog
    {
        public Guid DriverActivityLogId { get; set; }

        public string Description { get; set; } = string.Empty;

        // [NEW] Thêm mức độ cảnh báo để dễ lọc log (Info, Warning, Critical)
        public string LogLevel { get; set; } = "Info";

        public DateTime CreateAt { get; set; } = DateTime.UtcNow;

        // Foreign Key
        public Guid DriverId { get; set; }

        public virtual Driver Driver { get; set; }
    }
}
