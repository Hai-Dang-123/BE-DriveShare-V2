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
        Task<ResponseDTO> GetAllItemsAsync();
        Task<ResponseDTO> GetItemsByOwnerIdAsync(Guid UserId);
        Task<ResponseDTO> UpdateItemAsync(ItemUpdateDTO itemUpdateDTO);
        Task<ResponseDTO> DeleteItemAsync(Guid itemId);
    }
}
