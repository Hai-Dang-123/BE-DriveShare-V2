using DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Repositories.Interface
{
    public interface IOwnerRepository : IGenericRepository<Owner>
    {
        Task<Owner?> GetOwnerByTaxCodeAsync(string taxCode);

        Task<Owner?> GetOwnerProfileAsync(Guid userId);
    }
}
