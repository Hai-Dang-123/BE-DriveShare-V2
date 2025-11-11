using BLL.Services.Interface;
using Common.DTOs;
using Common.Settings;
using Common.ValueObjects;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging; // Thêm Logger
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Web;

namespace BLL.Services.Impletement
{
    // --- DTOs ĐỂ HỨNG API SEARCH V1 (Khớp với Postman) ---
    // (Những DTO này nên được dời ra file DTOs riêng)

    /// <summary>
    /// Class gốc hứng toàn bộ response từ API Search V1
    /// </summary>
    public class VietMapSearchV1Response
    {
        [JsonPropertyName("code")]
        public string Code { get; set; }

        // SỬA LỖI: Thêm lớp "Data"
        [JsonPropertyName("data")]
        public VietMapSearchV1Data Data { get; set; }
    }

    /// <summary>
    /// Lớp bọc "data"
    /// </summary>
    public class VietMapSearchV1Data
    {
        [JsonPropertyName("features")]
        public List<VietMapApiFeature> Features { get; set; }
    }

    /// <summary>
    /// Đại diện cho một kết quả ("feature") (Vẫn giữ nguyên)
    /// </summary>
    public class VietMapApiFeature
    {
        [JsonPropertyName("geometry")]
        public VietMapApiGeometry Geometry { get; set; }

        [JsonPropertyName("properties")]
        public VietMapApiProperties Properties { get; set; }
    }

    public class VietMapApiGeometry
    {
        [JsonPropertyName("coordinates")]
        public List<double> Coordinates { get; set; }

        public double Longitude => Coordinates != null && Coordinates.Count > 0 ? Coordinates[0] : 0.0;
        public double Latitude => Coordinates != null && Coordinates.Count > 1 ? Coordinates[1] : 0.0;
    }

    public class VietMapApiProperties
    {
        [JsonPropertyName("label")]
        public string Label { get; set; } // Địa chỉ đầy đủ
    }

    /// <summary>
    /// DTO mà Interface của bạn yêu cầu (Giữ nguyên)
    /// </summary>
    public class VietMapGeocodeResultV2
    {
        public string Address { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public List<double> Coordinates { get; set; }
    }

    // --- SERVICE IMPLEMENTATION ---
    public class VietMapService : IVietMapService
    {
        private readonly HttpClient _httpClient;
        private readonly VietMapSetting _settings;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ILogger<VietMapService> _logger;

        public VietMapService(IHttpClientFactory httpClientFactory, IOptions<VietMapSetting> settings, ILogger<VietMapService> logger)
        {
            _settings = settings.Value;
            _httpClient = httpClientFactory.CreateClient("VietMapClient");
            _httpClient.BaseAddress = new Uri(_settings.BaseUrl); // "https://maps.vietmap.vn/"
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };
        }

        // --- SỬA HÀM SEARCHASYNC ĐỂ KHỚP VỚI POSTMAN (V1) ---
        public async Task<List<VietMapGeocodeResultV2>> SearchAsync(string text, string? focusLatLon = null)
        {
            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                throw new InvalidOperationException("VietMap API key is not configured.");
            }

            var encodedText = HttpUtility.UrlEncode(text);

            // SỬA LỖI: Dùng URL V1 (giống Postman)
            var url = $"api/search?apikey={_settings.ApiKey}&text={encodedText}";

            if (!string.IsNullOrWhiteSpace(focusLatLon))
            {
                url += $"&focus={focusLatLon}";
            }

            string content = string.Empty;
            try
            {
                var response = await _httpClient.GetAsync(url);
                content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("VietMap Search API (V1) call failed. Status: {StatusCode}, Response: {Content}", response.StatusCode, content);
                    return new List<VietMapGeocodeResultV2>();
                }

                // SỬA LỖI: Deserialize vào DTO V1 (VietMapSearchV1Response)
                var apiResponse = JsonSerializer.Deserialize<VietMapSearchV1Response>(content, _jsonOptions);

                // Kiểm tra cấu trúc lồng nhau
                if (apiResponse == null || apiResponse.Code != "OK" || apiResponse.Data?.Features == null)
                {
                    _logger.LogWarning("VietMap Search API (V1) returned OK but data was invalid. Content: {Content}", content);
                    return new List<VietMapGeocodeResultV2>();
                }

                // Map (chuyển đổi) từ List<VietMapApiFeature> sang List<VietMapGeocodeResult>
                var results = apiResponse.Data.Features.Select(feature => new VietMapGeocodeResultV2
                {
                    Address = feature.Properties?.Label,
                    Latitude = feature.Geometry?.Latitude ?? 0.0,
                    Longitude = feature.Geometry?.Longitude ?? 0.0,
                    Coordinates = feature.Geometry?.Coordinates
                }).ToList();

                return results;
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "VietMap JSON Deserialize (V1) FAILED. API response was: {Content}", content);
                return new List<VietMapGeocodeResultV2>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred in VietMap SearchAsync (V1).");
                return new List<VietMapGeocodeResultV2>();
            }
        }

        // --- GeocodeAsync (Hàm này không cần sửa) ---
        public async Task<Location?> GeocodeAsync(string address, string? focusLatLon = null)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return new Location(address, 0.0, 0.0);
            }

            var searchResults = await SearchAsync(address, focusLatLon);
            var bestMatch = searchResults.FirstOrDefault();

            if (bestMatch == null || bestMatch.Coordinates == null || bestMatch.Coordinates.Count < 2)
            {
                return new Location(address, 0.0, 0.0);
            }

            return new Location(
                bestMatch.Address,
                bestMatch.Latitude,
                bestMatch.Longitude
            );
        }

        // --- GetRouteAsync (Sửa URL và lỗi .Value) ---
        public async Task<VietMapPath?> GetRouteAsync(Location start, Location end, string vehicleType = "car", int capacityKg = 0)
        {
            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                throw new InvalidOperationException("VietMap API key is not configured.");
            }

            if (!start.Latitude.HasValue || !start.Longitude.HasValue ||
                !end.Latitude.HasValue || !end.Longitude.HasValue)
            {
                _logger.LogWarning("GetRouteAsync failed: Start or End location is missing coordinates.");
                return null;
            }

            var startPoint = $"{start.Latitude.Value.ToString(CultureInfo.InvariantCulture)},{start.Longitude.Value.ToString(CultureInfo.InvariantCulture)}";
            var endPoint = $"{end.Latitude.Value.ToString(CultureInfo.InvariantCulture)},{end.Longitude.Value.ToString(CultureInfo.InvariantCulture)}";

            var queryParams = HttpUtility.ParseQueryString(string.Empty);
            queryParams["apikey"] = _settings.ApiKey;
            queryParams.Add("point", startPoint);
            queryParams.Add("point", endPoint);
            queryParams["vehicle"] = vehicleType.ToLower();
            queryParams["points_encoded"] = "true";

            if (vehicleType.ToLower() == "truck")
            {
                queryParams["capacity"] = capacityKg.ToString();
            }

            // SỬA LỖI: Dùng URL V3 cho Route (API Route chỉ có V3)
            var url = $"api/route/v3?{queryParams.ToString()}";

            try
            {
                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("VietMap Route API (V3) call failed. Status: {StatusCode}, Response: {Content}", response.StatusCode, content);
                    return null;
                }

                var routeResponse = JsonSerializer.Deserialize<VietMapRouteResponse>(content, _jsonOptions);

                if (routeResponse == null || routeResponse.Code != "OK" || routeResponse.Paths == null || !routeResponse.Paths.Any())
                {
                    _logger.LogWarning("VietMap Route API (V3) returned OK but no path was found. Message: {Message}", routeResponse?.Messages);
                    return null;
                }

                return routeResponse.Paths[0];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred in VietMap GetRouteAsync (V3).");
                return null;
            }
        }
    }
}