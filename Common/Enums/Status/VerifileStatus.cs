using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Enums.Status
{
    public enum VerifileStatus
    {
        ACTIVE,
        INACTIVE,
        DELETED,
        PENDING_REVIEW , //  Đang chờ nhân viên duyệt
        REJECTED         //  Nhân viên đã từ chối
    }
}
