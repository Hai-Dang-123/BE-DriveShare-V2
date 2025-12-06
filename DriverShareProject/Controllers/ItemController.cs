using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ItemController : ControllerBase
    {
        private readonly IItemService _itemService;
        public ItemController(IItemService itemService)
        {
            _itemService = itemService;
        }
        [HttpPost("owner-create-item")]
        public async Task<IActionResult> OwnerCreateItem([FromBody] ItemCreateDTO itemCreateDTO)
        {
            var result = await _itemService.OwnerCreateItemAsync(itemCreateDTO);
            return StatusCode(result.StatusCode, result);
        }
        [HttpPost("provider-create-item")]
        public async Task<IActionResult> ProviderCreateItem([FromForm] ItemCreateDTO itemCreateDTO)
        {
            var result = await _itemService.ProviderCreateItemAsync(itemCreateDTO);
            return StatusCode(result.StatusCode, result);
        }
        [HttpGet("get-item-by-id/{itemId}")]
        public async Task<IActionResult> GetItemById([FromRoute] Guid itemId)
        {
            var result = await _itemService.GetItemByIdAsync(itemId);
            return StatusCode(result.StatusCode, result);
        }


        [HttpPut("update-item")]
        public async Task<IActionResult> UpdateItem([FromBody] ItemUpdateDTO itemUpdateDTO)
        {
            var result = await _itemService.UpdateItemAsync(itemUpdateDTO);
            return StatusCode(result.StatusCode, result);
        }
        [HttpDelete("delete-item/{itemId}")]
        public async Task<IActionResult> DeleteItem([FromRoute] Guid itemId)
        {
            var result = await _itemService.DeleteItemAsync(itemId);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Lấy danh sách hàng hóa của User đang đăng nhập (Trừ Deleted)
        /// </summary>
        [HttpGet("get-items-by-user-id")]
        //[Authorize] // Bắt buộc đăng nhập
        public async Task<IActionResult> GetItemsByUserId(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = "ASC")
        {
            // Lưu ý: Không cần truyền userId từ Route vì Service tự lấy từ Token
            var result = await _itemService.GetItemsByUserIdAsync(pageNumber, pageSize, search, sortBy, sortOrder);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Lấy tất cả hàng hóa trong hệ thống (Admin/Public)
        /// </summary>
        [HttpGet("get-all-items")]
        public async Task<IActionResult> GetAllItems(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = "ASC")
        {
            var result = await _itemService.GetAllItemsAsync(pageNumber, pageSize, search, sortBy, sortOrder);
            return StatusCode(result.StatusCode, result);
        }

        /// <summary>
        /// Lấy danh sách hàng hóa PENDING của User đang đăng nhập
        /// </summary>
        [HttpGet("get-pending-by-user")]
        //[Authorize] // Bắt buộc đăng nhập
        public async Task<IActionResult> GetPendingItemsByUser(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = "ASC")
        {
            var result = await _itemService.GetPendingItemsByUserIdAsync(pageNumber, pageSize, search, sortBy, sortOrder);
            return StatusCode(result.StatusCode, result);
        }
    }
    }
