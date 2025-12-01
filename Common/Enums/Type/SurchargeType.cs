using System.ComponentModel;

namespace Common.Enums.Type
{
    public enum SurchargeType
    {
        [Description("Phí nhiên liệu")]
        FUEL = 1, // Trả thiếu xăng

        [Description("Phí quá giới hạn Km")]
        DISTANCE = 2, // Vượt số km cho phép/ngày

        [Description("Phí trả chậm")]
        LATE_RETURN = 3, // Trả xe muộn giờ

        [Description("Phí vệ sinh")]
        CLEANING = 4, // Xe quá bẩn

        [Description("Phí sửa chữa/Hư hỏng")]
        DAMAGE = 5, // Trầy xước, móp méo

        [Description("Phí cầu đường/Phạt nguội")]
        TOLL_FEE = 6,

        [Description("Khác")]
        OTHER = 99
    }
}