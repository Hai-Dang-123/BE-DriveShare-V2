using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class VehicleImageController : ControllerBase
    {
        private readonly IVehicleImageService _vehicleImageService;

        public VehicleImageController(IVehicleImageService vehicleImageService)
        {
            _vehicleImageService = vehicleImageService;
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create([FromForm] VehicleImageCreateDTO dto)
        {
            var result = await _vehicleImageService.CreateVehicleImageAsync(dto);
            return Ok(result);
        }

        [HttpPut("update")]
        public async Task<IActionResult> Update([FromForm] VehicleImageUpdateDTO dto)
        {
            var result = await _vehicleImageService.UpdateVehicleImageAsync(dto);
            return Ok(result);
        }

        [HttpDelete("soft-delete/{id}")]
        public async Task<IActionResult> SoftDelete(Guid id)
        {
            var result = await _vehicleImageService.SoftDeleteVehicleImageAsync(id);
            return Ok(result);
        }

        [HttpGet("all/{vehicleId}")]
        public async Task<IActionResult> GetAll(Guid vehicleId)
        {
            var result = await _vehicleImageService.GetAllVehicleImagesAsync(vehicleId);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _vehicleImageService.GetVehicleImageByIdAsync(id);
            return Ok(result);
        }
    }
}
