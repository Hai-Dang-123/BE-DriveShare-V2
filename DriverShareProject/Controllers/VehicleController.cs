using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VehicleController : ControllerBase
    {
        private readonly IVehicleService _vehicleService;

        public VehicleController(IVehicleService vehicleService)
        {
            _vehicleService = vehicleService;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] VehicleCreateDTO dto)
        {
            var result = await _vehicleService.CreateVehicleAsync(dto);
            return Ok(result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] VehicleUpdateDTO dto)
        {
            dto.VehicleId = id;
            var result = await _vehicleService.UpdateVehicleAsync(dto);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDelete(Guid id)
        {
            var result = await _vehicleService.SoftDeleteVehicleAsync(id);
            return Ok(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _vehicleService.GetAllVehiclesAsync();
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var result = await _vehicleService.GetVehicleByIdAsync(id);
            return Ok(result);
        }
    }
}
