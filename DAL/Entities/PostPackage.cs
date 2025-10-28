using Common.Enums.Status; 
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class PostPackage
    {
        public Guid PostPackageId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime Created { get; set; } = DateTime.Now;
        public DateTime Updated { get; set; } = DateTime.Now;

        public decimal OfferedPrice { get; set; } // Giá cước Provider đưa ra
        public PostStatus Status { get; set; }

        // --- GỢI Ý BẮT BUỘC (Dựa trên sơ đồ quan hệ) ---

        // 1. Ai là người đăng bài? (Provider - PostPackage 1-n)
        public Guid ProviderId { get; set; } // FK to Provider
        public virtual Provider Provider { get; set; } = null!;

        // 2. Bài đăng này cho gói hàng nào? (PostPackage - Package 1-1)
        public virtual ICollection<Package> Packages { get; set; } = null!;

        // 3. Lộ trình của bài đăng là gì? (PostPackage - ShippingRoute 1-1)
        public Guid ShippingRouteId { get; set; } // FK to ShippingRoute
        public virtual ShippingRoute ShippingRoute { get; set; } = null!;


    }
}