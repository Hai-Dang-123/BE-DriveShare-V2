using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class DeliveryRecordTermDTO
    {
        public Guid DeliveryRecordTermId { get; set; }
        public Guid DeliveryRecordTemplateId { get; set; }
        public string Content { get; set; }
        public int DisplayOrder { get; set; }
    }
}
