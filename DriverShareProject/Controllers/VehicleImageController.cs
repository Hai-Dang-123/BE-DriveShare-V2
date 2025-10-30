using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace DriverShareProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VehicleImageController : ControllerBase
    {
        private readonly IVehicleImageService _vehicleImageService;

        public VehicleImageController(IVehicleImageService vehicleImageService)
        {
            _vehicleImageService = vehicleImageService;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromForm] VehicleImageCreateDTO dto)
        {
            var result = await _vehicleImageService.CreateAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromForm] VehicleImageUpdateDTO dto)
        {
            dto.VehicleImageId = id;
            var result = await _vehicleImageService.UpdateAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> SoftDelete(Guid id)
        {
            var result = await _vehicleImageService.SoftDeleteAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("vehicle/{vehicleId:guid}")]
        public async Task<IActionResult> GetAll(Guid vehicleId)
        {
            var result = await _vehicleImageService.GetAllAsync(vehicleId);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _vehicleImageService.GetByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }
    }
}
