using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Enums.Type
{
    public enum TransactionType
    {
        // === CỘNG TIỀN (System -> User) ===
        TOPUP,         // Nạp tiền
        OWNER_PAYOUT,  // Trả tiền cho Owner (Giai đoạn cuối)
        DRIVER_PAYOUT, // Trả tiền cho Driver (Giai đoạn cuối)
        REFUND,        // Hoàn tiền

        // === TRỪ TIỀN (User -> System) ===
        WITHDRAWAL,             // Rút tiền
        POST_TRIP_PAYMENT,           // Provider thanh toán cho Trip
        POST_PAYMENT,           // Provider thanh toán khi đăng bài post
        PLATFORM_FEE,           // Phí nền tảng
        DRIVER_SERVICE_PAYMENT,  // Owner thanh toán tiền thuê tài xế (cho nền tảng giữ)

        PENALTY,               // Phạt vi phạm
        DEPOSIT,               // Tiền cọc

        // tiền đang nợ nền tảng ( chưa thanh toán )
        OUTSTANDING_PAYMENT,

        // tiền thanh toán cho nền tảng
        PLATFORM_PAYMENT,

        COMPENSATION,         // bồi thường
        COMMISSION


    }

}
