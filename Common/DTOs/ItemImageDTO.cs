using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class ItemImageDTO
    {
    }
    public class ItemImageCreateDTO
    {
        public Guid ItemId { get; set; }
        public IFormFile File { get; set; } = null!;
    }
    public class UpdateItemImageDTO
    {
        public string ItemImageId { get; set; }
        public Guid ItemId { get; set; }
        public IFormFile File { get; set; } = null!;
    }
    public class ItemImageReadDTO
    {
        public Guid ItemImageId { get; set; }
        public Guid ItemId { get; set; }
        public string ImageUrl { get; set; } = null!;
    }

}
