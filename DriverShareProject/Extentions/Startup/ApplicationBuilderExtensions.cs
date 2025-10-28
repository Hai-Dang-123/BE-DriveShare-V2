using DriverShareProject.Extentions.Middleware;

namespace DriverShareProject.Extentions.Startup
{
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Gọi toàn bộ middleware trung gian dùng chung cho toàn bộ pipeline.
        /// </summary>
        public static IApplicationBuilder UseApplicationMiddlewares(this IApplicationBuilder app)
        {
            // 📦 Bật nén dữ liệu (Gzip, Brotli...) để giảm kích thước HTTP response trả về
            app.UseResponseCompression();

            // 🚦 Kích hoạt rate limiter (giới hạn số request theo IP, tránh spam, bảo vệ hệ thống)
            app.UseRateLimiter();

            // 📊 Ghi log toàn bộ request/response để phục vụ audit, debugging, hoặc thống kê
            app.UseMiddleware<LoggingMiddleware>();

            // ⏱️ Middleware đo thời gian thực thi của từng request, giúp theo dõi hiệu suất API
            app.UseMiddleware<PerformanceMiddleware>();


            return app;
        }
    }
}
