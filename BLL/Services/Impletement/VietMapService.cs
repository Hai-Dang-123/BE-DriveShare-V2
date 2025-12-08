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

       
        public async Task<Location?> GeocodeAsync(string address, string? focusLatLon = null)
        {
            // 1. Giữ lại địa chỉ gốc để nếu thất bại hoàn toàn thì vẫn trả về text gốc (với tọa độ 0,0)
            var originalAddress = address;
            var fallbackLocation = new Location(originalAddress ?? "", 0.0, 0.0);

            if (string.IsNullOrWhiteSpace(address)) return fallbackLocation;

            // 2. Cấu hình logic nới lỏng
            int maxAttempts = 3; // Chỉ thử tối đa 3 lần (Tránh loop vô tận hoặc spam API)
            int currentAttempt = 0;
            string currentSearchText = address;

            try
            {
                while (currentAttempt < maxAttempts && !string.IsNullOrWhiteSpace(currentSearchText))
                {
                    // Gọi API tìm kiếm
                    // Lưu ý: focusLatLon rất quan trọng ở đây. 
                    // Ví dụ: Tìm "Đường Lê Lợi". Nếu có focus ở HCM, nó ra Lê Lợi Q1. Nếu focus ở HN, nó ra Lê Lợi Hà Đông.
                    var searchResults = await SearchAsync(currentSearchText, focusLatLon);

                    // Lấy kết quả tốt nhất
                    var bestMatch = searchResults.FirstOrDefault();

                    // CHECK: Nếu tìm thấy VÀ có tọa độ hợp lệ
                    if (bestMatch != null &&
                        bestMatch.Coordinates != null &&
                        bestMatch.Coordinates.Count >= 2 &&
                        (bestMatch.Latitude != 0 || bestMatch.Longitude != 0))
                    {
                        _logger.LogInformation("Geocode Success at attempt {Attempt}. Input: {Input} -> Found: {Found}",
                            currentAttempt + 1, currentSearchText, bestMatch.Address);

                        return new Location(
                            bestMatch.Address ?? currentSearchText,
                            bestMatch.Latitude,
                            bestMatch.Longitude
                        );
                    }

                    // NẾU KHÔNG TÌM THẤY -> THỰC HIỆN NỚI LỎNG ĐỊA CHỈ (RELAXING)
                    _logger.LogWarning("Geocode failed at attempt {Attempt} for: {Text}. Trying broader area...", currentAttempt + 1, currentSearchText);

                    currentAttempt++;

                    // Logic cắt chuỗi: Tìm dấu phẩy đầu tiên
                    // Ví dụ: "Số 10, Đường ABC, Quận 1" -> Cắt bỏ "Số 10" -> Còn "Đường ABC, Quận 1"
                    int firstCommaIndex = currentSearchText.IndexOf(',');

                    if (firstCommaIndex > -1 && firstCommaIndex < currentSearchText.Length - 1)
                    {
                        // Lấy phần sau dấu phẩy và xóa khoảng trắng thừa
                        currentSearchText = currentSearchText.Substring(firstCommaIndex + 1).Trim();
                    }
                    else
                    {
                        // Nếu không còn dấu phẩy nào để cắt -> Hết cách -> Break vòng lặp
                        break;
                    }
                }

                // Nếu chạy hết vòng lặp mà vẫn không ra tọa độ -> Trả về 0,0
                _logger.LogError("GeocodeAsync: Failed after {Attempt} attempts for address: {Address}", currentAttempt, originalAddress);
                return fallbackLocation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception in GeocodeAsync. Address: {Address}", originalAddress);
                return fallbackLocation;
            }
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

        // --- HÀM MỚI: TÍNH THỜI GIAN DI CHUYỂN DỰ KIẾN (Giờ) ---
        public async Task<double> GetEstimatedDurationHoursAsync(Location start, Location end, string vehicle = "truck")
        {
            // 1. Gọi API lấy lộ trình
            // Nếu start/end chưa có tọa độ, GetRouteAsync sẽ fail. 
            // (Ở Controller/Service gọi hàm này cần đảm bảo Location đã có Lat/Lon hoặc Geocode trước)
            var path = await GetRouteAsync(start, end, vehicle);

            if (path == null)
            {
                // Nếu không tìm thấy đường, trả về 0 hoặc throw exception tùy logic.
                // Ở đây trả về 0 để Service bên ngoài xử lý fallback.
                return 0;
            }

            // 2. Lấy thời gian chạy xe thuần túy (Mili-giây -> Giờ)
            double rawHours = path.Time / (1000.0 * 60 * 60);

            // 3. Tính toán Buffer (Hệ số an toàn cho Logistics)
            // Xe tải chạy thực tế luôn lâu hơn Google Maps/Vietmap dự báo lý tưởng
            double safetyFactor = 1.15; // Cộng thêm 15% (tắc đường, đèn đỏ, đường xấu)
            double loadingUnloadingTime = 0.5; // Cộng thêm 30 phút cho việc lấy xe/đậu xe tại bến (ước lượng trung bình)

            double totalEstimatedHours = (rawHours * safetyFactor) + loadingUnloadingTime;

            return Math.Round(totalEstimatedHours, 2); // Làm tròn 2 số thập phân
        }

        // =========================================================================
        // HÀM MỚI: KIỂM TRA ĐIỂM CÓ NẰM TRÊN LỘ TRÌNH KHÔNG (Offline Calculation)
        // =========================================================================
        public bool IsLocationOnRoute(Location point, string encodedPolyline, double bufferKm = 5.0)
        {
            if (point == null || !point.Latitude.HasValue || !point.Longitude.HasValue || string.IsNullOrEmpty(encodedPolyline))
                return false;

            // 1. Giải mã Polyline thành danh sách tọa độ
            var routePoints = DecodePolyline(encodedPolyline);

            // 2. Kiểm tra khoảng cách từ điểm đến đường gấp khúc (Polyline)
            // Nếu khoảng cách nhỏ nhất <= bufferKm thì trả về true
            return IsPointNearPolyline(point, routePoints, bufferKm);
        }

        // --- HELPER: GIẢI MÃ POLYLINE (Google Encoded Polyline Algorithm) ---
        private List<Location> DecodePolyline(string encodedPoints)
        {
            if (string.IsNullOrEmpty(encodedPoints)) return null;

            var poly = new List<Location>();
            char[] polylineChars = encodedPoints.ToCharArray();
            int index = 0;

            int currentLat = 0;
            int currentLng = 0;
            int next5bits;
            int sum;
            int shifter;

            while (index < polylineChars.Length)
            {
                // Calculate Latitude
                sum = 0;
                shifter = 0;
                do
                {
                    next5bits = (int)polylineChars[index++] - 63;
                    sum |= (next5bits & 31) << shifter;
                    shifter += 5;
                } while (next5bits >= 32);

                if (index >= polylineChars.Length) break;

                currentLat += (sum & 1) == 1 ? ~(sum >> 1) : (sum >> 1);

                // Calculate Longitude
                sum = 0;
                shifter = 0;
                do
                {
                    next5bits = (int)polylineChars[index++] - 63;
                    sum |= (next5bits & 31) << shifter;
                    shifter += 5;
                } while (next5bits >= 32);

                currentLng += (sum & 1) == 1 ? ~(sum >> 1) : (sum >> 1);

                poly.Add(new Location
                {
                    Latitude = Convert.ToDouble(currentLat) / 100000.0,
                    Longitude = Convert.ToDouble(currentLng) / 100000.0
                });
            }

            return poly;
        }

        // --- HELPER: TÍNH KHOẢNG CÁCH TỪ ĐIỂM ĐẾN ĐƯỜNG DẪN ---
        private bool IsPointNearPolyline(Location point, List<Location> routePath, double maxDistKm)
        {
            if (routePath == null || routePath.Count < 2) return false;

            // Duyệt qua từng đoạn thẳng (segment) của lộ trình
            for (int i = 0; i < routePath.Count - 1; i++)
            {
                var p1 = routePath[i];
                var p2 = routePath[i + 1];

                // Tính khoảng cách từ point đến đoạn thẳng p1-p2
                double dist = GetDistanceFromPointToSegment(point, p1, p2);

                if (dist <= maxDistKm) return true; // Tìm thấy điểm gần -> Hợp lệ
            }

            return false;
        }

        // Công thức tính khoảng cách từ điểm C đến đoạn thẳng AB
        private double GetDistanceFromPointToSegment(Location c, Location a, Location b)
        {
            // Chuyển Lat/Lon sang đơn vị mét phẳng (gần đúng) hoặc dùng Haversine
            // Để đơn giản và nhanh, ta dùng công thức hình học phẳng trên toạ độ rồi nhân hệ số Km
            // (Lưu ý: Đây là phép tính gần đúng, đủ dùng cho logic bán kính 5-10km)

            double A = c.Latitude.Value - a.Latitude.Value;
            double B = c.Longitude.Value - a.Longitude.Value;
            double C = b.Latitude.Value - a.Latitude.Value;
            double D = b.Longitude.Value - a.Longitude.Value;

            double dot = A * C + B * D;
            double len_sq = C * C + D * D;
            double param = -1;
            if (len_sq != 0) // avoid division by 0
                param = dot / len_sq;

            double xx, yy;

            if (param < 0)
            {
                xx = a.Latitude.Value;
                yy = a.Longitude.Value;
            }
            else if (param > 1)
            {
                xx = b.Latitude.Value;
                yy = b.Longitude.Value;
            }
            else
            {
                xx = a.Latitude.Value + param * C;
                yy = a.Longitude.Value + param * D;
            }

            double dx = c.Latitude.Value - xx;
            double dy = c.Longitude.Value - yy;

            // Convert độ sang Km (1 độ ~ 111km)
            double distDegree = Math.Sqrt(dx * dx + dy * dy);
            return distDegree * 111.0;
        }
    }
}