using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.Enums.Type;
using DAL.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class UserDocumentService : IUserDocumentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;

        public UserDocumentService(IUnitOfWork unitOfWork, UserUtility userUtility)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
        }

        public async Task<ResponseDTO> CheckCCCDVerifiedAsync()
        {
            try
            {
                // 1. Lấy UserId từ Token
                var userId = _userUtility.GetUserIdFromToken();

                // 2. Query database kiểm tra tồn tại
                // Điều kiện:
                // - Của User này
                // - Loại giấy tờ là CCCD
                // - Trạng thái là ACTIVE (Đã xác thực)
                bool isVerified = await _unitOfWork.UserDocumentRepo.GetAll()
                    .AnyAsync(x => x.UserId == userId
                                && x.DocumentType == DocumentType.CCCD
                                && x.Status == VerifileStatus.ACTIVE);

                if (isVerified)
                {
                    // Trả về true trong phần Data của ResponseDTO
                    return new ResponseDTO("Người dùng đã xác thực CCCD.", 200, true, true);
                }
                else
                {
                    // Trả về false (nhưng vẫn là status 200 vì request thành công, chỉ là kết quả check là chưa verify)
                    return new ResponseDTO("Người dùng chưa xác thực CCCD hoặc hồ sơ đang chờ duyệt.", 200, true, false);
                }
            }
            catch (Exception ex)
            {
                return new ResponseDTO("Lỗi hệ thống khi kiểm tra CCCD: " + ex.Message, 500, false);
            }
        }

    }
}
