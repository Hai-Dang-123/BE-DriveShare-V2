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
}
