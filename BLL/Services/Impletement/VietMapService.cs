using BLL.Services.Interface;
using Common.DTOs;
using Common.Settings;
using Common.ValueObjects;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace BLL.Services.Impletement
{
    public class VietMapService : IVietMapService
    {
        private readonly HttpClient _httpClient;
        private readonly VietMapSetting _settings;
        private readonly JsonSerializerOptions _jsonOptions;

        public VietMapService(IHttpClientFactory httpClientFactory, IOptions<VietMapSetting> settings)
        {
            _settings = settings.Value;
            _httpClient = httpClientFactory.CreateClient("VietMapClient");
            _httpClient.BaseAddress = new Uri(_settings.BaseUrl);

            // Cấu hình Json Serializer
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };
        }

        public async Task<List<VietMapGeocodeResult>> SearchAsync(string text, string? focusLatLon = null)
        {
            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                // TODO: Log lỗi nghiêm trọng
                throw new InvalidOperationException("VietMap API key is not configured.");
            }

            // Mã hóa text để đưa vào URL
            var encodedText = HttpUtility.UrlEncode(text);
            var url = $"/api/search/v3?apikey={_settings.ApiKey}&text={encodedText}";

            // Thêm focus nếu có
            if (!string.IsNullOrWhiteSpace(focusLatLon))
            {
                url += $"&focus={focusLatLon}";
            }

            try
            {
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    // TODO: Log lỗi chi tiết (await response.Content.ReadAsStringAsync())
                    return new List<VietMapGeocodeResult>(); // Trả về rỗng nếu lỗi
                }

                var content = await response.Content.ReadAsStringAsync();
                var results = JsonSerializer.Deserialize<List<VietMapGeocodeResult>>(content, _jsonOptions);

                return results ?? new List<VietMapGeocodeResult>();
            }
            catch (Exception ex)
            {
                // TODO: Log lỗi exception (ex.Message)
                return new List<VietMapGeocodeResult>();
            }
        }

        public async Task<Location?> GeocodeAsync(string address, string? focusLatLon = null)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return new Location(address, 0.0, 0.0); // Trả về 0,0 nếu địa chỉ rỗng
            }

            var searchResults = await SearchAsync(address, focusLatLon);

            // Lấy kết quả đầu tiên (thường là kết quả chính xác nhất)
            var bestMatch = searchResults.FirstOrDefault();

            if (bestMatch == null || bestMatch.Coordinates == null || bestMatch.Coordinates.Count < 2)
            {
                // Không tìm thấy kết quả nào có tọa độ
                // Trả về địa chỉ gốc với tọa độ 0,0
                return new Location(address, 0.0, 0.0);
            }

            // Trả về đối tượng Location
            // API VietMap trả về [Longitude, Latitude]
            return new Location(
                bestMatch.Address,      // Dùng địa chỉ đã được VietMap chuẩn hóa
                bestMatch.Latitude,     // Lấy từ helper property
                bestMatch.Longitude     // Lấy từ helper property
            );
        }

        public async Task<VietMapPath?> GetRouteAsync(Location start, Location end, string vehicleType = "car", int capacityKg = 0)
        {
            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                // TODO: Log lỗi nghiêm trọng
                throw new InvalidOperationException("VietMap API key is not configured.");
            }

            // API VietMap yêu cầu định dạng: latitude,longitude
            // Dùng InvariantCulture để đảm bảo dấu phẩy là ",", không phải "."
            var startPoint = $"{start.Latitude},{start.Longitude}";
            var endPoint = $"{end.Latitude},{end.Longitude}";

            // Xây dựng URL
            var queryParams = HttpUtility.ParseQueryString(string.Empty);
            queryParams["apikey"] = _settings.ApiKey;
            queryParams["point"] = startPoint;
            queryParams["point"] = endPoint;
            queryParams["vehicle"] = vehicleType.ToLower();
            queryParams["points_encoded"] = "true"; // Lấy polyline để lưu vào RouteData

            // Thêm trọng tải nếu là xe tải
            if (vehicleType.ToLower() == "truck")
            {
                queryParams["capacity"] = capacityKg.ToString();
            }

            var url = $"/api/route/v3?{queryParams.ToString()}";

            try
            {
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    // TODO: Log lỗi chi tiết (await response.Content.ReadAsStringAsync())
                    return null; // Trả về null nếu lỗi
                }

                var content = await response.Content.ReadAsStringAsync();
                var routeResponse = JsonSerializer.Deserialize<VietMapRouteResponse>(content, _jsonOptions);

                // Kiểm tra phản hồi thành công và có path
                if (routeResponse == null || routeResponse.Code != "OK" || routeResponse.Paths == null || !routeResponse.Paths.Any())
                {
                    // TODO: Log (routeResponse?.Messages ?? "No route found")
                    return null;
                }

                // Trả về tuyến đường đầu tiên (tối ưu nhất)
                return routeResponse.Paths[0];
            }
            catch (Exception ex)
            {
                // TODO: Log lỗi exception (ex.Message)
                return null;
            }
        }
    }
}
