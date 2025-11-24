using BLL.Services.Impletement;
using BLL.Services.Interface;
using Microsoft.AspNetCore.Mvc;

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

        // GET ALL
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var res = await _service.GetAllAsync();
            return StatusCode(res.StatusCode, res);
        }

        // GET BY ID
        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var res = await _service.GetByIdAsync(id);
            return StatusCode(res.StatusCode, res);
        }

        // GET BY USER ID (FE dùng)
        [HttpGet("user/{userId:guid}")]
        public async Task<IActionResult> GetByUserId(Guid userId)
        {
            var response = await _service.GetByUserIdAsync(userId);
            return StatusCode(response.StatusCode, response);
        }

    }
}
