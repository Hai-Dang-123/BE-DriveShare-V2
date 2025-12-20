using DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Threading.Tasks;

namespace DAL.UnitOfWork
{
    public interface IUnitOfWork : IDisposable
    {
        // --- 1. Repositories ---
        IBaseContractRepository BaseContractRepo { get; }
        IBaseUserRepository BaseUserRepo { get; }
        IContractTemplateRepository ContractTemplateRepo { get; }
        IContractTermRepository ContractTermRepo { get; }
        IDeliveryRecordRepository DeliveryRecordRepo { get; }
        IDeliveryRecordTemplateRepository DeliveryRecordTemplateRepo { get; }
        IDeliveryRecordTermRepository DeliveryRecordTermRepo { get; }
        IDriverActivityLogRepository DriverActivityLogRepo { get; }
        IDriverRepository DriverRepo { get; }
        IItemImageRepository ItemImageRepo { get; }
        IItemRepository ItemRepo { get; }
        IOwnerDriverLinkRepository OwnerDriverLinkRepo { get; }
        IOwnerRepository OwnerRepo { get; }
        IPackageImageRepository PackageImageRepo { get; }
        IPackageRepository PackageRepo { get; }
        IPostPackageRepository PostPackageRepo { get; }
        IPostTripRepository PostTripRepo { get; }
        IProviderRepository ProviderRepo { get; }
        IRoleRepository RoleRepo { get; }
        IShippingRouteRepository ShippingRouteRepo { get; }
        ITransactionRepository TransactionRepo { get; }
        //ITripCompensationRepository TripCompensationRepo { get; }
        ITripContactRepository TripContactRepo { get; }
        ITripDeliveryIssueImageRepository TripDeliveryIssueImageRepo { get; }
        ITripDeliveryIssueRepository TripDeliveryIssueRepo { get; }
        ITripDeliveryRecordRepository TripDeliveryRecordRepo { get; }
        ITripDriverAssignmentRepository TripDriverAssignmentRepo { get; }
        ITripDriverContractRepository TripDriverContractRepo { get; }
        ITripProviderContractRepository TripProviderContractRepo { get; }
        ITripRepository TripRepo { get; }
        ITripRouteRepository TripRouteRepo { get; }
        ITripRouteSuggestionRepository TripRouteSuggestionRepo { get; }
        IUserDocumentRepository UserDocumentRepo { get; }
        IUserTokenRepository UserTokenRepo { get; }
        IUserViolationRepository UserViolationRepo { get; }
        IVehicleDocumentRepository VehicleDocumentRepo { get; }
        IVehicleImageRepository VehicleImageRepo { get; }
        IVehicleRepository VehicleRepo { get; }
        IVehicleTypeRepository VehicleTypeRepo { get; }
        IWalletRepository WalletRepo { get; }
        IPostContactRepository PostContactRepo { get; }
        IDriverWorkSessionRepository DriverWorkSessionRepo { get; }
        IContactTokenRepository ContactTokenRepo { get; }
        ITripSurchargeRepository TripSurchargeRepo { get; }
        ITripVehicleHandoverRecordRepository TripVehicleHandoverRecordRepo { get; }
        ITripVehicleHandoverIssueRepository TripVehicleHandoverIssueRepo { get; }
        ITripVehicleHandoverIssueImageRepository TripVehicleHandoverIssueImageRepo { get; }
        ITripVehicleHandoverTermResultRepository TripVehicleHandoverTermResultRepo { get; }
        IPackageHandlingDetailRepository PackageHandlingDetailRepo { get; }
        IInspectionHistoryRepository InspectionHistoryRepo { get; }
        ITruckRestrictionRepository TruckRestrictionRepo { get; }
        IUserDeviceTokenRepository UserDeviceTokenRepo { get; }
        INotificationRepository NotificationRepo { get; }
        IPostTripDetailRepository PostTripDetailRepo { get; }

        // --- 2. Save Changes ---
        Task<int> SaveAsync();
        Task<bool> SaveChangeAsync();

        // --- 3. Transaction ---
        // Trả về IDbContextTransaction để Service tự quản lý scope
        Task<IDbContextTransaction> BeginTransactionAsync();
    }
}