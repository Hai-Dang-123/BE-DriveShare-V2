using System.ComponentModel;

namespace Common.Enums.Type
{
    public enum SurchargeType
    {
        // ===============================================================
        // NHÓM 1: LIÊN QUAN ĐẾN PHƯƠNG TIỆN (VEHICLE HANDOVER ISSUES)
        // ===============================================================

        [Description("Phí nhiên liệu")]
        FUEL, // Trả thiếu xăng/dầu so với lúc nhận

        [Description("Phí quá giới hạn Km")]
        DISTANCE, // Chạy lố số km khoán (nếu có)

        [Description("Phí trả xe chậm")]
        LATE_RETURN_VEHICLE, // Trả xe trễ giờ so với hợp đồng thuê

        [Description("Phí vệ sinh xe")]
        CLEANING, // Xe quá bẩn, ám mùi, cần dọn nội thất

        [Description("Phí sửa chữa hư hỏng xe")]
        VEHICLE_DAMAGE, // Trầy xước, móp méo, vỡ đèn, hỏng lốp...

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