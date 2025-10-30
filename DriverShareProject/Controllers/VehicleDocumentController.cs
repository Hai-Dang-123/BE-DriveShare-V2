using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VehicleDocumentController : ControllerBase
    {
        private readonly IVehicleDocumentService _vehicleDocumentService;

        public VehicleDocumentController(IVehicleDocumentService vehicleDocumentService)
        {
            _vehicleDocumentService = vehicleDocumentService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var response = await _vehicleDocumentService.GetAllAsync();
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var response = await _vehicleDocumentService.GetByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] VehicleDocumentDTO dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ResponseDTO("Invalid data", 400, false));
            }

            var response = await _vehicleDocumentService.CreateAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] VehicleDocumentDTO dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ResponseDTO("Invalid data", 400, false));
            }

            var response = await _vehicleDocumentService.UpdateAsync(id, dto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var response = await _vehicleDocumentService.DeleteAsync(id);
            return StatusCode(response.StatusCode, response);
        }
    }
}