using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class EkycAnalysisResultDTO
    {
        // Thông tin cơ bản trích xuất được
        public string OcrName { get; set; } = string.Empty;
        public string OcrId { get; set; } = string.Empty;
        public string OcrBirthDay { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;

        // Đánh giá tổng quan
        public bool IsValidDocument { get; set; } // Dựa trên is_legal của eKYC
        public double OverallScore { get; set; } // Điểm tin cậy trung bình

        // Các cảnh báo chi tiết (Đã dịch sang tiếng Việt)
        public List<string> Warnings { get; set; } = new List<string>();

        // Chi tiết lỗi (để highlight đỏ)
        public bool HasTampering { get; set; } // Có dấu hiệu chỉnh sửa/giả mạo
        public bool IsExpired { get; set; } // Hết hạn
        public bool IsCornerCut { get; set; } // Mất góc
        public bool IsScreenRecapture { get; set; } // Chụp lại qua màn hình
        public bool DataMismatch { get; set; } // Mặt trước sau không khớp
    }
}
