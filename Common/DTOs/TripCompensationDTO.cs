using Common.Enums.Status;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    //public class TripCompensationDTO
    //{
    //}
    public class TripCompensationCreateDTO
    {
        public Guid TripId { get; set; } 
        public Guid? IssueId { get; set; }
        public decimal Amount { get; set; }
        public string Reason { get; set; } = string.Empty;
        
    }
}
