using Common.Enums.Status;
using DAL.Context;
using DAL.Entities;
using DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Repositories.Implement
{
    public class ItemImageRepository : GenericRepository<ItemImage>, IItemImageRepository
    {
        private readonly DriverShareAppContext _context;
        public ItemImageRepository (DriverShareAppContext context) : base (context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ItemImage>> GetAllByItemIdAsync(Guid itemId)
        {
            return await _context.ItemImages
               .Where(ii => ii.ItemId == itemId && ii.Status == ItemImageStatus.Active)
               .ToListAsync();
        }
    }
}
