using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DeliveryRecordTermController : ControllerBase
    {
        private readonly IDeliveryRecordTermService _termService;

        public DeliveryRecordTermController(IDeliveryRecordTermService termService)
        {
            _termService = termService;
        }

        // -------------------------------------------------------------
        // 1. GET ALL TERMS BY TEMPLATE ID (WITH PAGINATION)
        // -------------------------------------------------------------
        [HttpGet("{templateId}/terms")]
        public async Task<IActionResult> GetAllTermsForTemplate(
            Guid templateId,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var response = await _termService.GetAllByTemplateIdAsync(templateId, pageNumber, pageSize);
            return StatusCode(response.StatusCode, response);
        }

        // -------------------------------------------------------------
        // 2. GET TERM BY ID
        // -------------------------------------------------------------
        [HttpGet("terms/{id}")]
        public async Task<IActionResult> GetTermById(Guid id)
        {
            var response = await _termService.GetByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        // -------------------------------------------------------------
        // 3. CREATE TERM
        // -------------------------------------------------------------
        [HttpPost("terms")]
        public async Task<IActionResult> CreateTerm([FromBody] DeliveryRecordTermDTO dto)
        {
            var response = await _termService.CreateAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        // -------------------------------------------------------------
        // 4. UPDATE TERM  ← (BẠN CHƯA CÓ – TÔI THÊM VÀO)
        // -------------------------------------------------------------
        [HttpPut("terms/{id}")]
        public async Task<IActionResult> UpdateTerm(Guid id, [FromBody] DeliveryRecordTermDTO dto)
        {
            var response = await _termService.UpdateAsync(id, dto);
            return StatusCode(response.StatusCode, response);
        }

        // -------------------------------------------------------------
        // 5. DELETE TERM  ← (BẠN CHƯA CÓ – TÔI THÊM VÀO)
        // -------------------------------------------------------------
        [HttpDelete("terms/{id}")]
        public async Task<IActionResult> DeleteTerm(Guid id)
        {
            var response = await _termService.DeleteAsync(id);
            return StatusCode(response.StatusCode, response);
        }
    }
}
