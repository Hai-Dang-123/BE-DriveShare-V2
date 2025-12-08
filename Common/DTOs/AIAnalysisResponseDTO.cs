using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class AIAnalysisResponseDTO
    {
        public bool IsSuccess { get; set; }
        public AIAnalysisResult Result { get; set; }
        public string RawContent { get; set; }
    }

    public class AIAnalysisResult
    {
        [JsonPropertyName("score")]
        public double Score { get; set; } // Tổng điểm (0-10)

        [JsonPropertyName("verdict")]
        public string Verdict { get; set; } // Kết luận: "KÈO SIÊU THƠM", "CẨN TRỌNG"

        // [MỚI] Tóm tắt ngắn gọn để hiện trên Card
        [JsonPropertyName("shortSummary")]
        public string ShortSummary { get; set; }

        [JsonPropertyName("financial")]
        public AnalysisFinancial Financial { get; set; }

        [JsonPropertyName("operational")]
        public AnalysisOperational Operational { get; set; }

        // [MỚI] Danh sách hành động cụ thể (Actionable Items)
        [JsonPropertyName("recommendedActions")]
        public List<string> RecommendedActions { get; set; }

        [JsonPropertyName("riskWarning")]
        public string? RiskWarning { get; set; }
    }

    public class AnalysisFinancial
    {
        [JsonPropertyName("assessment")]
        public string Assessment { get; set; } // "Lợi nhuận cao"

        [JsonPropertyName("estimatedRevenue")]
        public string EstimatedRevenue { get; set; } // "Khoảng 12 - 15 triệu"

        [JsonPropertyName("marketTrend")]
        public string MarketTrend { get; set; } // "Đang tăng", "Bình ổn", "Đang giảm"

        [JsonPropertyName("profitabilityScore")]
        public int ProfitabilityScore { get; set; } // Điểm lợi nhuận (0-10) để vẽ biểu đồ

        [JsonPropertyName("details")]
        public string Details { get; set; }
    }

    public class AnalysisOperational
    {
        [JsonPropertyName("vehicleRecommendation")]
        public string VehicleRecommendation { get; set; }

        [JsonPropertyName("routeDifficulty")]
        public string RouteDifficulty { get; set; } // "Dễ", "Trung bình", "Khó (Đèo dốc)"

        [JsonPropertyName("urgencyLevel")]
        public string UrgencyLevel { get; set; } // "Thấp", "Cao", "Gấp gáp"

        [JsonPropertyName("cargoNotes")]
        public string CargoNotes { get; set; }

        [JsonPropertyName("routeNotes")]
        public string RouteNotes { get; set; }
    }
}
