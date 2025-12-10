using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Enums.Status
{
    public enum DepositStatus
    {
        NOT_REQUIRED, // Không cần cọc
        PENDING,      // Chờ nộp
        DEPOSITED,    // Đã nộp
        REFUNDED,     // Đã hoàn trả (khi xong chuyến)
        SEIZED        // Bị tịch thu (do vi phạm)
    }
}
