using Common.Enums.Status;
using Common.ValueObjects;
using System;
using System.Collections.Generic;

namespace Common.DTOs
{
    public class VehicleCreateDTO
    {
        public Guid VehicleTypeId { get; set; }
        public string PlateNumber { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public int YearOfManufacture { get; set; }
        public string Color { get; set; } = string.Empty;
        public decimal PayloadInKg { get; set; }
        public decimal VolumeInM3 { get; set; }
        public List<string>? Features { get; set; } = new();
        public Location? CurrentAddress { get; set; }
    }

    public class VehicleUpdateDTO : VehicleCreateDTO
    {
        public Guid VehicleId { get; set; }
    }

    public class VehicleDetailDTO
    {
        public Guid VehicleId { get; set; }
        public string PlateNumber { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public int YearOfManufacture { get; set; }
        public decimal PayloadInKg { get; set; }
        public decimal VolumeInM3 { get; set; }
        public VehicleStatus Status { get; set; }
        public List<VehicleImageDetailDTO> ImageUrls { get; set; } = new();
        public List<VehicleDocumentDetailDTO> Documents { get; set; } = new();
        public VehicleTypeDTO VehicleType { get; set; }
        public GetDetailOwnerDTO Owner { get; set; }
    }
    public class VehicleDTO
    {
        public Guid VehicleId { get; set; }
        public string PlateNumber { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public int YearOfManufacture { get; set; }
        public decimal PayloadInKg { get; set; }
        public decimal VolumeInM3 { get; set; }
        public VehicleStatus Status { get; set; }
    }
}
