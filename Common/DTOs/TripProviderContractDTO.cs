using System;
using Common.Enums.Status;
using Common.Enums.Type;

namespace Common.DTOs
{
    public class TripProviderContractDTO
    {
        public Guid ContractId { get; set; }
        public string ContractCode { get; set; } = string.Empty;

        // --- Liên kết Trip ---
        public Guid TripId { get; set; }
        public string? TripCode { get; set; }

        // --- Các bên ---
        public Guid OwnerId { get; set; }
        public string? OwnerName { get; set; }

        public Guid ProviderId { get; set; }
        public string? ProviderName { get; set; }

        // --- Template ---
        public Guid? ContractTemplateId { get; set; }
        public string? TemplateName { get; set; }
        public string Version { get; set; } = string.Empty;

        // --- Giá trị hợp đồng ---
        public decimal? ContractValue { get; set; }
        public string Currency { get; set; } = "VND";

        // --- Trạng thái ---
        public ContractStatus Status { get; set; }
        public string? StatusText => Status.ToString();

        // --- Quy trình ký ---
        public bool OwnerSigned { get; set; }
        public DateTime? OwnerSignAt { get; set; }

        public bool ProviderSigned { get; set; }
        public DateTime? ProviderSignAt { get; set; }

        public string? FileURL { get; set; }

        // --- Thời gian hiệu lực ---
        public DateTime CreateAt { get; set; }
        public DateTime? EffectiveDate { get; set; }
        public DateTime? ExpirationDate { get; set; }

        // --- Ghi chú ---
        public string? Note { get; set; }

        // --- Phân loại ---
        public ContractType Type { get; set; } = ContractType.PROVIDER_CONTRACT;
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
