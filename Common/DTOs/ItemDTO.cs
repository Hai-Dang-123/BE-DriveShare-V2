﻿using System;
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
        public decimal Price { get; set; }
    }
    public class ItemUpdateDTO
    {
        public Guid ItemId { get; set; }
        public string ItemName { get; set; }
        public string Description { get; set; }
        public decimal? DeclaredValue { get; set; }
        public string Currency { get; set; } = "VND";

    }
    public class ItemReadDTO
    {
        public Guid ItemId { get; set; }
        public string ItemName { get; set; }
        public string Description { get; set; }
        public decimal? DeclaredValue { get; set; }
        public string Currency { get; set; } = "VND";
        public decimal Price { get; set; }
        public Guid? OwnerId { get; set; }
        public Guid? ProviderId { get; set; }
        public string Status { get; set; }
        public List<string> ImageUrls { get; set; } = new();
    }
}
