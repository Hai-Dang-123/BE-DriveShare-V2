using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Utilities
{
    public class TimeUtil
    {
        private static readonly TimeZoneInfo VnTimeZone = GetVnTimeZone();

        private static TimeZoneInfo GetVnTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); // Linux
            }
            catch
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"); // Windows
            }
        }

        /// <summary>
        /// Giờ Việt Nam – dùng để LƯU DB
        /// </summary>
        public static DateTime NowVN()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VnTimeZone);
        }
    }
}
