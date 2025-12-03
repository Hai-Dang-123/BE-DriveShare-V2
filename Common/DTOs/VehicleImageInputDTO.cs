using Common.Enums.Type;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class VehicleImageInputDTO
    {
        public IFormFile ImageFile { get; set; } = null!;
        public string? Caption { get; set; }
        public VehicleImageType ImageType { get; set; } // FE phải gửi lên: 1 (Overview) hoặc 2 (Plate)
    }
}
