using Common.DTOs;
using Common.Enums.Type;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IWalletService
    {
        /// <summary>
        /// Lấy thông tin ví của người dùng đang đăng nhập.
        /// </summary>
        Task<ResponseDTO> GetMyWalletAsync();

        /// <summary>
        /// Lấy lịch sử giao dịch (phân trang) của người dùng đang đăng nhập.
        /// </summary>
        Task<ResponseDTO> GetMyTransactionHistoryAsync(int pageNumber = 1, int pageSize = 10);

       
    }
}
