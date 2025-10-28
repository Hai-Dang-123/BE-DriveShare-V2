using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class ContractTerm
    {
        public Guid ContractTermId { get; set; }
        public string Content { get; set; } = null!;
        public int Order { get; set; }  

        //
        public virtual ContractTemplate ContractTemplate { get; set; }
        public Guid ContractTemplateId { get; set; }
    }
}
