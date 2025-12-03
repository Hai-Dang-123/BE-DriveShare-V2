using BLL.Services.Interface;
using Common.DTOs;
using Common.Enums.Type; // Nhớ using Enum
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserDocumentController : ControllerBase
    {
        private readonly IUserDocumentService _service;

        public UserDocumentController(IUserDocumentService service)
        {
            _service = service;
        }

        // ============================================================
        // 1. CHECK STATUS & GET INFO
        // ============================================================

        [HttpGet("check-verified-status")]
        // [Authorize] 
        public async Task<IActionResult> CheckVerifiedStatus()
        {
            // Hàm này check logic: Driver cần cả 2, User thường chỉ cần CCCD
            var response = await _service.CheckCCCDVerifiedAsync();
            return StatusCode(response.StatusCode, response);
        }

        [HttpGet("my-documents")]
        // [Authorize] 
        public async Task<IActionResult> GetMyDocuments()
        {
            // API mới để lấy chi tiết CCCD + Bằng lái (nếu là tài xế)
            var response = await _service.GetMyVerifiedDocumentsAsync();
            return StatusCode(response.StatusCode, response);
        }

        // ============================================================
        // 2. UPLOAD & VERIFY EKYC
        // ============================================================

        [HttpPost("verify-cccd")]
        public async Task<IActionResult> VerifyCCCD([FromForm] UploadIdentityRequestDTO request)
        {
            // Gọi Service với Type = CCCD
            var result = await _service.CreateAndVerifyDocumentAsync(
                request.Front,
                request.Back,
                request.Selfie,
                DocumentType.CCCD
            );

            return StatusCode(result.StatusCode, result);
        }

        [HttpPost("verify-license")]
        public async Task<IActionResult> VerifyLicense([FromForm] UploadIdentityRequestDTO request)
        {
            // Gọi Service với Type = DRIVER_LINCENSE (Bằng lái xe)
            // Lưu ý: Back và Selfie có thể null ở đây
            var result = await _service.CreateAndVerifyDocumentAsync(
                request.Front,
                request.Back,   // Có thể null
                request.Selfie, // Có thể null
                DocumentType.DRIVER_LINCENSE
            );

            return StatusCode(result.StatusCode, result);
        }

        // ============================================================
        // 3. ADMIN / MANAGEMENT APIs
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var res = await _service.GetAllAsync();
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var res = await _service.GetByIdAsync(id);
            return StatusCode(res.StatusCode, res);
        }

        [HttpGet("user/{userId:guid}")]
        public async Task<IActionResult> GetByUserId(Guid userId)
        {
            var response = await _service.GetByUserIdAsync(userId);
            return StatusCode(response.StatusCode, response);
        }
    }

    // ============================================================
    // DTO Helper (Cập nhật Nullable để dùng chung)
    // ============================================================
    public class UploadIdentityRequestDTO
    {
        public IFormFile Front { get; set; } = null!; // Mặt trước bắt buộc
        public IFormFile? Back { get; set; }          // Mặt sau (Optional cho Bằng lái)
        public IFormFile? Selfie { get; set; }        // Selfie (Optional cho Bằng lái)
    }
}