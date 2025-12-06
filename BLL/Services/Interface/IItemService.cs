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
        // 1. Lấy tất cả Items (Admin/Public)
        Task<ResponseDTO> GetAllItemsAsync(int pageNumber, int pageSize, string? search, string? sortBy, string? sortOrder);

        // 2. Lấy Items của User đang đăng nhập (Trừ Deleted)
        Task<ResponseDTO> GetItemsByUserIdAsync(int pageNumber, int pageSize, string? search, string? sortBy, string? sortOrder);

        // 3. Lấy Items đang Pending của User đang đăng nhập
        Task<ResponseDTO> GetPendingItemsByUserIdAsync(int pageNumber, int pageSize, string? search, string? sortBy, string? sortOrder);
        Task<ResponseDTO> UpdateItemAsync(ItemUpdateDTO itemUpdateDTO);
        Task<ResponseDTO> DeleteItemAsync(Guid itemId);
    }
}
