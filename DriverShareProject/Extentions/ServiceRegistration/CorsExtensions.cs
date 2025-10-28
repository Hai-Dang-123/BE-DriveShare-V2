namespace DriverShareProject.Extentions.ServiceRegistration
{
    public static class CorsExtensions
    {
        private const string DefaultCorsPolicyName = "DefaultCorsPolicy";

        public static IServiceCollection AddCorsPolicy(this IServiceCollection services)
        {
            services.AddCors(options =>
            {
                options.AddPolicy(name: DefaultCorsPolicyName, builder =>
                {
                    builder
                        .AllowAnyOrigin() // Hoặc thay bằng .WithOrigins("https://app.com") nếu cần fix origin
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });

            return services;
        }

        public static IApplicationBuilder UseCorsPolicy(this IApplicationBuilder app)
        {
            app.UseCors(DefaultCorsPolicyName);
            return app;
        }
    }
}
