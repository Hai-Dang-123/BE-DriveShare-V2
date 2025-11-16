using Common.Enums.Type;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class PostTripDetailCreateDTO
    {
        [Required]
        public DriverType Type { get; set; } // Chính / Phụ

        public int RequiredCount { get; set; } = 1;

        [Range(0, double.MaxValue)]
        public decimal PricePerPerson { get; set; }

        public string PickupLocation { get; set; } = string.Empty;
        public string DropoffLocation { get; set; } = string.Empty;
        public bool MustPickAtGarage { get; set; } = false;
        public bool MustDropAtGarage { get; set; } = false;
    }
}
