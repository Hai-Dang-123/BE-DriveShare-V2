using Common.Enums.Status;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    internal class PackageDTO
    {
    }
    public class PackageCreateDTO
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Quantity { get; set; } 
        public string? Unit { get; set; } 
        public decimal WeightKg { get; set; }
        public decimal VolumeM3 { get; set; } 
        public string? OtherRequirements { get; set; }
        public Guid? OwnerId { get; set; } 
        public Guid? ProviderId { get; set; } 
        public Guid ItemId { get; set; }

        public List<string>? HandlingAttributes { get; set; }

        public List<IFormFile> PackageImages { get; set; } = new List<IFormFile>();
    }
    public class PackageUpdateDTO
     {
        public Guid PackageId { get; set; }
        public string PackageCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Quantity { get; set; }
        public string? Unit { get; set; }
        public decimal WeightKg { get; set; }
        public decimal VolumeM3 { get; set; }
        public string? OtherRequirements { get; set; }
        public Guid? OwnerId { get; set; }
        public Guid? ProviderId { get; set; }
        public Guid ItemId { get; set; }
        public Guid PostPackageId { get; set; }
        public Guid TripId { get; set; }
        public List<string>? HandlingAttributes { get; set; }
    }
    public class PackageGetByIdDTO
    {
        public Guid PackageId { get; set; }
        public string PackageCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Quantity { get; set; }
        public string? Unit { get; set; }
        public decimal WeightKg { get; set; }
        public decimal VolumeM3 { get; set; }
        public PackageStatus Status { get; set; }
        public List<string> HandlingAttributes { get; set; } = new List<string>();
        public string? OtherRequirements { get; set; }
        public Guid? OwnerId { get; set; }
        public Guid? ProviderId { get; set; }
        public Guid ItemId { get; set; }
        public Guid? PostPackageId { get; set; }
        public Guid? TripId { get; set; }

        //public List<string>? PackageImageUrls { get; set; }
        public ItemReadDTO Item { get; set; }
        public List<PackageImageReadDTO> PackageImages { get; set; }

    }

    public class PackageGetAllDTO
    {
        public Guid PackageId { get; set; }
        public string PackageCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Quantity { get; set; }
        public string? Unit { get; set; }
        public decimal WeightKg { get; set; }
        public decimal VolumeM3 { get; set; }
        public PackageStatus Status { get; set; }
        public List<string> HandlingAttributes { get; set; } = new List<string>();
        public string? OtherRequirements { get; set; }
        public Guid? OwnerId { get; set; }
        public Guid? ProviderId { get; set; }
        public Guid? PostPackageId { get; set; }
        public Guid? TripId { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<PackageImageReadDTO> PackageImages { get; set; }
    }

   
}
