using Common.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class OwnerProfileDTO : BaseProfileDTO
    {
        // Thông tin riêng của Owner
        public string? CompanyName { get; set; }
        public string? TaxCode { get; set; }
        public Location? BusinessAddress { get; set; }
        public decimal? AverageRating { get; set; }

        // Thông tin "Analysis"
        public int TotalVehicles { get; set; }
        public int TotalDrivers { get; set; }
        public int TotalTripsCreated { get; set; }
    }
}
