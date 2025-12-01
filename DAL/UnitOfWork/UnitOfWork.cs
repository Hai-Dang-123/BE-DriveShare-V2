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
        private IDbContextTransaction? _transaction;



        public UnitOfWork(DriverShareAppContext context)
        {
            _context = context;
            BaseContractRepo = new BaseContractRepository(context);

            BaseUserRepo = new BaseUserRepository(context); // Giả sử bạn có class BaseUserRepository
            ContractTemplateRepo = new ContractTemplateRepository(context);
            ContractTermRepo = new ContractTermRepository(context);
            DeliveryRecordRepo = new DeliveryRecordRepository(context);
            DeliveryRecordTemplateRepo = new DeliveryRecordTemplateRepository(context);
            DeliveryRecordTermRepo = new DeliveryRecordTermRepository(context);
            DriverActivityLogRepo = new DriverActivityLogRepository(context);
            DriverRepo = new DriverRepository(context); // Giả sử bạn có class DriverRepository
            ItemImageRepo = new ItemImageRepository(context);
            ItemRepo = new ItemRepository(context);
            OwnerDriverLinkRepo = new OwnerDriverLinkRepository(context);
            OwnerRepo = new OwnerRepository(context); // Giả sử bạn có class OwnerRepository
            PackageImageRepo = new PackageImageRepository(context);
            PackageRepo = new PackageRepository(context);
            PostPackageRepo = new PostPackageRepository(context);
            PostTripRepo = new PostTripRepository(context);
            ProviderRepo = new ProviderRepository(context); // Giả sử bạn có class ProviderRepository
            RoleRepo = new RoleRepository(context);
            ShippingRouteRepo = new ShippingRouteRepository(context);
            TransactionRepo = new TransactionRepository(context);
            TripCompensationRepo = new TripCompensationRepository(context);
            TripContactRepo = new TripContactRepository(context);
            // Lưu ý tên class TripDeliveryIssueImageRepository có thể sai, kiểm tra lại nhé
            TripDeliveryIssueImageRepo = new TripDeliveryIssueImageRepository(context);
            TripDeliveryIssueRepo = new TripDeliveryIssueRepository(context);
            TripDeliveryRecordRepo = new TripDeliveryRecordRepository(context);
            TripDriverAssignmentRepo = new TripDriverAssignmentRepository(context);
            TripDriverContractRepo = new TripDriverContractRepository(context);
            TripProviderContractRepo = new TripProviderContractRepository(context);
            TripRepo = new TripRepository(context);
            TripRouteRepo = new TripRouteRepository(context);
            TripRouteSuggestionRepo = new TripRouteSuggestionRepository(context);
            UserDocumentRepo = new UserDocumentRepository(context);
            UserTokenRepo = new UserTokenRepository(context);
            UserViolationRepo = new UserViolationRepository(context);
            VehicleDocumentRepo = new VehicleDocumentRepository(context);
            VehicleImageRepo = new VehicleImageRepository(context);
            VehicleRepo = new VehicleRepository(context);
            VehicleTypeRepo = new VehicleTypeRepository(context);
            WalletRepo = new WalletRepository(context);
            PostContactRepo = new PostContactRepository(context);

            DriverWorkSessionRepo = new DriverWorkSessionRepository(context);
            ContactTokenRepo = new ContactTokenRepository(context);
            TripVehicleHandoverTermResultRepo = new TripVehicleHandoverTermResultRepository(context);
            TripVehicleHandoverIssueRepo = new TripVehicleHandoverIssueRepository(context);
            TripVehicleHandoverRecordRepo = new TripVehicleHandoverRecordRepository(context);
            TripSurchargeRepo = new TripSurchargeRepository(context);
            TripVehicleHandoverIssueImageRepo = new TripVehicleHandoverIssueImageRepository(context);


        }

        public IBaseContractRepository BaseContractRepo { get; private set; }
        public IBaseUserRepository BaseUserRepo { get; private set; }
        public IContractTemplateRepository ContractTemplateRepo { get; private set; }
        public IContractTermRepository ContractTermRepo { get; private set; }
        public IDeliveryRecordRepository DeliveryRecordRepo { get; private set; }
        public IDeliveryRecordTemplateRepository DeliveryRecordTemplateRepo { get; private set; }
        public IDeliveryRecordTermRepository DeliveryRecordTermRepo { get; private set; }
        public IDriverActivityLogRepository DriverActivityLogRepo { get; private set; }
        public IDriverRepository DriverRepo { get; private set; }
        public IItemImageRepository ItemImageRepo { get; private set; }
        public IItemRepository ItemRepo { get; private set; }
        public IOwnerDriverLinkRepository OwnerDriverLinkRepo { get; private set; }
        public IOwnerRepository OwnerRepo { get; private set; }
        public IPackageImageRepository PackageImageRepo { get; private set; }
        public IPackageRepository PackageRepo { get; private set; }
        public IPostPackageRepository PostPackageRepo { get; private set; }
        public IPostTripRepository PostTripRepo { get; private set; }
        public IProviderRepository ProviderRepo { get; private set; }
        public IRoleRepository RoleRepo { get; private set; }
        public IShippingRouteRepository ShippingRouteRepo { get; private set; }
        public ITransactionRepository TransactionRepo { get; private set; }
        public ITripCompensationRepository TripCompensationRepo { get; private set; }
        public ITripContactRepository TripContactRepo { get; private set; }
        public ITripDeliveryIssueImageRepository TripDeliveryIssueImageRepo { get; private set; }
        public ITripDeliveryIssueRepository TripDeliveryIssueRepo { get; private set; }
        public ITripDeliveryRecordRepository TripDeliveryRecordRepo { get; private set; }
        public ITripDriverAssignmentRepository TripDriverAssignmentRepo { get; private set; }
        public ITripDriverContractRepository TripDriverContractRepo { get; private set; }
        public ITripProviderContractRepository TripProviderContractRepo { get; private set; }
        public ITripRepository TripRepo { get; private set; }
        public ITripRouteRepository TripRouteRepo { get; private set; }
        public ITripRouteSuggestionRepository TripRouteSuggestionRepo { get; private set; }
        public IUserDocumentRepository UserDocumentRepo { get; private set; }
        public IUserTokenRepository UserTokenRepo { get; private set; }
        public IUserViolationRepository UserViolationRepo { get; private set; }
        public IVehicleDocumentRepository VehicleDocumentRepo { get; private set; }
        public IVehicleImageRepository VehicleImageRepo { get; private set; }
        public IVehicleRepository VehicleRepo { get; private set; }
        public IVehicleTypeRepository VehicleTypeRepo { get; private set; }
        public IWalletRepository WalletRepo { get; private set; }

        public IPostContactRepository PostContactRepo { get; private set; }
        public IDriverWorkSessionRepository DriverWorkSessionRepo { get; private set; } 
        public IContactTokenRepository ContactTokenRepo { get; private set; }
        public ITripVehicleHandoverTermResultRepository TripVehicleHandoverTermResultRepo { get; private set; }
        public ITripVehicleHandoverIssueRepository TripVehicleHandoverIssueRepo { get; private set; }
        public ITripVehicleHandoverRecordRepository TripVehicleHandoverRecordRepo { get; private set; }
        public ITripSurchargeRepository TripSurchargeRepo { get; private set; }
        public ITripVehicleHandoverIssueImageRepository TripVehicleHandoverIssueImageRepo { get; private set; }




        // Transaction methods
        public async Task BeginTransactionAsync()
        {
            _transaction = await _context.Database.BeginTransactionAsync();
        }


        public async Task CommitTransactionAsync()
        {
            if (_transaction != null)
            {
                try
                {
                    await _context.SaveChangesAsync();
                    await _transaction.CommitAsync();
                }
                catch
                {
                    await RollbackTransactionAsync();
                    throw;
                }
                finally
                {
                    await _transaction.DisposeAsync();
                }
            }
        }


        //public IBookingRepository BookingRepo { get; private set; } 
       

        public async Task RollbackTransactionAsync()
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync();
                await _transaction.DisposeAsync();
            }
        }

        // Save changes

        public async Task<int> SaveAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public async Task<bool> SaveChangeAsync()
        {
            return await SaveAsync() > 0;
        }

        // Dispose pattern
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _transaction?.Dispose();
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