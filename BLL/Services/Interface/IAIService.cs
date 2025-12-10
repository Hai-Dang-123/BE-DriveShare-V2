using Common.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IAIService
    {
        /// <summary>
        /// Phân tích đơn hàng (PostPackage) để tư vấn cho chủ xe.
        /// </summary>
        /// <param name="postData">Dữ liệu đơn hàng (object)</param>
        /// <returns>Kết quả phân tích dưới dạng DTO</returns>
        Task<AIAnalysisResponseDTO> AnalyzePostPackageAsync(object postData);

        /// <summary>
        /// Phân tích bài đăng tuyển dụng tài xế (PostTrip) để tư vấn cho tài xế.
        /// </summary>
        /// <param name="postData">Dữ liệu bài đăng tuyển dụng (object)</param>
        /// <returns>Kết quả phân tích dưới dạng DTO</returns>
        Task<AIAnalysisResponseDTO> AnalyzePostTripAsync(object postData);
    }
}
