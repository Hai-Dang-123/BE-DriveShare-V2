using Common.Enums.Status;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class Package
    {
        public Guid PackageId { get; set; }
        public string PackageCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Quantity { get; set; } // Số lượng của Item bên trong
        public string? Unit { get; set; } // Ví dụ: "cái", "thùng", "kg"
        public decimal WeightKg { get; set; }
        public decimal VolumeM3 { get; set; } // Mét khối

        public PackageStatus Status { get; set; } 

        // --- GỢI Ý CẢI TIẾN (Thay thế cho các trường Is...) ---
        // Lưu dưới dạng JSON hoặc một mảng string trong DB
        // Ví dụ: ["FRAGILE", "FLAMMABLE", "REFRIGERATION"]
        public List<string> HandlingAttributes { get; set; } = new List<string>();
        public string? OtherRequirements { get; set; }

        
        // Người tạo
        public virtual Owner? Owner { get; set; }
        public Guid? OwnerId { get; set; } // FK to Owner

        public virtual Provider? Provider { get; set; }
        public Guid? ProviderId { get; set; } // FK to Provider


        // Liên kết 1-1
        public virtual Item Item { get; set; } = null!;
        public Guid ItemId { get; set; } // FK to Item

        public virtual PostPackage? PostPackage { get; set; } // Gói hàng này trong bài đăng nào
        public Guid PostPackageId { get; set; }
        public virtual Trip? Trip { get; set; } // Gói hàng này trong chuyến đi nào
        public Guid TripId { get; set; }

        // Liên kết 1-n
        public virtual ICollection<PackageImage> PackageImages { get; set; } = new List<PackageImage>();
    }
}
