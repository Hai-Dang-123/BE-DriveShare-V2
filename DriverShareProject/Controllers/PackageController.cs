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
    public class PackageController : ControllerBase
    {
        private readonly IPackageService _packageService;
        public PackageController(IPackageService packageService)
        {
            _packageService = packageService;
        }
        [HttpPost("owner-create-package")]
        public async Task<IActionResult> OwnerCreatePackage([FromBody] PackageCreateDTO packageCreateDTO)
        {
            var result = await _packageService.OwnerCreatePackageAsync(packageCreateDTO);
            return StatusCode(result.StatusCode, result);
        }
        [HttpPost("provider-create-package")]
        public async Task<IActionResult> ProviderCreatePackage([FromBody] PackageCreateDTO packageCreateDTO)
        {
            var result = await _packageService.ProviderCreatePackageAsync(packageCreateDTO);
            return StatusCode(result.StatusCode, result);

        }
        [HttpGet("get-package-by-id/{packageId}")]
        public async Task<IActionResult> GetPackageById([FromRoute] Guid packageId)
        {
            var result = await _packageService.GetPackageByIdAsync(packageId);
            return StatusCode(result.StatusCode, result);
        }
        [HttpGet("get-all-packages")]
        public async Task<IActionResult> GetAllPackages()
        {
            var result = await _packageService.GetAllPackagesAsync();
            return StatusCode(result.StatusCode, result);
        }
        [HttpPut("update-package")]
        public async Task<IActionResult> UpdatePackage([FromBody] PackageUpdateDTO packageUpdateDTO)
        {
            var result = await _packageService.UpdatePackageAsync(packageUpdateDTO);
            return StatusCode(result.StatusCode, result);
        }
        [HttpDelete("delete-package/{packageId}")]
        public async Task<IActionResult> DeletePackage([FromRoute] Guid packageId)
        {
            var result = await _packageService.DeletePackageAsync(packageId);
            return StatusCode(result.StatusCode, result);
        }
    }
    }
