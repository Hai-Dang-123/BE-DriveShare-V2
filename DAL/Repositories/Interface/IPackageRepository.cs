using DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Repositories.Interface
{
    public interface IPackageRepository : IGenericRepository<Package>
    {
        Task<IEnumerable<Package>> GetAllPackagesAsync();
        Task<Package?> GetPackageByIdAsync(Guid packageId);
        Task<IEnumerable<Package>> GetPackagesByUserIdAsync(Guid UserId);
        IQueryable<Package> GetPackagesByUserIdQueryable(Guid userId);
        IQueryable<Package> GetAllPackagesQueryable();
    }
}
