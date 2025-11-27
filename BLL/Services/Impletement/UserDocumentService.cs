using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.Enums.Type;

using DAL.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
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


        public UserDocumentService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        // ============================================================
        // 1. GET ALL
        // ============================================================
        public async Task<ResponseDTO> GetAllAsync()
        {
            try
            {
                var docs = await _unitOfWork.UserDocumentRepo.GetAll()
                    .AsNoTracking()
                    .ToListAsync();

                return new ResponseDTO("Success", 200, true, docs);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error: {ex.Message}", 500, false);
            }
        }

        // ============================================================
        // 2. GET BY ID
        // ============================================================
        public async Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            try
            {
                var doc = await _unitOfWork.UserDocumentRepo.GetAll()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.UserDocumentId == id);

                if (doc == null)
                    return new ResponseDTO("Document not found", 404, false);

                return new ResponseDTO("Success", 200, true, doc);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error: {ex.Message}", 500, false);
            }
        }

        // ============================================================
        // 3. GET BY USER (API FE đang dùng)
        // ============================================================
        public async Task<ResponseDTO> GetByUserIdAsync(Guid userId)
        {
            try
            {
                var user = await _unitOfWork.BaseUserRepo
                    .GetAll()
                    .Include(u => u.Role)
                    .Include(u => u.UserDocuments)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                if (user == null)
                    return new ResponseDTO("User not found", 404, false);

                // -------------------------------
                // 1. Map User
                // -------------------------------
                var userDto = new UserWithDocumentsDTO
                {
                    UserId = user.UserId,
                    FullName = user.FullName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    AvatarUrl = user.AvatarUrl,
                    RoleName = user.Role?.RoleName ?? "",
                    Status = user.Status.ToString(),
                    CreatedAt = user.CreatedAt
                };

                // -------------------------------
                // 2. Map Documents
                // -------------------------------
                userDto.Documents = user.UserDocuments.Select(d => new UserDocumentDTO
                {
                    UserDocumentId = d.UserDocumentId,
                    UserId = d.UserId,
                    DocumentType = d.DocumentType.ToString(),
                    FrontImageUrl = d.FrontImageUrl,
                    FrontImageHash = d.FrontImageHash,
                    BackImageUrl = d.BackImageUrl,
                    BackImageHash = d.BackImageHash,
                    Status = d.Status.ToString(),
                    RejectionReason = d.RejectionReason,
                    CreatedAt = d.CreatedAt,
                    VerifiedAt = d.VerifiedAt
                }).ToList();

                return new ResponseDTO("Success", 200, true, userDto);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error: {ex.Message}", 500, false);
            }
        }


        // ============================================================
        // ❗ Không implement CREATE / UPDATE / DELETE
        // Vì domain chưa có DocumentType
        // ============================================================
        public Task<ResponseDTO> CreateAsync(UserDocumentDTO dto)
            => Task.FromResult(new ResponseDTO("Not supported", 400, false));

        public Task<ResponseDTO> UpdateAsync(Guid id, UserDocumentDTO dto)
            => Task.FromResult(new ResponseDTO("Not supported", 400, false));

        public Task<ResponseDTO> DeleteAsync(Guid id)
            => Task.FromResult(new ResponseDTO("Not supported", 400, false));
    }
}
