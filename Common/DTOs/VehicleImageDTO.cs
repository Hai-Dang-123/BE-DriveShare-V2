using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;

namespace Common.DTOs
{
    public class VehicleImageCreateDTO
    {
        public Guid VehicleId { get; set; }
        public IFormFile File { get; set; } = null!;
        public string? Caption { get; set; }
    }

    public class VehicleImageUpdateDTO
    {
        public Guid VehicleImageId { get; set; }
        public IFormFile? File { get; set; } // có thể thay đổi ảnh
        public string? Caption { get; set; }
    }

    public class VehicleImageDetailDTO
    {
        public Guid VehicleImageId { get; set; }
        public Guid VehicleId { get; set; }
        public string ImageURL { get; set; } = string.Empty;
        public string? Caption { get; set; }
        public DateTime CreatedAt { get; set; }
    }
    public class VehicleImageDTO
    {
        public Guid VehicleImageId { get; set; }
        public Guid VehicleId { get; set; }
        public string? Caption { get; set; }
        public string ImageURL { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
