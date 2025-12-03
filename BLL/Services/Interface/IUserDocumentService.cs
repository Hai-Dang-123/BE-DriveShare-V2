using Common.DTOs;
using Common.Enums.Type; // Nhớ using Enum DocumentType
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IUserDocumentService
    {
        /// <summary>
        /// Kiểm tra trạng thái giấy tờ. 
        /// Nếu là Driver: Phải có cả CCCD và GPLX Active.
        /// Nếu là User khác: Chỉ cần CCCD Active.
        /// </summary>
        Task<ResponseDTO> CheckCCCDVerifiedAsync();

        Task<ResponseDTO> GetMyVerifiedDocumentsAsync();
        Task<ResponseDTO> GetAllAsync();
        Task<ResponseDTO> GetByIdAsync(Guid id);
        Task<ResponseDTO> GetByUserIdAsync(Guid userId);

        /// <summary>
        /// Tạo và xác thực giấy tờ.
        /// - CCCD: Cần đủ 3 ảnh (front, back, selfie).
        /// - GPLX: Chỉ cần front (back, selfie có thể null).
        /// </summary>
        Task<ResponseDTO> CreateAndVerifyDocumentAsync(IFormFile frontImg, IFormFile? backImg, IFormFile? selfieImg, DocumentType docType);
    }
}