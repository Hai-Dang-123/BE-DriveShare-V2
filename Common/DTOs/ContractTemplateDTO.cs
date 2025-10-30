using Common.Enums.Type;
using Microsoft.AspNetCore.Http;
using System;

namespace Common.DTOs
{
    public class ContractTemplateCreateDTO
    {
        public string ContractTemplateName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public ContractType Type { get; set; }
    }

    public class ContractTemplateUpdateDTO
    {
        public Guid ContractTemplateId { get; set; }
        public string? ContractTemplateName { get; set; }
        public string? Version { get; set; }
        public ContractType? Type { get; set; }
    }

    public class ContractTemplateDetailDTO
    {
        public Guid ContractTemplateId { get; set; }
        public string ContractTemplateName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public ContractType Type { get; set; }

        public List<ContractTermDTO> ContractTerms { get; set; } = new List<ContractTermDTO>();
    }

    public class ContractTemplateDTO
    {
        public Guid ContractTemplateId { get; set; }
        public string ContractTemplateName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public ContractType Type { get; set; }

    }

    public class GetDetailContractTemplateDTO
    {
        public Guid ContractTemplateId { get; set; }
        public string ContractTemplateName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public ContractType Type { get; set; }
        public List<ContractTermDetailDTO> ContractTerms { get; set; }
    }
}
