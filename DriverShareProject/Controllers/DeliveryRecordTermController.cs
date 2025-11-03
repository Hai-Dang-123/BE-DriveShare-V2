using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DeliveryRecordTermController : ControllerBase
    {
        private readonly IDeliveryRecordTermService _deliveryRecordTermService;

        public DeliveryRecordTermController(IDeliveryRecordTermService deliveryRecordTermService)
        {
            _deliveryRecordTermService = deliveryRecordTermService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var response = await _deliveryRecordTermService.GetAllAsync();
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var response = await _deliveryRecordTermService.GetByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] DeliveryRecordTermDTO dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ResponseDTO("Invalid data", 400, false));
            }

            var response = await _deliveryRecordTermService.CreateAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] DeliveryRecordTermDTO dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ResponseDTO("Invalid data", 400, false));
            }

            var response = await _deliveryRecordTermService.UpdateAsync(id, dto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var response = await _deliveryRecordTermService.DeleteAsync(id);
            return StatusCode(response.StatusCode, response);
        }
    }
}