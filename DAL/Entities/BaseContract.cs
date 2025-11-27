using Common.Enums.Status;
using Common.Enums.Type;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class BaseContract
    {
        public Guid ContractId { get; set; }
        public string ContractCode { get; set; } = string.Empty;

        // --- Các bên tham gia ---
        public Guid OwnerId { get; set; } // FK to Owner
        //public Guid CounterpartyId { get; set; } // FK to User (Driver hoặc Provider)
        public ContractType Type { get; set; } // Enum (OwnerDriver, OwnerProvider)

        // --- Liên kết nghiệp vụ (BẮT BUỘC) ---

        // --- Template ---
        public Guid? ContractTemplateId { get; set; } // FK to ContractTemplate
        public string Version { get; set; } = string.Empty;

        // --- Tài chính (Gợi ý) ---
        public decimal? ContractValue { get; set; } // Giá trị hợp đồng
        public string Currency { get; set; } = "VND";

        // --- Trạng thái & Vòng đời ---
        public ContractStatus Status { get; set; }
        public DateTime CreateAt { get; set; }
        public DateTime? EffectiveDate { get; set; } // Ngày hiệu lực
        public DateTime? ExpirationDate { get; set; } // Ngày hết hạn
        public string? TerminationReason { get; set; } // Lý do chấm dứt
        public string? Note { get; set; }

        // --- Quy trình ký ---
        public bool OwnerSigned { get; set; }
        public DateTime? OwnerSignAt { get; set; }

        public bool CounterpartySigned { get; set; }
        public DateTime? CounterpartySignAt { get; set; }
        public string? FileURL { get; set; } // Link file PDF/ảnh đã ký

        // --- Thuộc tính điều hướng (BẮT BUỘC cho EF Core) ---
        public virtual Owner Owner { get; set; } = null!;
        public virtual ContractTemplate? ContractTemplate { get; set; }


    }
}