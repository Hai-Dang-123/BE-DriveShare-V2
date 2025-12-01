using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Enums.Type
{
    public enum VehicleIssueType
    {
        // --- NGOẠI THẤT (BODYWORK) ---
        [Description("Trầy xước")]
        SCRATCH = 1,

        [Description("Móp méo")]
        DENT = 2,

        [Description("Nứt/Vỡ")]
        CRACK = 3, // Kính, đèn, gương

        [Description("Tróc sơn")]
        PAINT_PEELING = 4,

        // --- VỆ SINH (CLEANLINESS) ---
        [Description("Dơ bẩn/Cần vệ sinh")]
        DIRTY = 10,

        [Description("Có mùi hôi")]
        ODOR = 11, // Mùi thuốc lá, ẩm mốc...

        // --- VẬN HÀNH & KỸ THUẬT (MECHANICAL) ---
        [Description("Lỗi động cơ/Máy móc")]
        MECHANICAL = 20, // Check engine, tiếng kêu lạ

        [Description("Lỗi hệ thống điện")]
        ELECTRICAL = 21, // Đèn, còi, màn hình, điều hòa

        [Description("Lỗi lốp xe")]
        TIRE = 22, // Thủng, mòn, non hơi

        // --- TÀI SẢN (INVENTORY) ---
        [Description("Mất phụ kiện/Giấy tờ")]
        MISSING_ITEM = 30, // Lốp dự phòng, kích, giấy tờ

        // --- KHÁC ---
        [Description("Khác")]
        OTHER = 99
    }
}
