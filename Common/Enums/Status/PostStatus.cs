using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Enums.Status
{
    public enum PostStatus
    {
        // ───────────────   ───────────────
        PENDING, // KHỞI TẠO
        AWAITING_SIGNATURE, // CHỜ KÝ
        AWAITING_PAYMENT, // CHỜ THANH TOÁN
        OPEN, // ĐANG HOẠT ĐỘNG
        IN_PROGRESS, // ĐANG DI CHUYỂN
        DONE, // HOÀN THÀNH
        OUT_OF_DATE, // HẾT HẠN
        DELETED // XÓA
    }
}
