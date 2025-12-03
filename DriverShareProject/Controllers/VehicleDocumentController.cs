using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VehicleDocumentController : ControllerBase
    {
        private readonly IVehicleDocumentService _service;

        public VehicleDocumentController(IVehicleDocumentService service)
        {
            _service = service;
        }

        /// <summary>
        /// Thêm mới giấy tờ cho xe (Cà vẹt, Bảo hiểm...)
        /// </summary>
        /// <param name="vehicleId">ID của xe cần thêm giấy tờ</param>
        /// <param name="request">Form chứa ảnh và thông tin</param>
        [HttpPost("add/{vehicleId:guid}")]
        [Authorize] // Bắt buộc đăng nhập
        public async Task<IActionResult> AddDocument(Guid vehicleId, [FromForm] AddVehicleDocumentDTO request)
        {
            var response = await _service.AddDocumentAsync(vehicleId, request);
            return StatusCode(response.StatusCode, response);
        }
    }
}