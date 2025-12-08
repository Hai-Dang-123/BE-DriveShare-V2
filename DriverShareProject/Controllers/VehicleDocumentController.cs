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

        // =========================================================================
        // 1. [USER/OWNER] Upload giấy tờ xe (Cà vẹt, Bảo hiểm...)
        // =========================================================================
        /// <summary>
        /// Owner upload giấy tờ cho xe. Trạng thái mặc định là PENDING_REVIEW.
        /// </summary>
        /// <param name="vehicleId">ID của xe</param>
        /// <param name="request">Form chứa ảnh (Front/Back) và ngày hết hạn</param>
        [HttpPost("add/{vehicleId:guid}")]
        [Authorize] // Bắt buộc đăng nhập
        [Consumes("multipart/form-data")] // Quan trọng để nhận file upload
        public async Task<IActionResult> AddDocument(Guid vehicleId, [FromForm] AddVehicleDocumentDTO request)
        {
            // Validate cơ bản
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var response = await _service.AddDocumentAsync(vehicleId, request);
            return StatusCode(response.StatusCode, response);
        }

        // =========================================================================
        // 2. [STAFF/ADMIN] Duyệt giấy tờ xe (Manual Review)
        // =========================================================================
        /// <summary>
        /// Staff duyệt hoặc từ chối giấy tờ xe.
        /// </summary>
        /// <param name="request">DTO chứa ID giấy tờ và kết quả duyệt</param>
        [HttpPost("review")]
        //[Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> ReviewDocument([FromBody] ReviewVehicleDocumentDTO request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var response = await _service.ReviewVehicleDocumentAsync(
                request.DocumentId,
                request.IsApproved,
                request.RejectReason
            );

            return StatusCode(response.StatusCode, response);
        }


        /// <summary>
        /// 1. Lấy danh sách tóm tắt các giấy tờ xe đang chờ duyệt (Phân trang)
        /// </summary>
        [HttpGet("pending-reviews")]
        //[Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> GetPendingReviewsList(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,      // Tìm kiếm: Biển số, Tên chủ, Loại giấy
            [FromQuery] string? sortField = null,   // Sắp xếp: "date", "plate", "owner", "type"
            [FromQuery] string? sortOrder = "DESC") // Thứ tự: "ASC" hoặc "DESC"
        {
            var response = await _service.GetPendingVehicleDocumentsListAsync(
                pageNumber,
                pageSize,
                search,
                sortField,
                sortOrder
            );
            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// 2. Lấy chi tiết một giấy tờ xe để Staff kiểm tra (Ảnh, Thông tin xe...)
        /// </summary>
        [HttpGet("pending-reviews/{id:guid}")]
        //[Authorize(Roles = "Admin,Staff")]
        public async Task<IActionResult> GetPendingReviewDetail(Guid id)
        {
            var response = await _service.GetVehicleDocumentDetailAsync(id);

            if (!response.IsSuccess) return StatusCode(response.StatusCode, response);
            return Ok(response);
        }
    }
}