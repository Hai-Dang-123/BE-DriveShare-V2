using System;

namespace Common.DTOs
{
    public class VehicleTypeCreateDTO
    {
        public string VehicleTypeName { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class VehicleTypeUpdateDTO
    {
        public Guid VehicleTypeId { get; set; }
        public string VehicleTypeName { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class VehicleTypeDTO
    {
        public Guid VehicleTypeId { get; set; }
        public string VehicleTypeName { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
