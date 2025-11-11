using Common.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IItemService
    {
        Task<ResponseDTO> OwnerCreateItemAsync(ItemCreateDTO itemCreateDTO);
        Task<ResponseDTO> ProviderCreateItemAsync(ItemCreateDTO itemCreateDTO);
        Task<ResponseDTO> GetItemByIdAsync(Guid itemId);
        Task<ResponseDTO> GetAllItemsAsync(int pageNumber, int pageSize);
        Task<ResponseDTO> GetItemsByUserIdAsync(int pageNumber, int pageSize);
        Task<ResponseDTO> UpdateItemAsync(ItemUpdateDTO itemUpdateDTO);
        Task<ResponseDTO> DeleteItemAsync(Guid itemId);
        Task<ResponseDTO> GetPendingItemsByUserIdAsync(int pageNumber, int pageSize);
    }
}
