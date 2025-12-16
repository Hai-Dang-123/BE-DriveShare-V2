using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Enums.Status
{
    public enum PackageStatus
    {
        PENDING,     // KHỞI TẠO
        LOOKING_FOR_OWNER, // ĐANG TÌM CHỦ HÀNG
        IN_PROGRESS, // ĐANG VẬN CHUYỂN
        DELETED,    // XÓA
        COMPLETED,   // HOÀN THÀNH
        REJECTED     // TỪ CHỐI
    }
}
