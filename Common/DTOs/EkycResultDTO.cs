using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    // DTO tổng hợp kết quả trả về từ quy trình EKYC
    public class EkycResultDTO
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }

        public string? FrontHash { get; set; }
        public string? BackHash { get; set; }
        public string? FaceHash { get; set; }

        public bool IsRealCard { get; set; }
        public double? FaceMatchScore { get; set; }

        public VnptOcrData? OcrData { get; set; }

        // Lưu chuỗi JSON gốc để audit
        public string? OcrRawJson { get; set; }
    }

}
