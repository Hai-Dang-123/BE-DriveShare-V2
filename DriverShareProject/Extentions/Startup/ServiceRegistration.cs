using DriverShareProject.Extentions.ServiceRegistration;

namespace DriverShareProject.Extentions.Startup
{
    public static class ServiceRegistration
    {
        /// <summary>
        /// Đăng ký toàn bộ các service cần thiết cho ứng dụng vào DI container.
        /// </summary>
        public static IServiceCollection RegisterAllServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Đăng ký các service chính của ứng dụng (DbContext, AutoMapper, Repositories, Services...)
            services.AddApplicationServices(configuration);

            // Đăng ký xác thực bằng JWT Bearer, cấu hình TokenValidationParameters, issuer, audience, key...
            services.AddJwtAuthentication();

            // Đăng ký cấu hình Swagger cho tài liệu API (OpenAPI)
            services.AddSwaggerDocumentation();

            // Đăng ký chính sách CORS cho phép FE (Android Studio app) gọi API từ domain khác
            services.AddCorsPolicy();

            // Đăng ký middleware giới hạn tốc độ gọi API (Rate Limiting) dựa theo IP client
            services.AddGlobalRateLimiting();

            // Đăng ký middleware nén dữ liệu (Gzip) để tăng tốc độ truyền tải HTTP response
            services.AddCustomResponseCompression();

            return services;
        }
    }
}
