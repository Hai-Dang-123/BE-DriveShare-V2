using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class TripDriverContract : BaseContract
    {
        public Guid TripId { get; set; }
        public virtual Trip Trip { get; set; } = null!;
        public Guid CounterpartyId { get; set; }
        public virtual Driver Counterparty { get; set; } = null!; 
    }
}
