
using BLL.Services.Implement;
using BLL.Services.Impletement;
using BLL.Services.Interface;
using BLL.Utilities;
using DAL.Context;
using DAL.Repositories.Implement;
using DAL.Repositories.Interface;
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

            //services.AddScoped<IDeliveryRecordTemplateService, DeliveryRecordTemplateService>();
            //services.AddScoped<IDeliveryRecordTermService, DeliveryRecordTermService>();
            //services.AddScoped<IUserDocumentService, UserDocumentService>();
            //services.AddScoped<IVehicleDocumentService, VehicleDocumentService>();


            services.AddScoped<UserUtility>();


            services.AddScoped<IItemService, ItemService>();
            services.AddScoped<IItemImagesService, ItemImagesService>();
            services.AddScoped<IPackageService, PackageService>();
            services.AddScoped<IPackageImageService, PackageImageService>();
            services.AddScoped<IOwnerDriverLinkService, OwnerDriverLinkService>();
            services.AddScoped<IVehicleService, VehicleService>();
            services.AddScoped<IVehicleImageService, VehicleImageService>();
            services.AddScoped<IVehicleTypeService, VehicleTypeService>();
            services.AddScoped<IContractTemplateService, ContractTemplateService>();
            services.AddScoped<IContractTermService, ContractTermService>();

            services.AddScoped<IPostPackageService, PostPackageService>();
            services.AddScoped<ITripContactService, TripContactService>();

            services.AddScoped<ITripService, TripService>();
            services.AddScoped<ITripProviderContractService, TripProviderContractService>();
            services.AddScoped<ITripDriverContractService, TripDriverContractService>();

            services.AddScoped<ITripRouteService, TripRouteService>();
            services.AddScoped<IShippingRouteService, ShippingRouteService>();


            services.AddScoped<IVietMapService, VietMapService>();
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
