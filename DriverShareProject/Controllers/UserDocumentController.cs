using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserDocumentController : ControllerBase
    {
        private readonly IUserDocumentService _userDocumentService;

        public UserDocumentController(IUserDocumentService userDocumentService)
        {
            _userDocumentService = userDocumentService;
        }

        [HttpGet("check-cccd-status")]
        //[Authorize] // Bắt buộc có token
        public async Task<IActionResult> CheckCCCDStatus()
        {
            var response = await _userDocumentService.CheckCCCDVerifiedAsync();
            return StatusCode(response.StatusCode, response);
        }
    }
}