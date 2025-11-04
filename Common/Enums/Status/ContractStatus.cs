using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Enums.Status
{
    public enum ContractStatus
    {
        PENDING,
        COMPLETED,
        AWAITING_CONTRACT_SIGNATURE,
        CANCELLED,
        REJECTED
    }
}
