using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class DriverActivityLog
    {
        public Guid DriverActivityLogId { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime CreateAt { get; set; }

        //
        public virtual Driver Driver { get; set; } 
        public Guid DriverId { get; set; }
    }
}
