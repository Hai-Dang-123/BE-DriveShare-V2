using System;
using System.Collections.Generic;
using Common.Enums.Status;
using Common.Enums.Type;

namespace Common.DTOs
{
    // Hợp đồng Driver (output cơ bản)
    // 3. Cập nhật DTO Hợp đồng Driver
    public class TripDriverContractDTO
    {
        public Guid ContractId { get; set; }
        public string ContractCode { get; set; }

        // ... Các field cơ bản ...
        public Guid TripId { get; set; }
        public string TripCode { get; set; }
        public decimal? ContractValue { get; set; }
        public string Currency { get; set; }
        public string Status { get; set; }
        public string Type { get; set; }
        public string FileURL { get; set; }
        public string Note { get; set; }

        public DateTime CreateAt { get; set; }
        public DateTime? EffectiveDate { get; set; }
        public DateTime? ExpirationDate { get; set; }

        public bool OwnerSigned { get; set; }
        public DateTime? OwnerSignAt { get; set; }
        public bool DriverSigned { get; set; }
        public DateTime? DriverSignAt { get; set; }

        public Guid ContractTemplateId { get; set; }
        public string TemplateName { get; set; }
        public string Version { get; set; }

        // [QUAN TRỌNG] THÔNG TIN 2 BÊN
        public ContractPartyDTO PartyA { get; set; } // Owner
        public ContractPartyDTO PartyB { get; set; } // Driver
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
