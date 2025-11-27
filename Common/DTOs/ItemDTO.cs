using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class ItemDTO
    {
    }
    public class ItemCreateDTO
    {
        public string ItemName { get; set; }
        public string Description { get; set; }
        public decimal? DeclaredValue { get; set; }
        public string Currency { get; set; } = "VND";
        public int Quantity { get; set; }
        public string Unit { get; set; }
        //public decimal Price { get; set; }
        public List<IFormFile> ItemImages { get; set; } 
    }
    public class ItemUpdateDTO
    {
        public Guid ItemId { get; set; }
        public string ItemName { get; set; }
        public string Description { get; set; }
        public decimal? DeclaredValue { get; set; }
        public string Currency { get; set; } = "VND";
        public int Quantity { get; set; }
        public string Unit { get; set; }

    }
    public class ItemReadDTO
    {
        public Guid ItemId { get; set; }
        public string ItemName { get; set; }
        public string Description { get; set; }
        public decimal? DeclaredValue { get; set; }
        public string Currency { get; set; } = "VND";
        public decimal Price { get; set; }
        public int Quantity { get; set;}
        public string Unit { get; set; }
        public Guid? OwnerId { get; set; }
        public Guid? ProviderId { get; set; }
        public string Status { get; set; }
        public List<ItemImageReadDTO> ImageUrls { get; set; } = new();
    }
}
