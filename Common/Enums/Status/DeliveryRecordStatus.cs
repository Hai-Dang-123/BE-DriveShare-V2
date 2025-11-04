using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Enums.Status
{
    public enum DeliveryRecordStatus
    {
        PENDING,
        AWAITING_DELIVERY_RECORD_SIGNATURE,
        COMPLETED,
        CANCELLED,
        REJECTED
    }
}
