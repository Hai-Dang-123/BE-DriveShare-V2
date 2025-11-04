using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    internal class TripDeliveryIssueImageDTO
    {
    }
    public class TripDeliveryIssueImageCreateDTO
    {
        public IFormFile Files { get; set; } 
        public string? Caption { get; set; }
        public Guid TripDeliveryIssueId { get; set; }
    }
   
    
}
