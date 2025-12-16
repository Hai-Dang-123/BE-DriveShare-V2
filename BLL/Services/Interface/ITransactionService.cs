using Common.DTOs;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface ITransactionService
    {
        // 1. User gọi nạp tiền
        Task<ResponseDTO> CreateTopupAsync(InternalTransactionRequestDTO dto);

        // 2. User gọi rút tiền
        Task<ResponseDTO> RequestWithdrawalAsync(WithdrawalRequestDTO dto);

        // 3. Webhook gọi xác nhận nạp tiền (Hàm mới thêm)
        Task<ResponseDTO> ConfirmTopupTransactionAsync(string tokenCode, decimal transferAmount, string bankReferenceCode);

        /// <summary>
        /// (Hệ thống/Service) Tạo một giao dịch THANH TOÁN (TRỪ TIỀN) liên quan đến chuyến đi.
        /// ⚠️ SỬA ĐỔI: Trả về ResponseDTO
        /// </summary>
        Task<ResponseDTO> CreatePaymentAsync(InternalTransactionRequestDTO dto);

        /// <summary>
        /// (Hệ thống/Service) Tạo một giao dịch NHẬN TIỀN (CỘNG TIỀN) liên quan đến chuyến đi.
        /// ⚠️ SỬA ĐỔI: Trả về ResponseDTO
        /// </summary>
        Task<ResponseDTO> CreatePayoutAsync(InternalTransactionRequestDTO dto);


        Task<ResponseDTO> GetAllAsync(int pageNumber, int pageSize);
        Task<ResponseDTO> GetByIdAsync(Guid transactionId);
        Task<bool> IsUserRestrictedDueToDebtAsync(Guid userId);
    }
}