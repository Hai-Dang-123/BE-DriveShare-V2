
using BLL.Services.Impletement;
using BLL.Services.Interface;
using BLL.Utilities;
using DAL.Context;
using DAL.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace DriverShareProject.Extentions.ServiceRegistration
{
    public static class ApplicationServiceExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            //services.AddAutoMapper(typeof(AutoMapperProfile));


            services.AddDbContext<DriverShareAppContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            //Service
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IItemService, ItemService>();
            services.AddScoped<IItemImagesService, ItemImagesService>();


            services.AddScoped<IFirebaseUploadService, FirebaseUploadService>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<UserUtility>();

            //services.AddHttpClient<IVNPTTokenService, VNPTTokenService>();
            //services.AddHttpClient<IEKYCService, EKYCService>(client =>
            //{
            //    client.BaseAddress = new Uri(configuration["VNPTAuth:BaseUrl"]);
            //    client.Timeout = TimeSpan.FromMinutes(5);
            //});

            return services;
        }
    }
}
