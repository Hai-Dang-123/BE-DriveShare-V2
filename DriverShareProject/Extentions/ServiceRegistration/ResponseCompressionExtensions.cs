using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;

namespace DriverShareProject.Extentions.ServiceRegistration
{
    public static class ResponseCompressionExtensions
    {
        public static IServiceCollection AddCustomResponseCompression(this IServiceCollection services)
        {
            services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<GzipCompressionProvider>();
            });

            services.Configure<GzipCompressionProviderOptions>(options =>
            {
                options.Level = CompressionLevel.Fastest;
            });

            return services;
        }
    }
}
