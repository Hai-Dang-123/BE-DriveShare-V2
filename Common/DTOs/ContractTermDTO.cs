using System;

namespace Common.DTOs
{
    public class ContractTermCreateDTO
    {
        public string Content { get; set; } = string.Empty;
        public int Order { get; set; }
        public Guid ContractTemplateId { get; set; }
    }

    public class ContractTermUpdateDTO
    {
        public Guid ContractTermId { get; set; }
        public string? Content { get; set; }
        public int? Order { get; set; }
    }

    public class ContractTermDetailDTO
    {
        public Guid ContractTermId { get; set; }
        public string Content { get; set; } = string.Empty;
        public int Order { get; set; }
        public Guid ContractTemplateId { get; set; }
    }

    public class ContractTermDTO
    {
        public Guid ContractTermId { get; set; }
        public string Content { get; set; } = string.Empty;
        public int Order { get; set; }
        public Guid ContractTemplateId { get; set; }
    }
}
