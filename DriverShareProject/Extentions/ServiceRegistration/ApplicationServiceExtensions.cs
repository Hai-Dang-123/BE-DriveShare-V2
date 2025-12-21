
using BLL.Services.Implement;
using BLL.Services.Impletement;
using BLL.Services.Interface;
using BLL.Utilities;
using DAL.Context;
using DAL.Repositories.Implement;
using DAL.Repositories.Interface;
using DAL.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using System.Net;

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

            services.AddScoped<IDeliveryRecordTemplateService, DeliveryRecordTemplateService>();


            services.AddScoped<IDeliveryRecordTermService, DeliveryRecordTermService>();
            services.AddScoped<IUserDocumentService, UserDocumentService>();
            services.AddScoped<IVehicleDocumentService, VehicleDocumentService>();



            //services.AddScoped<UserUtility>();
            services.AddScoped<IAdminservices, Adminservices>();
            services.AddScoped<IRoleService, RoleService>();
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

          


            services.AddScoped<ITripDeliveryIssueImageService, TripDeliveryIssueImageService>();
            services.AddScoped<ITripDeliveryIssueService, TripDeliveryIssueService>();
            //services.AddScoped<ITripCompensationService, TripCompensationService>();
            services.AddScoped<ITripDeliveryRecordService, TripDeliveryRecordService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IPostContactService, PostContactService>();
            services.AddScoped<IVehicleDocumentService, VehicleDocumentService>();
            services.AddScoped<IWalletService, WalletService>();
            services.AddScoped<ITransactionService, TransactionService>();
            services.AddScoped<ITripDriverAssignmentService, TripDriverAssignmentService>();
            services.AddScoped<IPostTripService, PostTripService>();
            services.AddScoped<IDriverWorkSessionService, DriverWorkSessionService>();
            services.AddScoped<ITripVehicleHandoverRecordService, TripVehicleHandoverRecordService>();
            services.AddScoped<ITripSurchargeService, TripSurchargeService>();
            services.AddScoped<ITripVehicleHandoverRecordService, TripVehicleHandoverRecordService>();
            services.AddScoped<IPostAnalysisService, PostAnalysisService>();
            services.AddScoped<IAIService, AIService>();
            services.AddScoped<ITrafficRestrictionService, TrafficRestrictionService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IDriverActivityLogService, DriverActivityLogService>();

            services.AddScoped<IVietMapService, VietMapService>();
            services.AddScoped<IFirebaseUploadService, FirebaseUploadService>();
            services.AddScoped<IEmailService, EmailService>();
            //services.AddScoped<IEKYCService, EKYCService>();
            //services.AddScoped<IVNPTTokenService, VNPTTokenService>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();
            services.AddScoped<UserUtility>();

            services.AddScoped<ISepayService, SepayService>();
            services.AddHttpClient<IVNPTTokenService, VNPTTokenService>();
            // Trong Program.cs hoặc Startup.cs
            services.AddHttpClient<IEKYCService, EKYCService>()
                .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                {
                    // 1. Cho phép mọi chứng chỉ SSL (Fix lỗi SSL Handshake nếu có)
                    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true,

                    // 2. Tự động giải nén GZip (Postman tự làm cái này, C# mặc định thì không)
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,

                    // 3. Tắt Cookie Container (Để tránh lưu cache session cũ)
                    UseCookies = false
                });

            return services;
        }
    }
}
