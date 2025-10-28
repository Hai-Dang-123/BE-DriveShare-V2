using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Enums.Type
{
    public enum TransactionType
    {
        TOPUP,         // Nạp tiền vào ví
        WITHDRAWAL,    // Rút tiền ra khỏi ví
        TRIP_PAYMENT,  // Thanh toán cho chuyến đi
        DRIVER_PAYOUT, // Chi trả cho tài xế
        PLATFORM_FEE,  // Phí nền tảng thu
        REFUND,        // Hoàn tiền
    }

}
