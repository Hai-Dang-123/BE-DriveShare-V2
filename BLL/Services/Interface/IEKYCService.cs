using Common.DTOs;
using Common.Enums.Type;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IEKYCService
    {
        // Hàm này xử lý luồng (flow) khác nhau dựa trên loại giấy tờ
        Task<EkycResultDTO> VerifyIdentityAsync(IFormFile frontImage, IFormFile? backImage, IFormFile? selfieImage, DocumentType docType);

        // Các hàm con
        Task<string?> UploadFileAsync(IFormFile file, string title = "image");
        Task<(VnptApiResponse<VnptOcrData>? Response, string RawJson)> OcrFullAsync(string frontHash, string backHash, string clientSession);
        Task<(VnptApiResponse<VnptOcrData>? Response, string RawJson)> OcrFrontOnlyAsync(string frontHash, string clientSession);

        Task<VnptApiResponse<VnptCardLivenessData>> CheckCardLivenessAsync(string hash, string clientSession);
        Task<VnptApiResponse<VnptFaceCompareData>> CompareFaceAsync(string frontHash, string faceHash, string clientSession);
        Task<ResponseDTO> GetVnptSdkConfigAsync();
    }
}