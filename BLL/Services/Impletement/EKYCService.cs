using BLL.Services.Interface;
using Common.Constants;
using Common.DTOs;
using Common.Enums.Type;
using Common.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class EKYCService : IEKYCService
    {
        private readonly HttpClient _httpClient;
        private readonly IVNPTTokenService _vnptTokenService;
        private readonly ILogger<EKYCService> _logger;
        private readonly VNPTAuthSettings _settings;

        public EKYCService(
            HttpClient httpClient,
            IVNPTTokenService vnptTokenService,
            ILogger<EKYCService> logger,
            IOptions<VNPTAuthSettings> settings)
        {
            _httpClient = httpClient;
            _vnptTokenService = vnptTokenService;
            _logger = logger;
            _settings = settings.Value;

            if (_httpClient.BaseAddress == null)
            {
                _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
            }
        }

        // ================== MAIN WORKFLOW ==================
        public async Task<EkycResultDTO> VerifyIdentityAsync(IFormFile frontImage, IFormFile? backImage, IFormFile? selfieImage, DocumentType docType)
        {
            var result = new EkycResultDTO { IsSuccess = false };
            string clientSession = GenerateClientSession();

            try
            {
                // 1. Upload ảnh Front (Bắt buộc)
                var frontTask = UploadFileAsync(frontImage, "Front ID");
                Task<string?>? backTask = backImage != null ? UploadFileAsync(backImage, "Back ID") : null;
                Task<string?>? selfieTask = selfieImage != null ? UploadFileAsync(selfieImage, "Selfie") : null;

                await Task.WhenAll(frontTask,
                                   backTask ?? Task.FromResult<string?>(null),
                                   selfieTask ?? Task.FromResult<string?>(null));

                result.FrontHash = await frontTask;
                result.BackHash = backTask != null ? await backTask : null;
                result.FaceHash = selfieTask != null ? await selfieTask : null;

                if (string.IsNullOrEmpty(result.FrontHash))
                {
                    result.ErrorMessage = "Lỗi upload ảnh mặt trước.";
                    return result;
                }

                // 2. Phân nhánh xử lý theo loại giấy tờ
                VnptApiResponse<VnptOcrData>? ocrRes = null;

                if (docType == DocumentType.DRIVER_LINCENSE)
                {
                    // --- FLOW GPLX: Chỉ gọi OCR Front ---
                    var ocrTuple = await OcrFrontOnlyAsync(result.FrontHash, clientSession);
                    ocrRes = ocrTuple.Response;
                    result.OcrRawJson = ocrTuple.RawJson;

                    // GPLX chỉ cần check real card mặt trước (nếu API Front hỗ trợ trả về checking_result_front thì ta dùng nó, 
                    // hoặc gọi thêm CardLiveness nếu cần. Ở đây API Front đã có checking_result_front chứa edited/recaptured)
                    if (ocrRes?.Object?.CheckingResultFront != null)
                    {
                        var check = ocrRes.Object.CheckingResultFront;
                        // Nếu '0' là tốt, '1' là lỗi
                        bool isFake = check.RecapturedResult == "1" || check.EditedResult == "1" || check.CheckPhotocopiedResult == "1";
                        result.IsRealCard = !isFake;
                    }
                    else
                    {
                        // Fallback nếu không có thông tin check giả mạo trong response
                        result.IsRealCard = true;
                    }
                }
                else
                {
                    // --- FLOW CCCD: Gọi OCR Full (Front + Back) & Liveness ---
                    if (string.IsNullOrEmpty(result.BackHash))
                    {
                        result.ErrorMessage = "CCCD yêu cầu ảnh mặt sau.";
                        return result;
                    }

                    var ocrTuple = await OcrFullAsync(result.FrontHash, result.BackHash, clientSession);
                    ocrRes = ocrTuple.Response;
                    result.OcrRawJson = ocrTuple.RawJson;

                    var livenessRes = await CheckCardLivenessAsync(result.FrontHash, clientSession);
                    if (livenessRes?.Object != null)
                    {
                        result.IsRealCard = livenessRes.Object.Liveness == "success";
                    }
                }

                // 3. So khớp khuôn mặt (Nếu có selfie)
                if (!string.IsNullOrEmpty(result.FaceHash))
                {
                    var compareRes = await CompareFaceAsync(result.FrontHash, result.FaceHash, clientSession);
                    if (compareRes?.Object != null)
                    {
                        result.FaceMatchScore = compareRes.Object.Prob * 100;
                    }
                }
                else
                {
                    // Nếu là GPLX mà không có selfie thì bỏ qua bước này (hoặc set 100 nếu không bắt buộc)
                    // Ở đây để null
                }

                // 4. Validate kết quả OCR
                if (ocrRes == null || ocrRes.Object == null)
                {
                    result.ErrorMessage = $"Lỗi OCR: {ocrRes?.Message ?? "Null Response"}";
                    return result;
                }

                // VNPT success code check
                bool isApiSuccess = ocrRes.StatusCode == 200 || ocrRes.Message == "IDG-00000000";
                if (!isApiSuccess)
                {
                    result.ErrorMessage = $"VNPT trả về lỗi: {ocrRes.Message}";
                    return result;
                }

                result.OcrData = ocrRes.Object;
                result.IsSuccess = true;
                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Exception: {ex.Message}";
                _logger.LogError(ex, result.ErrorMessage);
                return result;
            }
        }

        // ================== API CALLS ==================

        public async Task<(VnptApiResponse<VnptOcrData>? Response, string RawJson)> OcrFullAsync(string frontHash, string backHash, string clientSession)
        {
            var body = new
            {
                img_front = frontHash,
                img_back = backHash,
                client_session = clientSession,
                type = -1,
                crop_param = "0.14,0.3",
                validate_postcode = true,
                token = GenerateToken()
            };
            return await CallVnptApiWithRawAsync<VnptApiResponse<VnptOcrData>>(EKYCConstant.OcrFull, body);
        }

        // MỚI: Hàm gọi OCR Front Only cho GPLX
        public async Task<(VnptApiResponse<VnptOcrData>? Response, string RawJson)> OcrFrontOnlyAsync(string frontHash, string clientSession)
        {
            var body = new
            {
                img_front = frontHash,
                client_session = clientSession,
                type = 6, // 6 là GPLX theo tài liệu VNPT cũ, hoặc để body mẫu bạn gửi
                validate_postcode = true,
                crop_param = "0.14,0.3",
                token = GenerateToken()
            };
            // Gọi vào endpoint Front
            return await CallVnptApiWithRawAsync<VnptApiResponse<VnptOcrData>>(EKYCConstant.OcrFront, body);
        }

        public async Task<VnptApiResponse<VnptCardLivenessData>> CheckCardLivenessAsync(string hash, string clientSession)
        {
            var body = new { img = hash, client_session = clientSession };
            return await CallVnptApiAsync<VnptApiResponse<VnptCardLivenessData>>(EKYCConstant.CardLiveness, body);
        }

        public async Task<VnptApiResponse<VnptFaceCompareData>> CompareFaceAsync(string frontHash, string faceHash, string clientSession)
        {
            var body = new
            {
                img_front = frontHash,
                img_face = faceHash,
                client_session = clientSession,
                token = GenerateToken()
            };
            return await CallVnptApiAsync<VnptApiResponse<VnptFaceCompareData>>(EKYCConstant.FaceCompare, body);
        }

        // ... Các hàm Upload, Config, Helper giữ nguyên như cũ ...
        public async Task<string?> UploadFileAsync(IFormFile file, string title = "image")
        {
            // (Copy lại logic UploadFileAsync từ câu trả lời trước, giữ nguyên không đổi)
            // ... Code Upload ...
            try
            {
                var accessToken = await _vnptTokenService.GetAccessTokenAsync();
                var (tokenKey, tokenId) = await _vnptTokenService.GetServiceTokensAsync("eKYC");

                using var request = new HttpRequestMessage(HttpMethod.Post, EKYCConstant.FileUpload);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Headers.TryAddWithoutValidation("Token-id", tokenId);
                request.Headers.TryAddWithoutValidation("Token-key", tokenKey);
                request.Headers.TryAddWithoutValidation("mac-address", Guid.NewGuid().ToString());

                using var form = new MultipartFormDataContent();
                if (file.Length <= 0) return null;

                using var fileStream = file.OpenReadStream();
                if (fileStream.CanSeek) fileStream.Position = 0;
                var fileContent = new StreamContent(fileStream);
                var mimeType = !string.IsNullOrEmpty(file.ContentType) ? file.ContentType : "image/jpeg";
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
                string fileName = file.FileName ?? $"{Guid.NewGuid()}.jpg";

                form.Add(fileContent, "file", fileName);
                form.Add(new StringContent(title), "title");
                form.Add(new StringContent("Uploaded via API"), "description");
                request.Content = form;

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode) return null;

                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("message", out var msgProp) && msgProp.GetString() == "IDG-00000000")
                {
                    return doc.RootElement.GetProperty("object").GetProperty("hash").GetString();
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upload Failed");
                return null;
            }
        }

        public async Task<ResponseDTO> GetVnptSdkConfigAsync()
        {
            // Giữ nguyên
            var accessToken = await _vnptTokenService.GetAccessTokenAsync();
            var (tokenKey, tokenId) = await _vnptTokenService.GetServiceTokensAsync("eKYC");
            return new ResponseDTO("Success", 200, true, new { AccessToken = accessToken, TokenId = tokenId, TokenKey = tokenKey });
        }

        // Hàm gọi API trả về Tuple (Data, RawJson)
        private async Task<(T? Data, string RawJson)> CallVnptApiWithRawAsync<T>(string endpoint, object body)
        {
            try
            {
                var accessToken = await _vnptTokenService.GetAccessTokenAsync();
                var (tokenKey, tokenId) = await _vnptTokenService.GetServiceTokensAsync("eKYC");

                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Headers.TryAddWithoutValidation("Token-id", tokenId);
                request.Headers.TryAddWithoutValidation("Token-key", tokenKey);
                request.Headers.TryAddWithoutValidation("mac-address", Guid.NewGuid().ToString());

                var serializeOptions = new JsonSerializerOptions { PropertyNamingPolicy = null };
                string jsonBody = JsonSerializer.Serialize(body, serializeOptions);
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"API {endpoint} Failed: {content}");
                    return (default, content);
                }

                var deserializeOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    NumberHandling = JsonNumberHandling.AllowReadingFromString
                };

                return (JsonSerializer.Deserialize<T>(content, deserializeOptions), content);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"API Error: {endpoint}");
                return (default, string.Empty);
            }
        }

        // Wrapper cũ để tương thích code cũ nếu cần (nhưng nên chuyển sang dùng hàm trên)
        private async Task<T?> CallVnptApiAsync<T>(string endpoint, object body)
        {
            var result = await CallVnptApiWithRawAsync<T>(endpoint, body);
            return result.Data;
        }

        private string GenerateToken() => "ocrreq_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmssfff");
        private string GenerateClientSession() => $"ANDROID_simulate_{Guid.NewGuid().ToString("N").Substring(0, 10)}";
    }
}