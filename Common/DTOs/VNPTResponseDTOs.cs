// Wrapper chung của VNPT
using System.Text.Json.Serialization;

public class VnptApiResponse<T>
{
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("object")]
    public T Object { get; set; }
}

// Dữ liệu OCR (Dùng chung cho cả CCCD và GPLX - map các trường tương đương)
public class VnptOcrData
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("birth_day")]
    public string? BirthDay { get; set; }

    [JsonPropertyName("origin_location")]
    public string? OriginLocation { get; set; }

    [JsonPropertyName("recent_location")]
    public string? RecentLocation { get; set; }

    [JsonPropertyName("issue_place")]
    public string? IssuePlace { get; set; }

    [JsonPropertyName("issue_date")]
    public string? IssueDate { get; set; }

    [JsonPropertyName("valid_date")]
    public string? ValidDate { get; set; }

    // --- Riêng cho GPLX ---
    [JsonPropertyName("rank")]
    public string? Rank { get; set; }

    [JsonPropertyName("card_type")]
    public string? CardType { get; set; }

    // --- Cảnh báo & Tampering ---
    [JsonPropertyName("tampering")]
    public VnptTampering? Tampering { get; set; }

    [JsonPropertyName("warning_msg")]
    public List<string>? WarningMsg { get; set; }

    // Map từ checking_result_front của GPLX sang danh sách warning chung nếu cần
    [JsonPropertyName("checking_result_front")]
    public VnptCheckingResultFront? CheckingResultFront { get; set; }
}

public class VnptTampering
{
    [JsonPropertyName("is_legal")]
    public string? IsLegal { get; set; } // "yes", "no"

    [JsonPropertyName("warning")]
    public List<string>? Warning { get; set; }
}

public class VnptCheckingResultFront
{
    [JsonPropertyName("corner_cut_result")]
    public string? CornerCutResult { get; set; } // "0" hoặc "1"

    [JsonPropertyName("recaptured_result")]
    public string? RecapturedResult { get; set; } // "0" hoặc "1" (Chụp lại qua màn hình)

    [JsonPropertyName("check_photocopied_result")]
    public string? CheckPhotocopiedResult { get; set; } // "0" hoặc "1"

    [JsonPropertyName("edited_result")]
    public string? EditedResult { get; set; }
}

public class VnptCardLivenessData
{
    [JsonPropertyName("liveness")]
    public string? Liveness { get; set; } // "success", "failure"
}

public class VnptFaceCompareData
{
    [JsonPropertyName("prob")]
    public double Prob { get; set; }
}