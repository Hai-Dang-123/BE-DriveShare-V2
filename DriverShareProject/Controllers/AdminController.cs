using BLL.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly IRoleService _roleService;
        private readonly IAdminservices _adminServices;
        public AdminController(IRoleService roleService, IAdminservices adminServices)
        {
            _roleService = roleService;
            _adminServices = adminServices;
        }
        [HttpGet("roles")]
        public async Task<IActionResult> GetAllRoles()
        {
            var response = await _roleService.GetAllRole();
            if (response.IsSuccess)
            {
                return Ok(response);
            }
            return StatusCode(response.StatusCode, response);
        }
        [HttpGet("overview")]
        public async Task<IActionResult> GetOverview()
        {
            var result = await _adminServices.GetOverview();
            return StatusCode(result.StatusCode, result);
        }
        [HttpGet("users/by-role")]
        public async Task<IActionResult> GetUserCountByRole()
        {
            var result = await _adminServices.GetUserCountByRole();
            return StatusCode(result.StatusCode, result);
        }
        [HttpGet("users/registration")]
        public async Task<IActionResult> GetUserRegistrationStats(
           [FromQuery] DateTime from,
           [FromQuery] DateTime to,
           [FromQuery] string groupBy = "day")
        {
            var result = await _adminServices.GetUserRegistrationStats(from, to, groupBy);
            return StatusCode(result.StatusCode, result);
        }
        [HttpGet("trips/created")]
        public async Task<IActionResult> GetTripCreatedStats(
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            [FromQuery] string groupBy = "day")
        {
            var result = await _adminServices.GetTripCreatedStats(from, to, groupBy);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("packages/by-status")]
        public async Task<IActionResult> GetPackageStatsByStatus()
        {
            var result = await _adminServices.GetPackageStatsByStatus();
            return StatusCode(result.StatusCode, result);
        }
        [HttpGet("packages/created")]
        public async Task<IActionResult> GetPackageCreatedStats(
           [FromQuery] DateTime from,
           [FromQuery] DateTime to,
           [FromQuery] string groupBy = "day")
        {
            var result = await _adminServices.GetPackageCreatedStats(from, to, groupBy);
            return StatusCode(result.StatusCode, result);
        }
       
        [HttpGet("revenue")]
        public async Task<IActionResult> GetRevenueStats(
          [FromQuery] DateTime from,
          [FromQuery] DateTime to,
          [FromQuery] string groupBy = "month")
        {
            var result = await _adminServices.GetRevenueStats(from, to, groupBy);
            return StatusCode(result.StatusCode, result);
        }
        [HttpGet("trips/by-status")]
        public async Task<IActionResult> GetTripStatsByStatus()
        {
            var result = await _adminServices.GetTripStatsByStatus();
            return StatusCode(result.StatusCode, result);
        }
       

    }
}
