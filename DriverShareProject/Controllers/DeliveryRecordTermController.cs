using BLL.Services.Impletement;
using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    public class DeliveryRecordTermController : Controller
    {
        private readonly IDeliveryRecordTermService _termService;
        public DeliveryRecordTermController(IDeliveryRecordTermService termService)
        {
            _termService = termService;
        }
        /// <summary>
        /// [Admin] Lấy tất cả điều khoản của một Mẫu (có phân trang).
        /// </summary>
        [HttpGet("{templateId}/terms")]
        public async Task<IActionResult> GetAllTermsForTemplate(Guid templateId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var response = await _termService.GetAllByTemplateIdAsync(templateId, pageNumber, pageSize);
            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// [Admin] Lấy chi tiết một điều khoản.
        /// </summary>
        [HttpGet("terms/{id}")]
        public async Task<IActionResult> GetTermById(Guid id)
        {
            var response = await _termService.GetByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// [Admin] Tạo điều khoản mới cho Mẫu.
        /// </summary>
        [HttpPost("terms")]
        public async Task<IActionResult> CreateTerm([FromBody] DeliveryRecordTermDTO dto)
        {
            var response = await _termService.CreateAsync(dto);
            return StatusCode(response.StatusCode, response);
        }
    }
}
