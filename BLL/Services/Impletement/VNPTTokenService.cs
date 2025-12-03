using BLL.Services.Interface;
using Common.Constants;
using Common.Settings;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading; // Thêm thư viện này
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class VNPTTokenService : IVNPTTokenService
    {
        private readonly HttpClient _httpClient;
        private readonly VNPTAuthSettings _settings;

        // Cache biến Token
        private string _accessToken;
        private DateTime _accessTokenExpiry;
        private string _publicKey;
        private string _uuidProjectServicePlan;

        // FIX: Thêm Semaphore để khóa luồng (Thread-Safe)
        private readonly SemaphoreSlim _tokenLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _serviceLock = new SemaphoreSlim(1, 1);

        public VNPTTokenService(HttpClient httpClient, IOptions<VNPTAuthSettings> settings)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        }

        public async Task<string> GetAccessTokenAsync()
        {
            // Kiểm tra nhanh trước khi wait
            if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _accessTokenExpiry)
            {
                return _accessToken;
            }

            // Chờ đến lượt (Chỉ 1 luồng được chạy đoạn dưới này tại 1 thời điểm)
            await _tokenLock.WaitAsync();

            try
            {
                // Kiểm tra lại lần nữa (Double-check locking)
                if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _accessTokenExpiry)
                {
                    return _accessToken;
                }

                var form = new Dictionary<string, string>
                {
                    ["grant_type"] = _settings.GrantType
                };

                var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl}{VNPTEndpoints.TokenEndpoint}")
                {
                    Content = new FormUrlEncodedContent(form)
                };

                var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.ClientId}:{_settings.ClientSecret}"));
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authHeader);

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);

                _accessToken = doc.RootElement.GetProperty("access_token").GetString();
                var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();

                // Trừ đi 60s để an toàn
                _accessTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60);

                return _accessToken;
            }
            finally
            {
                // Luôn giải phóng khóa
                _tokenLock.Release();
            }
        }

        public async Task<(string TokenKey, string TokenId)> GetServiceTokensAsync(string channelCode)
        {
            if (!string.IsNullOrEmpty(_publicKey) && !string.IsNullOrEmpty(_uuidProjectServicePlan))
            {
                return (_publicKey, _uuidProjectServicePlan);
            }

            await _serviceLock.WaitAsync();

            try
            {
                if (!string.IsNullOrEmpty(_publicKey) && !string.IsNullOrEmpty(_uuidProjectServicePlan))
                {
                    return (_publicKey, _uuidProjectServicePlan);
                }

                var token = await GetAccessTokenAsync();

                var req = new HttpRequestMessage(HttpMethod.Post, VNPTEndpoints.CheckRegisterEndpoint);
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("bearer", token);
                req.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                // Fix: Dùng channelCode động
                req.Content = JsonContent.Create(new { channelCode = channelCode });

                var response = await _httpClient.SendAsync(req);
                response.EnsureSuccessStatusCode();

                var body = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("object", out var objElement))
                {
                    _publicKey = objElement.GetProperty("publicKey").GetString();
                    _uuidProjectServicePlan = objElement.GetProperty("uuidProjectServicePlan").GetString();
                }
                else
                {
                    // Fallback nếu JSON khác cấu trúc (tùy môi trường VNPT)
                    throw new Exception("VNPT CheckRegister: Response không chứa object.");
                }

                return (_publicKey, _uuidProjectServicePlan);
            }
            finally
            {
                _serviceLock.Release();
            }
        }
    }
}