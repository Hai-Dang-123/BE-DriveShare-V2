using Common.Enums.Status;
using Common.Enums.Type;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    //public class TripDeliveryIssueDTO
    //{
    //}
    public class TripDeliveryIssueCreateDTO
    {
        public Guid TripDeliveryIssueId { get; set; }
        public Guid TripId { get; set; } 
        public Guid? DeliveryRecordId { get; set; }
        public DeliveryIssueType IssueType { get; set; }
        public string Description { get; set; } 

    }
}
