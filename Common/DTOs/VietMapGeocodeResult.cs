using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class VietMapGeocodeResult
    {
        [JsonPropertyName("ref_id")]
        public string RefId { get; set; }

        [JsonPropertyName("distance")]
        public double Distance { get; set; }

        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("display")]
        public string Display { get; set; }

        [JsonPropertyName("boundaries")]
        public List<Boundary> Boundaries { get; set; }

        [JsonPropertyName("categories")]
        public List<string> Categories { get; set; }

        [JsonPropertyName("entry_points")]
        public List<EntryPoint> EntryPoints { get; set; }

        // --- QUAN TRỌNG: Trường này bị thiếu trong tài liệu của bạn ---
        // API thực tế trả về tọa độ [longitude, latitude]
        [JsonPropertyName("coordinates")]
        public List<double> Coordinates { get; set; }

        // --- Thuộc tính helper (bỏ qua khi parse JSON) ---
        [JsonIgnore]
        public double Latitude => (Coordinates != null && Coordinates.Count > 1) ? Coordinates[1] : 0;

        [JsonIgnore]
        public double Longitude => (Coordinates != null && Coordinates.Count > 0) ? Coordinates[0] : 0;
    }

    public class Boundary
    {
        [JsonPropertyName("type")]
        public int Type { get; set; } // 0=city, 1=district, 2=ward

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("prefix")]
        public string Prefix { get; set; }

        [JsonPropertyName("full_name")]
        public string FullName { get; set; }
    }

    public class EntryPoint
    {
        [JsonPropertyName("ref_id")]
        public string RefId { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }
    }
}
