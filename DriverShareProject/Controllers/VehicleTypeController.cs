using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace DriverShareProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VehicleTypeController : ControllerBase
    {
        private readonly IVehicleTypeService _vehicleTypeService;

        public VehicleTypeController(IVehicleTypeService vehicleTypeService)
        {
            _vehicleTypeService = vehicleTypeService;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] VehicleTypeCreateDTO dto)
        {
            var result = await _vehicleTypeService.CreateAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] VehicleTypeUpdateDTO dto)
        {
            dto.VehicleTypeId = id;
            var result = await _vehicleTypeService.UpdateAsync(dto);
            return StatusCode(result.StatusCode, result);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> SoftDelete(Guid id)
        {
            var result = await _vehicleTypeService.SoftDeleteAsync(id);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("get-all-vehicle-type")]
        public async Task<IActionResult> GetAll()
        {
            var result = await _vehicleTypeService.GetAllAsync();
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _vehicleTypeService.GetByIdAsync(id);
            return StatusCode(result.StatusCode, result);
        }
    }
}
