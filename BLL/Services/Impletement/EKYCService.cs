using BLL.Services.Interface;
using Common.Constants;
using Common.DTOs;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class EKYCService : IEKYCService
    {
        private readonly HttpClient _httpClient;
        private readonly IVNPTTokenService _vnptTokenService;

        public EKYCService(HttpClient httpClient, IVNPTTokenService vnptTokenService)
        {
            _httpClient = httpClient;
            _vnptTokenService = vnptTokenService;
        }

        //public async Task<ResponseDTO> UploadFileAsync(EKYCUploadRequestDTO requestDto)
        //{
        //    try
        //    {
        //        var accessToken = await _vnptTokenService.GetAccessTokenAsync();
        //        var (tokenKey, tokenId) = await _vnptTokenService.GetServiceTokensAsync("eKYC");

        //        using var request = new HttpRequestMessage(HttpMethod.Post, EKYCConstant.FileUploadEndpoint);
        //        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        //        request.Headers.Add("Token-id", tokenId);
        //        request.Headers.Add("Token-key", tokenKey);
        //        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        //        using var form = new MultipartFormDataContent();
        //        using var fileStream = requestDto.File.OpenReadStream();
        //        var fileContent = new StreamContent(fileStream);
        //        fileContent.Headers.ContentType = new MediaTypeHeaderValue(requestDto.File.ContentType);
        //        form.Add(fileContent, "file", requestDto.File.FileName);
        //        form.Add(new StringContent(requestDto.Title), "title");

        //        if (!string.IsNullOrEmpty(requestDto.Description))
        //            form.Add(new StringContent(requestDto.Description), "description");

        //        request.Content = form;

        //        var response = await _httpClient.SendAsync(request);
        //        var content = await response.Content.ReadAsStringAsync();

        //        using var doc = JsonDocument.Parse(content);
        //        var message = doc.RootElement.GetProperty("message").GetString();

        //        if (message == "IDG-00000000")
        //        {
        //            var hash = doc.RootElement.GetProperty("object").GetProperty("hash").GetString();
        //            return new ResponseDTO("Upload success", 200, true, hash);
        //        }

        //        return new ResponseDTO($"Upload file failed: {message}", (int)response.StatusCode, false, content);
        //    }
        //    catch (Exception ex)
        //    {
        //        return new ResponseDTO($"Upload file exception: {ex.Message}", 500, false);
        //    }
        //}
        // ================================
        // EKYCService.cs
        // ================================

        public async Task<string?> UploadFileAsync(EKYCUploadRequestDTO requestDto)
        {
            try
            {
                var accessToken = await _vnptTokenService.GetAccessTokenAsync();
                var (tokenKey, tokenId) = await _vnptTokenService.GetServiceTokensAsync("eKYC");

                using var request = new HttpRequestMessage(HttpMethod.Post, EKYCConstant.FileUploadEndpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Headers.Add("Token-id", tokenId);
                request.Headers.Add("Token-key", tokenKey);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using var form = new MultipartFormDataContent();
                using var fileStream = requestDto.File.OpenReadStream();
                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(requestDto.File.ContentType);
                form.Add(fileContent, "file", requestDto.File.FileName);
                form.Add(new StringContent(requestDto.Title), "title");

                if (!string.IsNullOrEmpty(requestDto.Description))
                    form.Add(new StringContent(requestDto.Description), "description");

                request.Content = form;

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(content);
                var message = doc.RootElement.GetProperty("message").GetString();

                if (message == "IDG-00000000")
                {
                    var hash = doc.RootElement.GetProperty("object").GetProperty("hash").GetString();
                    return hash; // ✅ Trả về hash (fileId)
                }

                Console.WriteLine($"⚠️ Upload failed: {message}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ UploadFileAsync error: {ex.Message}");
                return null;
            }
        }

        public async Task<ResponseDTO> OcrAsync(EKYCOcrRequestDTO dto)
        {
            try
            {
                var accessToken = await _vnptTokenService.GetAccessTokenAsync();
                var (tokenKey, tokenId) = await _vnptTokenService.GetServiceTokensAsync("eKYC");

                using var request = new HttpRequestMessage(HttpMethod.Post, EKYCConstant.OcrEndpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                request.Headers.Add("Token-id", tokenId);
                request.Headers.Add("Token-key", tokenKey);
                request.Headers.Add("mac-address", dto.MacAddress ?? "TEST1");
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var body = new
                {
                    img_front = dto.ImgFront,
                    img_back = dto.ImgBack,
                    client_session = dto.ClientSession,
                    type = dto.Type ?? -1,
                    validate_postcode = dto.ValidatePostcode ?? false,
                    crop_param = dto.CropParam,
                    token = dto.Token,
                    challenge_code = dto.ChallengeCode ?? "1111"
                };

                request.Content = JsonContent.Create(body);

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(content);
                var message = doc.RootElement.GetProperty("message").GetString();

                if (response.IsSuccessStatusCode && message == "IDG-00000000")
                    return new ResponseDTO("OCR success", 200, true, content);

                return new ResponseDTO("OCR failed", (int)response.StatusCode, false, content);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"OCR exception: {ex.Message}", 500, false);
            }
        }

        //TestHoang
        // ✅ Hàm mới xử lý OCR cho Vehicle
        public async Task<string> ReadVehicleDocumentAsync(string frontUrl, string? backUrl)
        {
            var dto = new EKYCOcrRequestDTO
            {
                ImgFront = frontUrl,
                ImgBack = backUrl,
                Type = 14, // ✅ mã OCR cho “vehicle registration” (VNPT EKYC)
                ClientSession = Guid.NewGuid().ToString()
            };

            var response = await OcrAsync(dto);

            if (!response.IsSuccess)
                throw new Exception($"OCR failed: {response.Message}");

            // Trả về JSON OCR result (string)
            return response.Result?.ToString() ?? "{}";
        }

        public class EKYCUploadRequestDTO
        {
            public IFormFile File { get; set; }
            public string Title { get; set; }
            public string? Description { get; set; }
        }

        public class EKYCOcrRequestDTO
        {
            public string ImgFront { get; set; }
            public string ImgBack { get; set; }
            public string ClientSession { get; set; }
            public int? Type { get; set; } = -1;
            public bool? ValidatePostcode { get; set; } = false;
            public string CropParam { get; set; }
            public string Token { get; set; }
            public string? ChallengeCode { get; set; } = "1111";
            public string? MacAddress { get; set; } = "TEST1";
        }
    }
}
