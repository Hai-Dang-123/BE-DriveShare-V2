using DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Repositories.Interface
{
    public interface IItemRepository : IGenericRepository<Item>
    {
        Task<IEnumerable<Item>> GetAllItemsAsync();
        Task<IEnumerable<Item>> GetItemsByUserIdAsync(Guid UserId);
        Task<Item> GetItemByIdAsync(Guid itemId);

        IQueryable<Item> GetItemsByUserIdQueryable(Guid userId);
        IQueryable<Item> GetAllItemsQueryable();



    }
}
