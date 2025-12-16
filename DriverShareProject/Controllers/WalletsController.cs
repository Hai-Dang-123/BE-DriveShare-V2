using BLL.Services.Implement;
using BLL.Services.Interface;
using Common.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DriverShareProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WalletsController : ControllerBase
    {
        private readonly IWalletService _walletService;
        private readonly ITransactionService _transactionService;

        // 1. Inject WalletService
        public WalletsController(IWalletService walletService, ITransactionService transactionService)
        {
            _walletService = walletService;
            _transactionService = transactionService;
        }

        /// <summary>
        /// Lấy thông tin ví của người dùng đang đăng nhập.
        /// </summary>
        [HttpGet("my-wallet")]
        public async Task<IActionResult> GetMyWallet()
        {
            var response = await _walletService.GetMyWalletAsync();
            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// Lấy lịch sử giao dịch (phân trang) của người dùng đang đăng nhập.
        /// </summary>
        [HttpGet("my-wallet/history")]
        public async Task<IActionResult> GetMyTransactionHistory([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var response = await _walletService.GetMyTransactionHistoryAsync(pageNumber, pageSize);

            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// Tạo một yêu cầu rút tiền (Withdrawal) từ ví của người dùng đang đăng nhập.
        /// </summary>
        [HttpPost("withdraw")]
        public async Task<IActionResult> RequestWithdrawal([FromBody] WithdrawalRequestDTO dto)
        {
            var response = await _transactionService.RequestWithdrawalAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        
        /// <summary>
        /// Nạp tiền (Topup) vào ví của một User.
        /// </summary>
        [HttpPost("topup")]
        public async Task<IActionResult> CreateTopup([FromBody] InternalTransactionRequestDTO dto)
        {
            var response = await _transactionService.CreateTopupAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// (Admin/System) Tạo một giao dịch thanh toán (Payment) cho một User.
        /// (VD: Provider thanh toán, Owner thanh toán tiền thuê tài xế).
        /// </summary>
        [HttpPost("payment")]
        public async Task<IActionResult> CreatePayment([FromBody] InternalTransactionRequestDTO dto)
        {
            var response = await _transactionService.CreatePaymentAsync(dto);
            return StatusCode(response.StatusCode, response);
        }

        /// <summary>
        /// (System) Tạo một giao dịch nhận tiền (Payout) cho một User.
        /// (VD: Trả tiền cho Owner, Trả tiền cho Driver).
        /// </summary>
        [HttpPost("payout")]
        public async Task<IActionResult> CreatePayout([FromBody] InternalTransactionRequestDTO dto)
        {
            var response = await _transactionService.CreatePayoutAsync(dto);
            return StatusCode(response.StatusCode, response);
        }
    }
}
