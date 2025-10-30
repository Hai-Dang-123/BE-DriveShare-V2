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
    public class ItemImagesController : ControllerBase
    {
        private readonly IItemImagesService _itemImagesService;
        public ItemImagesController(IItemImagesService itemImagesService)
        {
            _itemImagesService = itemImagesService;
        }
        [HttpPost("Create-ItemImage")]
        public async Task<IActionResult> CreateItemImage([FromForm] ItemImageCreateDTO dto)
        {
            var response = await _itemImagesService.CreateItemImageAsync(dto);
            return StatusCode(response.StatusCode, response);
        }
        [HttpGet("getall-itemimage/{itemId}")]
        public async Task<IActionResult> GetAllByItemId(Guid itemId)
        {
            var response = await _itemImagesService.GetALlItemImagesByItemIdAsync(itemId);
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("get-itemimage-byId{itemImageId}")]
        public async Task<IActionResult> GetItemImageById(Guid itemImageId)
        {
            var response = await _itemImagesService.GetItemImageByIdAsync(itemImageId);
            return StatusCode(response.StatusCode, response);
        }
        [HttpPut("Update-itemage")]
        public async Task<IActionResult> UpdateItemImage([FromForm] UpdateItemImageDTO dto)
        {
            var response = await _itemImagesService.UpdateItemImageAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        
        [HttpDelete("delete-itemimage/{itemImageId}")]
        public async Task<IActionResult> DeleteItemImage(Guid itemImageId)
        {
            var response = await _itemImagesService.DeleteItemImageAsync(itemImageId);
            return StatusCode(response.StatusCode, response);
        }

    }
}
