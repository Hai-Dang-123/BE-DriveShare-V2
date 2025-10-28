using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.ValueObjects
{
    public class TimeWindow
    {
        public TimeOnly? StartTime { get; private set; }
        public TimeOnly? EndTime { get; private set; }

        public TimeWindow() { }
        public TimeWindow(TimeOnly? startTime, TimeOnly? endTime)
        {
            // Có thể thêm logic kiểm tra: StartTime <= EndTime nếu cả hai đều có giá trị
            if (startTime.HasValue && endTime.HasValue && startTime.Value > endTime.Value)
            {
                throw new ArgumentException("StartTime cannot be after EndTime.");
            }
            StartTime = startTime;
            EndTime = endTime;
        }

        // Các phương thức tiện ích
        public bool IsValid() => StartTime.HasValue && EndTime.HasValue;
        public bool Contains(TimeOnly time)
        {
            if (!IsValid()) return false;
            return time >= StartTime.Value && time <= EndTime.Value;
        }

        // Override Equals và GetHashCode
        public override bool Equals(object? obj)
        {
            return obj is TimeWindow other &&
                   StartTime.Equals(other.StartTime) &&
                   EndTime.Equals(other.EndTime);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(StartTime, EndTime);
        }
    }
}
