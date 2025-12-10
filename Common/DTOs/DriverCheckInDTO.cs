using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    // DTO Check-in
    public class DriverCheckInDTO
    {
        [Required]
        public Guid TripId { get; set; }

        [Required]
        public double Latitude { get; set; } // GPS hiện tại

        [Required]
        public double Longitude { get; set; } // GPS hiện tại

        public string? CurrentAddress { get; set; } // Địa chỉ text (nếu có)

        [Required]
        public IFormFile EvidenceImage { get; set; } = null!; // Ảnh bằng chứng
    }

    // DTO Check-out
    public class DriverCheckOutDTO
    {
        [Required]
        public Guid TripId { get; set; }

        [Required]
        public double Latitude { get; set; }

        [Required]
        public double Longitude { get; set; }

        public string? CurrentAddress { get; set; }

        [Required]
        public IFormFile EvidenceImage { get; set; } = null!;
    }
}
