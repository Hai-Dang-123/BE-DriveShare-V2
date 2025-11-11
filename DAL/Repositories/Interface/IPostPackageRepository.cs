using DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Repositories.Interface
{
    public interface IPostPackageRepository : IGenericRepository<PostPackage>
    {
        IQueryable<PostPackage> GetAllQueryable();
        IQueryable<PostPackage> GetByProviderIdQueryable(Guid providerId);
        Task<PostPackage?> GetDetailsByIdAsync(Guid postPackageId);
    }
}
