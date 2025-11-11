using DAL.Repositories.Interface;
using System;
using System.Threading.Tasks;

namespace DAL.UnitOfWork
{
    public interface IUnitOfWork : IDisposable
    {
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
        ITripCompensationRepository TripCompensationRepo { get; }
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


        // Save changes

        Task<int> SaveAsync();
        Task<bool> SaveChangeAsync();

        // Transaction methods
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}
