using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Entities
{
    public class ItemImage
    {
        public Guid ItemImageId { get; set; }
        public string ItemImageURL { get; set; } = string.Empty;

        //
        public virtual Item Item { get; set; }
        public Guid ItemId { get; set; }
    }
}
