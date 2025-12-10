using Common.Settings;

namespace DriverShareProject.Extentions.BuilderExtensions
{
    public static class ConfigurationBuilderExtensions
    {

        public static WebApplicationBuilder AddAppConfiguration(this WebApplicationBuilder builder)
        {
            //EmailSettings
            builder.Services.Configure<EmailSettings>(
                builder.Configuration.GetSection("Email"));

            // Các config khác 

            // Middleware Performance
            builder.Services.Configure<PerformanceSetting>(
                builder.Configuration.GetSection("Middleware:Performance"));

            //SePay
            builder.Services.Configure<SePaySetting>(builder.Configuration.GetSection("SePay"));

            //Firebase
            builder.Services.Configure<FirebaseSetting>(builder.Configuration.GetSection("Firebase"));

            //VietMap
            builder.Services.Configure<VietMapSetting>(builder.Configuration.GetSection("VietMap"));

            // VNPT Auth
            builder.Services.Configure<VNPTAuthSettings>(builder.Configuration.GetSection("VNPTAuth"));

            // OpenAI
            builder.Services.Configure<OpenAISetting>(builder.Configuration.GetSection("OpenAI"));



            return builder;
        }
    }
}
