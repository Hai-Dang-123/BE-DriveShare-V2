using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DeliveryRecordTemplateController : ControllerBase
    {
        private readonly IDeliveryRecordTemplateService _deliveryRecordTemplateService;

        public DeliveryRecordTemplateController(IDeliveryRecordTemplateService deliveryRecordTemplateService)
        {
            _deliveryRecordTemplateService = deliveryRecordTemplateService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var response = await _deliveryRecordTemplateService.GetAllAsync();
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var response = await _deliveryRecordTemplateService.GetByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] DeliveryRecordTemplateDTO dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ResponseDTO("Invalid data", 400, false));
            }

            var response = await _deliveryRecordTemplateService.CreateAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] DeliveryRecordTemplateDTO dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ResponseDTO("Invalid data", 400, false));
            }

            var response = await _deliveryRecordTemplateService.UpdateAsync(id, dto);
            return StatusCode(response.StatusCode, response);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var response = await _deliveryRecordTemplateService.DeleteAsync(id);
            return StatusCode(response.StatusCode, response);
        }
    }
}