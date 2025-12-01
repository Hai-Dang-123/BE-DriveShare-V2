using BLL.Services.Interface;
using Common.Constants;
using Common.Settings;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class VNPTTokenService : IVNPTTokenService
    {
        private readonly HttpClient _httpClient;
        private readonly VNPTAuthSettings _settings;

        private string _accessToken;
        private DateTime _accessTokenExpiry;

        private string _publicKey;
        private string _uuidProjectServicePlan;

        public VNPTTokenService(HttpClient httpClient, IOptions<VNPTAuthSettings> settings)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        }

        public async Task<string> GetAccessTokenAsync()
        {
            if (string.IsNullOrEmpty(_accessToken) || DateTime.UtcNow >= _accessTokenExpiry)
            {
                var form = new Dictionary<string, string>
                {
                    ["grant_type"] = _settings.GrantType
                };

                var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl}{VNPTEndpoints.TokenEndpoint}")
                {
                    Content = new FormUrlEncodedContent(form)
                };

                // 🟢 Thêm Basic Auth header
                var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.ClientId}:{_settings.ClientSecret}"));
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authHeader);

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(stream);

                _accessToken = doc.RootElement.GetProperty("access_token").GetString();
                var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();
                _accessTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60);
            }

            return _accessToken;
        }



        public async Task<(string TokenKey, string TokenId)> GetServiceTokensAsync(string channelCode)
        {
            if (string.IsNullOrEmpty(_publicKey) || string.IsNullOrEmpty(_uuidProjectServicePlan))
            {
                var token = await GetAccessTokenAsync();

                var req = new HttpRequestMessage(HttpMethod.Post, VNPTEndpoints.CheckRegisterEndpoint);
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("bearer", token);
                req.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                req.Content = JsonContent.Create(new { channelCode });

                var response = await _httpClient.SendAsync(req);
                response.EnsureSuccessStatusCode();

                var body = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Response từ VNPT CheckRegister: " + body);

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                // 🟢 Truy cập vào phần "object"
                if (!root.TryGetProperty("object", out var objElement))
                {
                    throw new Exception($"Response không chứa 'object'. Body: {body}");
                }

                if (!objElement.TryGetProperty("publicKey", out var pkProp) ||
                    !objElement.TryGetProperty("uuidProjectServicePlan", out var uuidProp))
                {
                    throw new Exception($"'object' không chứa key cần thiết. Body: {body}");
                }

                _publicKey = pkProp.GetString();
                _uuidProjectServicePlan = uuidProp.GetString();
            }

            return (_publicKey, _uuidProjectServicePlan);
        }

    }
}
