using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Enums.Status
{
    public enum TransactionStatus
    {
        PENDING,
        WAITING_FOR_CONFIRMATION,
        SUCCEEDED,
        FAILED
    }
}
