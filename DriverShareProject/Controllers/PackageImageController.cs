using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    //[Authorize]
    public class PackageImageController : ControllerBase
    {
        private readonly IPackageImageService _packageImageService;
        public PackageImageController(IPackageImageService packageImageService)
        {
            _packageImageService = packageImageService;
        }
        [HttpPost("create")]
        public async Task<IActionResult> CreatePackageImage([FromForm] PackageImageCreateDTO packageImageCreateDTO)
        {
            var result = await _packageImageService.CreatePackageImageAsync(packageImageCreateDTO);
           
                return StatusCode(result.StatusCode, result);        
        }
        [HttpPost("delete")]
        public async Task<IActionResult> DeletePackageImage([FromBody]  Guid packageImageID)
        {
            var result = await _packageImageService.DeletePackageImageAsync(packageImageID);
            return StatusCode(result.StatusCode, result);
        }
        [HttpGet("get-by-package-id/{packageId}")]
        public async Task<IActionResult> GetAllPackageImagesByPackageId([FromRoute] Guid packageId)
        {
            var result = await _packageImageService.GetAllPackageImagesByPackageIdAsync(packageId);
            return StatusCode(result.StatusCode, result);
        }
        [HttpPut("update")]
        public async Task<IActionResult> UpdatePackageImage([FromBody] UpdatePackageImageDTO updatePackageImageDTO)
        {
            var result = await _packageImageService.UpdatePackageImageAsync(updatePackageImageDTO);
            return StatusCode(result.StatusCode, result);
        }
        [HttpGet("get-by-id/{packageImageId}")]
        public async Task<IActionResult> GetPackageImageById([FromRoute] Guid packageImageId)
        {
            var result = await _packageImageService.GetPackageImageByIdAsync(packageImageId);
            return StatusCode(result.StatusCode, result);
        }




    }
}
