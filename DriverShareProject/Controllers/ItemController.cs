using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
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
        public async Task<IActionResult> ProviderCreateItem([FromBody] ItemCreateDTO itemCreateDTO)
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
        [HttpGet("get-all-items")]
        public async Task<IActionResult> GetAllItems()
        {
            var result = await _itemService.GetAllItemsAsync();
            return StatusCode(result.StatusCode, result);
        }
        [HttpGet("get-items-by-owner-id/{userId}")]
        public async Task<IActionResult> GetItemsByOwnerId([FromRoute] Guid userId)
        {
            var result = await _itemService.GetItemsByOwnerIdAsync(userId);
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
    }
    }
