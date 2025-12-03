using Common.Enums.Status;
using Common.ValueObjects;
using Microsoft.AspNetCore.Http;
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

        // --- THÊM 2 THUỘC TÍNH NÀY ---

        // 1. Danh sách ảnh xe
        // FE sẽ append: formData.append('VehicleImages', file1)
        public List<VehicleImageInputDTO> VehicleImages { get; set; } = new List<VehicleImageInputDTO>();

        // 2. Danh sách giấy tờ
        // FE sẽ append: formData.append('Documents[0].DocumentType', 'REGISTRATION')
        //               formData.append('Documents[0].FrontFile', file)
        public List<VehicleDocumentInputDTO> Documents { get; set; } = new List<VehicleDocumentInputDTO>();
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
        // 1. Cờ xác nhận xe đã hợp lệ để chạy chưa (Dựa trên Cà vẹt)
        public bool IsVerified { get; set; }



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
