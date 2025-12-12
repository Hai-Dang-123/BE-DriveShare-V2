using BLL.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TransactionController : ControllerBase
    {
        private readonly ITransactionService _transactionService;

        public TransactionController(ITransactionService transactionService)
        {
            _transactionService = transactionService;
        }

        /// <summary>
        /// [User] Lấy lịch sử giao dịch CỦA TÔI (có phân trang).
        /// (Admin sẽ thấy tất cả, User thường chỉ thấy của mình)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllMyTransactions([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var response = await _transactionService.GetAllAsync(pageNumber, pageSize);
            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// [User] Lấy chi tiết một giao dịch.
        /// (Service sẽ kiểm tra quyền sở hữu)
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTransactionById(Guid id)
        {
            var response = await _transactionService.GetByIdAsync(id);
            return StatusCode(response.StatusCode, response);
        }
    }
}
