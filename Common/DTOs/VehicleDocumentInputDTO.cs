using Common.Enums.Type;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class VehicleDocumentInputDTO
    {
        
        public DateTime? ExpirationDate { get; set; }

        [Required]
        public IFormFile FrontFile { get; set; } = null!;

        [Required]
        public IFormFile BackFile { get; set; } = null!;
    }

    public class AddVehicleDocumentDTO
    {
        [Required]
        public DocumentType DocumentType { get; set; } // VEHICLE_REGISTRATION, INSURANCE...

        public DateTime? ExpirationDate { get; set; } // Ngày hết hạn (bắt buộc với Bảo hiểm)

        [Required]
        public IFormFile FrontFile { get; set; } = null!; // Mặt trước bắt buộc

        public IFormFile? BackFile { get; set; } // Mặt sau (có thể null nếu là bảo hiểm 1 mặt)
    }
}
