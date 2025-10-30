using Common.Enums.Status;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class PackageImage
    {
        public Guid PackageImageId { get; set; }
        public string PackageImageURL { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public PackageImageStatus Status { get; set; }
        //
        public virtual Package Package { get; set; }
        public Guid PackageId { get; set; }
    }
}
