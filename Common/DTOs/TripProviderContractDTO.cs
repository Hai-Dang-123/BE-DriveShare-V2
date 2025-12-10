using System;
using Common.Enums.Status;
using Common.Enums.Type;

namespace Common.DTOs
{
    // 2. Cập nhật DTO Hợp đồng Provider
    public class TripProviderContractDTO
    {
        public Guid ContractId { get; set; }
        public string ContractCode { get; set; }

        // ... Các field cơ bản khác ...
        public Guid TripId { get; set; }
        public string TripCode { get; set; }
        public decimal? ContractValue { get; set; }
        public string Currency { get; set; }
        public string Status { get; set; } // Enum string
        public string Type { get; set; }   // Enum string
        public string FileURL { get; set; }
        public string Note { get; set; }

        // Ngày tháng
        public DateTime CreateAt { get; set; }
        public DateTime? EffectiveDate { get; set; }
        public DateTime? ExpirationDate { get; set; }

        // Chữ ký
        public bool OwnerSigned { get; set; }
        public DateTime? OwnerSignAt { get; set; }
        public bool ProviderSigned { get; set; }
        public DateTime? ProviderSignAt { get; set; }

        // Template Info
        public Guid ContractTemplateId { get; set; }
        public string TemplateName { get; set; }
        public string Version { get; set; }

        // [QUAN TRỌNG] THÔNG TIN 2 BÊN
        public ContractPartyDTO PartyA { get; set; } // Owner
        public ContractPartyDTO PartyB { get; set; } // Provider
    }
    public class CreateTripProviderContractDTO
    {
        public Guid TripId { get; set; }        // ID của chuyến đi
        public Guid ProviderId { get; set; }    // ID của Provider mà Owner muốn ký hợp đồng
    }
    public class TripProviderContractDetailDTO
    {
        public TripProviderContractDTO Contract { get; set; } = null!;
        public ContractTemplateDTO Template { get; set; } = null!;
        public List<ContractTermDTO> Terms { get; set; } = new();
    }
}
