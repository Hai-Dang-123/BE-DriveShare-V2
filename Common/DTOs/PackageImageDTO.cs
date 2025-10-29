using Common.Enums.Status;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    internal class PackageImageDTO
    {
    }
    public class PackageImageCreateDTO
    {
        public Guid PackageId { get; set; }
        public IFormFile File { get; set; } = null!;
    }
    public class UpdatePackageImageDTO
    {
        public string PackageImageId { get; set; }
        public Guid PackageId { get; set; }
        public IFormFile File { get; set; } = null!;
    }
    public class PackageImageReadDTO
    {
        public Guid PackageImageId { get; set; }
        public Guid PackageId { get; set; }
        public string ImageUrl { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
    public class PackageImageUrlReadDTO
    {
        public Guid PackageImageId { get; set; }
        public Guid PackageId { get; set; }
        public string ImageUrl { get; set; } = null!;
    }
}
