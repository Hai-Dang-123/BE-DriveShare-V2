using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAL.Entities
{
    public class PackageHandlingDetail
    {
        [Key, ForeignKey("Package")]
        public Guid PackageId { get; set; } // Khóa chính cũng là khóa ngoại trỏ về Package

        // --- CÁC THUỘC TÍNH BOOL (Checklist) ---
        public bool IsFragile { get; set; }       // Dễ vỡ
        public bool IsLiquid { get; set; }        // Chất lỏng
        public bool IsRefrigerated { get; set; }  // Cần bảo quản lạnh
        public bool IsFlammable { get; set; }     // Dễ cháy/nổ
        public bool IsHazardous { get; set; }     // Hóa chất độc hại
        public bool IsBulky { get; set; }         // Cồng kềnh
        public bool IsPerishable { get; set; }    // Dễ hỏng (thực phẩm tươi sống)

        // Các yêu cầu khác (Text)
        public string? OtherRequirements { get; set; }

        // Navigation Property
        public virtual Package Package { get; set; } = null!;
    }
}