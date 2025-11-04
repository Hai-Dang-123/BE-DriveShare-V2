using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class DeliveryRecordTemplateDTO
    {
        public Guid DeliveryRecordTemplateId { get; set; }
        public string TemplateName { get; set; } = null!;
        public string Version { get; set; } = null!;
        public string Type { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public List<DeliveryRecordTermDTO> DeliveryRecordTerms { get; set; } = new List<DeliveryRecordTermDTO>();

    }
   
}
