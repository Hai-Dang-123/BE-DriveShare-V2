using Common.Enums.Status;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class Item
    {
        public Guid ItemId { get; set; }
        public string ItemName { get; set; } = null!;
        public string? Description { get; set; } = null!;
        //
        public decimal? DeclaredValue { get; set; } // Giá trị khai báo (quan trọng cho bồi thường)
        public string Currency { get; set; } = "VND";
        //public string? ItemType { get; set; } // Loại hàng: "Dễ vỡ", "Đông lạnh", v.v.
        public Guid? OwnerId { get; set; } // FK to Owner
        public Guid? ProviderId { get; set; } // FK to Provider
        
        public ItemStatus Status { get; set; }  // Trạng thái hiện tại của hàng hóa
        public virtual Owner? Owner { get; set; }
        public virtual Provider? Provider { get; set; }
        // Mối quan hệ 1-1 với Package
        // Giả định Item là principal, Package là dependent (Package sẽ có ItemId)
        public virtual Package Package { get; set; } = null!;
        // Mối quan hệ 1-n với ItemImage
        public virtual ICollection<ItemImage> ItemImages { get; set; } = new List<ItemImage>();
    }
}
