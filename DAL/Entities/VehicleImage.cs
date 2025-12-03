using Common.Enums.Type;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class VehicleImage
    {
        public Guid VehicleImageId { get; set; }
        public string ImageURL { get; set; } = string.Empty;

        // --- GỢI Ý BẮT BUỘC (Khóa ngoại - Dựa trên sơ đồ 1-n) ---
        public Guid VehicleId { get; set; } // FK to Vehicle
        // --- MỚI THÊM: Loại ảnh ---
        public VehicleImageType ImageType { get; set; } = VehicleImageType.OTHER;

        // GỢI Ý (Nghiệp vụ): Chú thích cho ảnh
        // Ví dụ: "Ảnh đầu xe", "Ảnh đuôi xe", "Ảnh thùng hàng"
        public string? Caption { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // --- GỢI Ý BẮT BUỘC (Thuộc tính điều hướng) ---
        public virtual Vehicle Vehicle { get; set; } = null!;
    }
}