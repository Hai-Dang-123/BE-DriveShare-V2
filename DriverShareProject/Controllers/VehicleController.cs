using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace DriverShareProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VehicleController : ControllerBase
    {
        private readonly IVehicleService _vehicleService;

        public VehicleController(IVehicleService vehicleService)
        {
            _vehicleService = vehicleService;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateVehicle(
        [FromForm] VehicleCreateDTO dto) 
        {
            var result = await _vehicleService.CreateAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] VehicleUpdateDTO dto)
        {
            dto.VehicleId = id;
            var result = await _vehicleService.UpdateAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> SoftDelete(Guid id)
        {
            var result = await _vehicleService.SoftDeleteAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _vehicleService.GetByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }
        // 1. Get All (Admin/Public)
        [HttpGet]
        public async Task<IActionResult> GetAllVehicles(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = "ASC"
        )
        {
            var response = await _vehicleService.GetAllAsync(pageNumber, pageSize, search, sortBy, sortOrder);
            return StatusCode(response.StatusCode, response);
        }

        // 2. Get My Vehicles (Owner - Tất cả xe)
        [HttpGet("get-my-vehicles")]
        [Authorize]
        public async Task<IActionResult> GetMyVehicles(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,      // Thêm
            [FromQuery] string? sortBy = null,      // Thêm
            [FromQuery] string? sortOrder = "ASC"   // Thêm
        )
        {
            var result = await _vehicleService.GetMyVehiclesAsync(pageNumber, pageSize, search, sortBy, sortOrder);
            return StatusCode(result.StatusCode, result);
        }

        // 3. Get My Active Vehicles (Owner - Chỉ xe Active)
        [HttpGet("get-my-active-vehicles")]
        [Authorize]
        public async Task<IActionResult> GetMyActiveVehicles(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,      // Thêm
            [FromQuery] string? sortBy = null,      // Thêm
            [FromQuery] string? sortOrder = "ASC"   // Thêm
        )
        {
            var result = await _vehicleService.GetMyActiveVehiclesAsync(pageNumber, pageSize, search, sortBy, sortOrder);
            return StatusCode(result.StatusCode, result);
        }
    }
}
