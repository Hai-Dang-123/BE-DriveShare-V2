using DAL.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Repositories.Interface
{
    public interface IPackageImageRepository : IGenericRepository<PackageImage>
    {
        Task<IEnumerable<PackageImage>> GetAllByPackageIdAsync(Guid packageId);
      
    }
}
