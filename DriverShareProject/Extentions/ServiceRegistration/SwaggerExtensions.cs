using Microsoft.OpenApi.Models;

namespace DriverShareProject.Extentions.ServiceRegistration
{
    public static class SwaggerExtensions
    {
        public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
        {
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Driver Share API",
                    Version = "v1"
                });

                // ✅ Định nghĩa JWT Bearer Authentication
                var securityScheme = new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Description = @"JWT Authorization header using the Bearer scheme. 
                                    Enter 'Bearer' [space] and then your token in the text input below.
                                    Example: 'Bearer abcdef123456'",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,     // 🔥 Đây là điểm quan trọng
                    Scheme = "bearer",                  // 🔥 Lowercase 'bearer'
                    BearerFormat = "JWT",
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                };

                options.AddSecurityDefinition("Bearer", securityScheme);

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        securityScheme,
                        new List<string>()
                    }
                });
            });

            return services;
        }
    }
}
