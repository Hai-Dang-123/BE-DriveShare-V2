using BLL.Services.Interface;
using Common.DTOs;
using Common.Settings;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class AIService : IAIService
    {
        private readonly HttpClient _httpClient;
        private readonly OpenAISetting _settings;

        public AIService(HttpClient httpClient, IOptions<OpenAISetting> settings)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _httpClient.BaseAddress = new Uri("https://api.openai.com/v1/");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        }

        public async Task<AIAnalysisResponseDTO> AnalyzePostPackageAsync(object postData)
        {
            var jsonInput = JsonSerializer.Serialize(postData);
            var systemPrompt = @"Bạn là Chuyên gia Chiến lược Logistics cấp cao. 
Nhiệm vụ: Phân tích đơn hàng vận chuyển (PostPackage) để tư vấn cho Chủ xe.

Yêu cầu đầu ra: Trả về JSON chuẩn (không markdown) khớp với cấu trúc sau:
{
  ""score"": (number 0-10),
  ""verdict"": (string ngắn gọn, viết hoa, vd: ""KÈO THƠM"", ""RỦI RO CAO""),
  ""shortSummary"": (string, tóm tắt 1 câu để hiển thị nhanh),
  ""financial"": {
    ""assessment"": (string, đánh giá chung),
    ""estimatedRevenue"": (string, ước lượng doanh thu/lợi nhuận),
    ""marketTrend"": (string, chọn 1 trong: ""Tăng mạnh"", ""Tăng nhẹ"", ""Bình ổn"", ""Giảm""),
    ""profitabilityScore"": (int 0-10, điểm lợi nhuận),
    ""details"": (string, giải thích chi tiết)
  },
  ""operational"": {
    ""vehicleRecommendation"": (string),
    ""routeDifficulty"": (string, vd: ""Dễ đi"", ""Đèo dốc khó đi""),
    ""urgencyLevel"": (string, vd: ""Thư thả"", ""Rất gấp""),
    ""cargoNotes"": (string),
    ""routeNotes"": (string)
  },
  ""recommendedActions"": [
    (string, hành động 1, vd: ""Nên gọi điện chốt ngay""),
    (string, hành động 2, vd: ""Yêu cầu ứng trước 50%""),
    (string, hành động 3)
  ],
  ""riskWarning"": (string hoặc null)
}";

            return await CallOpenAIAsync(systemPrompt, $"Dữ liệu đơn hàng chi tiết: {jsonInput}");
        }

        public async Task<AIAnalysisResponseDTO> AnalyzePostTripAsync(object postData)
        {
            var jsonInput = JsonSerializer.Serialize(postData);
            var systemPrompt = @"Bạn là một Cố vấn Sự nghiệp & Pháp lý cho Tài xế xe tải chuyên nghiệp tại Việt Nam.
    Nhiệm vụ: Phân tích bài đăng tuyển dụng (PostTrip) để bảo vệ quyền lợi và tư vấn thu nhập cho Tài xế.

    Hãy phân tích sâu:

    1.  **Phân tích Thu nhập & Phúc lợi (Income & Benefits):**
        * Đánh giá mức lương/thù lao so với công sức (km, thời gian, loại xe).
        * Tính toán thu nhập thực tế theo giờ làm việc.
        * So sánh với mức lương trung bình của tài xế cùng hạng bằng lái (C, D, E, FC).

    2.  **Đánh giá Phương tiện & An toàn (Vehicle & Safety):**
        * Nhận xét về dòng xe (Đời xe, thương hiệu): Xe này có hay hỏng vặt không? Có dễ lái không? (Dựa trên model xe).
        * An toàn lao động: Loại hàng hóa có độc hại hay nguy hiểm không?

    3.  **Điều kiện làm việc & Pháp lý (Working Conditions):**
        * Lộ trình: Cung đường này có ""dễ thở"" không? (Hay tắc biên, hay bắn tốc độ, đường xấu).
        * Thời gian: Có vi phạm luật 4h liên tục/10h ngày không? Có áp lực tiến độ không?
        * Quyền lợi khác: Có phụ cấp ăn ở, luật lá không?

    Định dạng đầu ra: JSON hợp lệ (không markdown), cấu trúc:
    {
      ""score"": (number 0-10),
      ""verdict"": (string, vd: ""VIỆC TỐT - LƯƠNG CAO"", ""CỰC NHỌC - LƯƠNG THẤP""),
      ""financial"": {
        ""assessment"": (string),
        ""details"": (string, phân tích thu nhập/so sánh)
      },
      ""operational"": {
        ""vehicleRecommendation"": (string, nhận xét về chất lượng xe/kỹ năng cần thiết),
        ""cargoNotes"": (string, lưu ý về công việc bốc xếp/trách nhiệm hàng hóa),
        ""routeNotes"": (string, đánh giá cung đường/áp lực thời gian)
      },
      ""riskWarning"": (string, cảnh báo về sức khỏe, an toàn hoặc pháp lý)
    }";

            return await CallOpenAIAsync(systemPrompt, $"Dữ liệu tuyển dụng chi tiết: {jsonInput}");
        }

        private async Task<AIAnalysisResponseDTO> CallOpenAIAsync(string systemPrompt, string userPrompt)
        {
            try
            {
                var requestBody = new
                {
                    model = _settings.Model,
                    messages = new object[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    max_tokens = 800,
                    temperature = 0.5
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("chat/completions", content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return new AIAnalysisResponseDTO { IsSuccess = false, RawContent = $"Error: {response.StatusCode} - {responseString}" };

                using var doc = JsonDocument.Parse(responseString);
                var aiContent = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

                // Clean Markdown
                var cleanJson = aiContent.Replace("```json", "").Replace("```", "").Trim();

                var resultObj = JsonSerializer.Deserialize<AIAnalysisResult>(cleanJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return new AIAnalysisResponseDTO { IsSuccess = true, Result = resultObj, RawContent = cleanJson };
            }
            catch (Exception ex)
            {
                return new AIAnalysisResponseDTO { IsSuccess = false, RawContent = $"Exception: {ex.Message}" };
            }
        }
    }
}