using DAL.Context;
using DAL.Repositories.Implement;
using DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Threading.Tasks;

namespace DAL.UnitOfWork
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly DriverShareAppContext _context;
        private bool _disposed = false;

        public UnitOfWork(DriverShareAppContext context)
        {
            _context = context;
            // Constructor giờ rất nhẹ, không khởi tạo gì cả.
        }

        // =======================================================================
        // REPOSITORIES (LAZY LOADING IMPLEMENTATION)
        // =======================================================================

        private IBaseContractRepository? _baseContractRepo;
        public IBaseContractRepository BaseContractRepo => _baseContractRepo ??= new BaseContractRepository(_context);

        private IBaseUserRepository? _baseUserRepo;
        public IBaseUserRepository BaseUserRepo => _baseUserRepo ??= new BaseUserRepository(_context);

        private IContractTemplateRepository? _contractTemplateRepo;
        public IContractTemplateRepository ContractTemplateRepo => _contractTemplateRepo ??= new ContractTemplateRepository(_context);

        private IContractTermRepository? _contractTermRepo;
        public IContractTermRepository ContractTermRepo => _contractTermRepo ??= new ContractTermRepository(_context);

        private IDeliveryRecordRepository? _deliveryRecordRepo;
        public IDeliveryRecordRepository DeliveryRecordRepo => _deliveryRecordRepo ??= new DeliveryRecordRepository(_context);

        private IDeliveryRecordTemplateRepository? _deliveryRecordTemplateRepo;
        public IDeliveryRecordTemplateRepository DeliveryRecordTemplateRepo => _deliveryRecordTemplateRepo ??= new DeliveryRecordTemplateRepository(_context);

        private IDeliveryRecordTermRepository? _deliveryRecordTermRepo;
        public IDeliveryRecordTermRepository DeliveryRecordTermRepo => _deliveryRecordTermRepo ??= new DeliveryRecordTermRepository(_context);

        private IDriverActivityLogRepository? _driverActivityLogRepo;
        public IDriverActivityLogRepository DriverActivityLogRepo => _driverActivityLogRepo ??= new DriverActivityLogRepository(_context);

        private IDriverRepository? _driverRepo;
        public IDriverRepository DriverRepo => _driverRepo ??= new DriverRepository(_context);

        private IItemImageRepository? _itemImageRepo;
        public IItemImageRepository ItemImageRepo => _itemImageRepo ??= new ItemImageRepository(_context);

        private IItemRepository? _itemRepo;
        public IItemRepository ItemRepo => _itemRepo ??= new ItemRepository(_context);

        private IOwnerDriverLinkRepository? _ownerDriverLinkRepo;
        public IOwnerDriverLinkRepository OwnerDriverLinkRepo => _ownerDriverLinkRepo ??= new OwnerDriverLinkRepository(_context);

        private IOwnerRepository? _ownerRepo;
        public IOwnerRepository OwnerRepo => _ownerRepo ??= new OwnerRepository(_context);

        private IPackageImageRepository? _packageImageRepo;
        public IPackageImageRepository PackageImageRepo => _packageImageRepo ??= new PackageImageRepository(_context);

        private IPackageRepository? _packageRepo;
        public IPackageRepository PackageRepo => _packageRepo ??= new PackageRepository(_context);

        private IPostPackageRepository? _postPackageRepo;
        public IPostPackageRepository PostPackageRepo => _postPackageRepo ??= new PostPackageRepository(_context);

        private IPostTripRepository? _postTripRepo;
        public IPostTripRepository PostTripRepo => _postTripRepo ??= new PostTripRepository(_context);

        private IProviderRepository? _providerRepo;
        public IProviderRepository ProviderRepo => _providerRepo ??= new ProviderRepository(_context);

        private IRoleRepository? _roleRepo;
        public IRoleRepository RoleRepo => _roleRepo ??= new RoleRepository(_context);

        private IShippingRouteRepository? _shippingRouteRepo;
        public IShippingRouteRepository ShippingRouteRepo => _shippingRouteRepo ??= new ShippingRouteRepository(_context);

        private ITransactionRepository? _transactionRepo;
        public ITransactionRepository TransactionRepo => _transactionRepo ??= new TransactionRepository(_context);

        //private ITripCompensationRepository? _tripCompensationRepo;
        //public ITripCompensationRepository TripCompensationRepo => _tripCompensationRepo ??= new TripCompensationRepository(_context);

        private ITripContactRepository? _tripContactRepo;
        public ITripContactRepository TripContactRepo => _tripContactRepo ??= new TripContactRepository(_context);

        private ITripDeliveryIssueImageRepository? _tripDeliveryIssueImageRepo;
        public ITripDeliveryIssueImageRepository TripDeliveryIssueImageRepo => _tripDeliveryIssueImageRepo ??= new TripDeliveryIssueImageRepository(_context);

        private ITripDeliveryIssueRepository? _tripDeliveryIssueRepo;
        public ITripDeliveryIssueRepository TripDeliveryIssueRepo => _tripDeliveryIssueRepo ??= new TripDeliveryIssueRepository(_context);

        private ITripDeliveryRecordRepository? _tripDeliveryRecordRepo;
        public ITripDeliveryRecordRepository TripDeliveryRecordRepo => _tripDeliveryRecordRepo ??= new TripDeliveryRecordRepository(_context);

        private ITripDriverAssignmentRepository? _tripDriverAssignmentRepo;
        public ITripDriverAssignmentRepository TripDriverAssignmentRepo => _tripDriverAssignmentRepo ??= new TripDriverAssignmentRepository(_context);

        private ITripDriverContractRepository? _tripDriverContractRepo;
        public ITripDriverContractRepository TripDriverContractRepo => _tripDriverContractRepo ??= new TripDriverContractRepository(_context);

        private ITripProviderContractRepository? _tripProviderContractRepo;
        public ITripProviderContractRepository TripProviderContractRepo => _tripProviderContractRepo ??= new TripProviderContractRepository(_context);

        private ITripRepository? _tripRepo;
        public ITripRepository TripRepo => _tripRepo ??= new TripRepository(_context);

        private ITripRouteRepository? _tripRouteRepo;
        public ITripRouteRepository TripRouteRepo => _tripRouteRepo ??= new TripRouteRepository(_context);

        private ITripRouteSuggestionRepository? _tripRouteSuggestionRepo;
        public ITripRouteSuggestionRepository TripRouteSuggestionRepo => _tripRouteSuggestionRepo ??= new TripRouteSuggestionRepository(_context);

        private IUserDocumentRepository? _userDocumentRepo;
        public IUserDocumentRepository UserDocumentRepo => _userDocumentRepo ??= new UserDocumentRepository(_context);

        private IUserTokenRepository? _userTokenRepo;
        public IUserTokenRepository UserTokenRepo => _userTokenRepo ??= new UserTokenRepository(_context);

        private IUserViolationRepository? _userViolationRepo;
        public IUserViolationRepository UserViolationRepo => _userViolationRepo ??= new UserViolationRepository(_context);

        private IVehicleDocumentRepository? _vehicleDocumentRepo;
        public IVehicleDocumentRepository VehicleDocumentRepo => _vehicleDocumentRepo ??= new VehicleDocumentRepository(_context);

        private IVehicleImageRepository? _vehicleImageRepo;
        public IVehicleImageRepository VehicleImageRepo => _vehicleImageRepo ??= new VehicleImageRepository(_context);

        private IVehicleRepository? _vehicleRepo;
        public IVehicleRepository VehicleRepo => _vehicleRepo ??= new VehicleRepository(_context);

        private IVehicleTypeRepository? _vehicleTypeRepo;
        public IVehicleTypeRepository VehicleTypeRepo => _vehicleTypeRepo ??= new VehicleTypeRepository(_context);

        private IWalletRepository? _walletRepo;
        public IWalletRepository WalletRepo => _walletRepo ??= new WalletRepository(_context);

        private IPostContactRepository? _postContactRepo;
        public IPostContactRepository PostContactRepo => _postContactRepo ??= new PostContactRepository(_context);

        private IDriverWorkSessionRepository? _driverWorkSessionRepo;
        public IDriverWorkSessionRepository DriverWorkSessionRepo => _driverWorkSessionRepo ??= new DriverWorkSessionRepository(_context);

        private IContactTokenRepository? _contactTokenRepo;
        public IContactTokenRepository ContactTokenRepo => _contactTokenRepo ??= new ContactTokenRepository(_context);

        private ITripVehicleHandoverTermResultRepository? _tripVehicleHandoverTermResultRepo;
        public ITripVehicleHandoverTermResultRepository TripVehicleHandoverTermResultRepo => _tripVehicleHandoverTermResultRepo ??= new TripVehicleHandoverTermResultRepository(_context);

        private ITripVehicleHandoverIssueRepository? _tripVehicleHandoverIssueRepo;
        public ITripVehicleHandoverIssueRepository TripVehicleHandoverIssueRepo => _tripVehicleHandoverIssueRepo ??= new TripVehicleHandoverIssueRepository(_context);

        private ITripVehicleHandoverRecordRepository? _tripVehicleHandoverRecordRepo;
        public ITripVehicleHandoverRecordRepository TripVehicleHandoverRecordRepo => _tripVehicleHandoverRecordRepo ??= new TripVehicleHandoverRecordRepository(_context);

        private ITripSurchargeRepository? _tripSurchargeRepo;
        public ITripSurchargeRepository TripSurchargeRepo => _tripSurchargeRepo ??= new TripSurchargeRepository(_context);

        private ITripVehicleHandoverIssueImageRepository? _tripVehicleHandoverIssueImageRepo;
        public ITripVehicleHandoverIssueImageRepository TripVehicleHandoverIssueImageRepo => _tripVehicleHandoverIssueImageRepo ??= new TripVehicleHandoverIssueImageRepository(_context);

        private IPackageHandlingDetailRepository? _packageHandlingDetailRepo;
        public IPackageHandlingDetailRepository PackageHandlingDetailRepo => _packageHandlingDetailRepo ??= new PackageHandlingDetailRepository(_context);

        // =======================================================================
        // TRANSACTION & SAVE (OPTIMIZED)
        // =======================================================================
        private IInspectionHistoryRepository? _inspectionHistoryRepo;
        public IInspectionHistoryRepository InspectionHistoryRepo => _inspectionHistoryRepo ??= new InspectionHistoryRepository(_context);

        private ITruckRestrictionRepository? _truckRestrictionRepo;
        public ITruckRestrictionRepository TruckRestrictionRepo => _truckRestrictionRepo ??= new TruckRestrictionRepository(_context);
        private IUserDeviceTokenRepository? _userDeviceTokenRepo;
        public IUserDeviceTokenRepository UserDeviceTokenRepo => _userDeviceTokenRepo ??= new UserDeviceTokenRepository(_context);
        private INotificationRepository? _notificationRepo;
        public INotificationRepository NotificationRepo => _notificationRepo ??= new NotificationRepository(_context);
        public async Task<int> SaveAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public async Task<bool> SaveChangeAsync()
        {
            return await SaveAsync() > 0;
        }

        public async Task<IDbContextTransaction> BeginTransactionAsync()
        {
            return await _context.Database.BeginTransactionAsync();
        }

        // =======================================================================
        // DISPOSE
        // =======================================================================
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _context.Dispose();
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}