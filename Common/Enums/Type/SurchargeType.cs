using System.ComponentModel;

namespace Common.Enums.Type
{
    public enum SurchargeType
    {
        // ===============================================================
        // NHÓM 1: LIÊN QUAN ĐẾN PHƯƠNG TIỆN (VEHICLE HANDOVER ISSUES)
        // ===============================================================

        VEHICLE_DAMAGE, // Phạt xe bị hư hỏng khi giao nhận (tài xế chịu trách nhiệm)
        VEHICLE_DIRTY,   // Phạt xe bẩn khi giao nhận (tài xế chịu trách nhiệm)

        // --- NGOẠI THẤT (BODYWORK) ---
        [Description("Trầy xước")]
        SCRATCH,

        [Description("Móp méo")]
        DENT,

        [Description("Nứt/Vỡ")]
        CRACK, // Kính, đèn, gương

        [Description("Tróc sơn")]
        PAINT_PEELING,

        // --- VỆ SINH (CLEANLINESS) ---
        [Description("Dơ bẩn/Cần vệ sinh")]
        DIRTY,

        [Description("Có mùi hôi")]
        ODOR, // Mùi thuốc lá, ẩm mốc...

        // --- VẬN HÀNH & KỸ THUẬT (MECHANICAL) ---
        [Description("Lỗi động cơ/Máy móc")]
        MECHANICAL, // Check engine, tiếng kêu lạ

        [Description("Lỗi hệ thống điện")]
        ELECTRICAL , // Đèn, còi, màn hình, điều hòa

        [Description("Lỗi lốp xe")]
        TIRE , // Thủng, mòn, non hơi

        // --- TÀI SẢN (INVENTORY) ---
        [Description("Mất phụ kiện/Giấy tờ")]
        MISSING_ITEM, // Lốp dự phòng, kích, giấy tờ



        // ===============================================================
        // NHÓM 2: LIÊN QUAN ĐẾN HÀNG HÓA (CARGO DELIVERY ISSUES)
        // ===============================================================

        [Description("Phạt hàng hóa hư hỏng")]
        CARGO_DAMAGE, // Hàng vỡ, móp, ướt, hư hại trong quá trình vận chuyển

        [Description("Phạt mất mát/Thất lạc hàng")]
        CARGO_LOSS,   // Mất hàng, thiếu số lượng so với biên bản

        [Description("Phạt giao hàng trễ")]
        LATE_DELIVERY, // Giao hàng trễ giờ cam kết (ảnh hưởng đến Provider)

        [Description("Phạt giao sai hàng/địa điểm")]
        MISDELIVERY,   // Giao nhầm hàng hoặc nhầm kho

        [Description("Phí bốc xếp phát sinh")]
        LOADING_UNLOADING_EXTRA, // Nếu tài xế không chịu bốc xếp mà Owner phải thuê ngoài (phạt lại tài xế)

        // ===============================================================
        // NHÓM 3: PHÁP LÝ & KHÁC
        // ===============================================================

        [Description("Phạt vi phạm giao thông")]
        TRAFFIC_VIOLATION, // Phạt nguội, lấn làn, quá tốc độ (Chủ xe bị báo phạt và thu lại từ tài xế)

        [Description("Phí cầu đường không hợp lệ")]
        UNAUTHORIZED_TOLL_FEE, // Đi đường tốn phí khi không được phép

        [Description("Khác")]
        OTHER
    }
}