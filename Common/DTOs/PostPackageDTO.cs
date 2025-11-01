using Common.Enums.Status;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class PostPackageDTO
    {
    }
    public class PostPackageCreateDTO
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal OfferedPrice { get; set; }       
        public Guid ShippingRouteId { get; set; } 
    }
    public class  ChangePostPackageStatusDTO
    {
        public Guid PostPackageId { get; set; }
        public PostStatus NewStatus { get; set; }
    }

}
