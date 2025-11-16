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

        /// <summary>
        /// [Admin] Lấy tất cả Mẫu biên bản (có phân trang).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllTemplates([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var response = await _deliveryRecordTemplateService.GetAllAsync(pageNumber, pageSize);
            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// [Admin] Lấy Mẫu biên bản theo ID.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTemplateById(Guid id)
        {
            var response = await _deliveryRecordTemplateService.GetByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }
    }
}