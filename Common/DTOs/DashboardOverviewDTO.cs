using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class DashboardOverviewDTO
    {
        public int TotalUsers { get; set; }
        public int TotalDrivers { get; set; }
        public int TotalOwners { get; set; }
        public int TotalProviders { get; set; }
        public int TotalTrips { get; set; }
        public int TotalPackages { get; set; }
        public decimal TotalRevenue { get; set; }
    }
    public class TimeSeriesDTO
    {
        public string Label { get; set; } = string.Empty;
        public decimal Value { get; set; }
    }
    public class StatusStatisticDTO
    {
        public string Status { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
