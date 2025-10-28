using Common.Enums.Type;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class ContractTemplate
    {
        public Guid ContractTemplateId { get; set; }
        public string ContractTemplateName { get; set; } = null!; 
        public string Version { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ContractType Type { get; set; }
        //
        public virtual ICollection<ContractTerm> ContractTerms { get; set; }
        public virtual ICollection<BaseContract> BaseContracts { get; set; } = new List<BaseContract>();

    }

}
