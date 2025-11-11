using Common.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class ProviderProfileDTO : BaseProfileDTO
    {
        // Thông tin riêng của Provider
        public string? CompanyName { get; set; }
        public string? TaxCode { get; set; }
        public Location? BusinessAddress { get; set; }
        public decimal AverageRating { get; set; }

        // Thông tin "Analysis"
        public int TotalItems { get; set; }
        public int TotalPackages { get; set; }
        public int TotalPackagePosts { get; set; }
    }
}
