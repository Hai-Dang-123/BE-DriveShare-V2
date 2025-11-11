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


        // Giả sử bạn có DbContext là _context
        // và DbSet<Item> là _context.Items

        public IQueryable<Item> GetItemsByUserIdQueryable(Guid userId)
        {
            // Quan trọng: Trả về IQueryable, không .ToListAsync()
            return _context.Items
                .AsNoTracking() // Tăng hiệu suất cho truy vấn đọc
                .Where(i => i.OwnerId == userId || i.ProviderId == userId) // (Logic này dựa trên các entity của bạn, item có cả OwnerId và ProviderId)
                .Include(i => i.ItemImages) // *BẮT BUỘC* Include để map DTO
                .OrderByDescending(i => i.ItemId); // *NÊN CÓ* OrderBy để phân trang nhất quán
        }

        public IQueryable<Item> GetAllItemsQueryable()
        {
            // Quan trọng: Trả về IQueryable, không .ToListAsync()
            return _context.Items
                .AsNoTracking()
                .Include(i => i.ItemImages) // *BẮT BUỘC* Include
                .OrderByDescending(i => i.ItemId); // *NÊN CÓ* OrderBy
        }
    }
}
