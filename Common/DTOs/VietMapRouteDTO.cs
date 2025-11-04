using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Common.DTOs
{
    // Class cha chứa toàn bộ phản hồi
    public class VietMapRouteResponse
    {
        [JsonPropertyName("code")]
        public string Code { get; set; }

        [JsonPropertyName("paths")]
        public List<VietMapPath> Paths { get; set; }

        [JsonPropertyName("messages")]
        public string? Messages { get; set; }
    }

    // Class chứa thông tin 1 tuyến đường
    public class VietMapPath
    {
        [JsonPropertyName("distance")]
        public double Distance { get; set; } // Tổng khoảng cách (mét)

        [JsonPropertyName("time")]
        public long Time { get; set; } // Tổng thời gian (mili giây)

        [JsonPropertyName("points")]
        public string Points { get; set; } // Chuỗi polyline mã hóa (Đây là RouteData)

        [JsonPropertyName("instructions")]
        public List<VietMapInstruction> Instructions { get; set; }

        [JsonPropertyName("bbox")]
        public List<double> Bbox { get; set; }
    }

    // Class chứa chỉ dẫn (nếu bạn cần)
    public class VietMapInstruction
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("distance")]
        public double Distance { get; set; }

        [JsonPropertyName("time")]
        public long Time { get; set; }

        [JsonPropertyName("street_name")]
        public string StreetName { get; set; }
    }
}
