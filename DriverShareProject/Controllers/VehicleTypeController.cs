using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]

    public class VehicleTypeController : ControllerBase
    {
        private readonly IVehicleTypeService _vehicleTypeService;

        public VehicleTypeController(IVehicleTypeService vehicleTypeService)
        {
            _vehicleTypeService = vehicleTypeService;
        }

        // ✅ Create
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromForm] VehicleTypeCreateDTO dto)
        {
            var response = await _vehicleTypeService.CreateVehicleTypeAsync(dto);
            return Ok(response);
        }

        // ✅ Update
        [HttpPut("update")]
        public async Task<IActionResult> Update([FromForm] VehicleTypeUpdateDTO dto)
        {
            var response = await _vehicleTypeService.UpdateVehicleTypeAsync(dto);
            return Ok(response);
        }

        // ✅ Soft Delete
        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDelete(Guid id)
        {
            var response = await _vehicleTypeService.SoftDeleteVehicleTypeAsync(id);
            return Ok(response);
        }

        // ✅ Get All
        [HttpGet("get-all")]
        public async Task<IActionResult> GetAll()
        {
            var response = await _vehicleTypeService.GetAllVehicleTypesAsync();
            return Ok(response);
        }

        // ✅ Get By Id
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var response = await _vehicleTypeService.GetVehicleTypeByIdAsync(id);
            return Ok(response);
        }
    }
}
