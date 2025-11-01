using System;
using System.Collections.Generic;
using Common.Enums.Status;
using Common.Enums.Type;

namespace Common.DTOs
{
    // Hợp đồng Driver (output cơ bản)
    public class TripDriverContractDTO
    {
        public Guid ContractId { get; set; }
        public string ContractCode { get; set; } = string.Empty;

        public Guid TripId { get; set; }
        public string TripCode { get; set; } = string.Empty;

        public Guid OwnerId { get; set; }
        public string OwnerName { get; set; } = string.Empty;

        public Guid DriverId { get; set; }
        public string DriverName { get; set; } = string.Empty;

        public Guid? ContractTemplateId { get; set; }
        public string TemplateName { get; set; } = string.Empty;

        public string Version { get; set; } = string.Empty;

        public decimal? ContractValue { get; set; }
        public string Currency { get; set; } = "VND";

        public ContractStatus Status { get; set; }

        public bool OwnerSigned { get; set; }
        public DateTime? OwnerSignAt { get; set; }
        public bool DriverSigned { get; set; }
        public DateTime? DriverSignAt { get; set; }

        public string? FileURL { get; set; }
        public DateTime CreateAt { get; set; }
        public DateTime? EffectiveDate { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public string? Note { get; set; }

        public ContractType Type { get; set; } = ContractType.DRIVER_CONTRACT;
    }

    // DTO tạo hợp đồng Driver (input)
    public class CreateTripDriverContractDTO
    {
        public Guid TripId { get; set; }     // Id chuyến đi
        public Guid DriverId { get; set; }   // Id tài xế sẽ ký
    }

    // DTO trả chi tiết: Contract + Template + Terms
    public class TripDriverContractDetailDTO
    {
        public TripDriverContractDTO Contract { get; set; } = null!;
        public ContractTemplateDTO Template { get; set; } = null!;
        public List<ContractTermDTO> Terms { get; set; } = new();
    }
}
