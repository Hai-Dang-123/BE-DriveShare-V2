using Common.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class Provider : BaseUser
    {
        public string? CompanyName { get; set; }
        public string? TaxCode { get; set; }
        public Location? BusinessAddress { get; set; }
        public decimal AverageRating { get; set; } = 5.0m;

        // --- GỢI Ý BẮT BUỘC (Navigation Properties - Dựa trên sơ đồ quan hệ) ---

        // Mối quan hệ 1-n với Mặt hàng (do Provider tạo)
        public virtual ICollection<Item> Items { get; set; } = new List<Item>();

        // Mối quan hệ 1-n với Thùng hàng (do Provider tạo)
        public virtual ICollection<Package> Packages { get; set; } = new List<Package>();

        // Mối quan hệ 1-n với Bài đăng gói cước
        public virtual ICollection<PostPackage> PostPackages { get; set; } = new List<PostPackage>();

        // Mối quan hệ 1-n với Hợp đồng (Provider ký với Owner)
        public virtual ICollection<TripProviderContract> TripProviderContracts { get; set; } = new List<TripProviderContract>();
    }
}