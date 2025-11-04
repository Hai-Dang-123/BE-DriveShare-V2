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
    public class ItemRepository : GenericRepository<Item>, IItemRepository
    {
        private readonly DriverShareAppContext _context;
        public ItemRepository (DriverShareAppContext context) : base(context)
        {
            _context = context;
        }
        public async Task<IEnumerable<Item>> GetItemsByUserIdAsync(Guid userId)
        {
            return await _context.Items
                .Include(i => i.ItemImages)
                .Where(i => (i.OwnerId == userId || i.ProviderId == userId) )
                .ToListAsync();
        }

        public async Task<IEnumerable<Item>> GetAllItemsAsync()
        {
            return await _context.Items
                .Include(i => i.ItemImages)
                //.Where (i => i.Status == ItemStatus.PENDING)
                .ToListAsync();
        }

        public async Task<Item> GetItemByIdAsync(Guid itemId)
        {
            return await _context.Items
                .Include(i => i.ItemImages)
                .Include(i => i.Package)
                .Include(i => i.Owner)
                .FirstOrDefaultAsync(i => i.ItemId == itemId);
        }
    }
}
