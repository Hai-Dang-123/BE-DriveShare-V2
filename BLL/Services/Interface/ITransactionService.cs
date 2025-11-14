using Common.DTOs;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface ITransactionService
    {
        /// <summary>
        /// (User) Tạo yêu cầu rút tiền (TRỪ TIỀN) từ ví CỦA MÌNH.
        /// (Hàm này trả về ResponseDTO để Controller có thể hiển thị cho user).
        /// </summary>
        Task<ResponseDTO> RequestWithdrawalAsync(WithdrawalRequestDTO dto);

        /// <summary>
        /// (Hệ thống/Admin) Tạo giao dịch NẠP TIỀN (CỘNG TIỀN) vào ví của một User.
        /// ⚠️ SỬA ĐỔI: Trả về ResponseDTO
        /// </summary>
        Task<ResponseDTO> CreateTopupAsync(InternalTransactionRequestDTO dto);

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
    }
}