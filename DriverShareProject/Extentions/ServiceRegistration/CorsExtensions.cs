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
            .WithOrigins(
                "http://localhost:8081",      // Expo web
                "http://localhost:5174",      // Expo web

                "http://192.168.100.49:19000", // Expo mobile dev
                "http://192.168.100.49:19006", // Expo web dev server
            "http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
           

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
