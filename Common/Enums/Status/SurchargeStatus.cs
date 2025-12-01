namespace Common.Enums.Status
{
    public enum SurchargeStatus
    {
        PENDING = 1,    // Mới tạo, chưa trả
        PAID = 2,       // Đã thanh toán
        DISPUTING = 3,  // Khách đang khiếu nại khoản này
        WAIVED = 4,     // Chủ xe tha, không thu nữa
        CANCELLED = 5   // Huỷ
    }
}