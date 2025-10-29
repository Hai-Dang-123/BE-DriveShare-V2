using DAL.Context;
using DAL.Entities;
using DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Repositories.Implement
{
    public class OwnerRepository : GenericRepository<Owner>, IOwnerRepository
    {
        private readonly DriverShareAppContext _context;
        public OwnerRepository (DriverShareAppContext context) : base (context)
        {
            _context = context;
        }

        public async Task<Owner?> GetOwnerByTaxCodeAsync(string taxCode)
        {
            if (string.IsNullOrWhiteSpace(taxCode))
            {
                return null;
            }
            return await _context.Owners.FirstOrDefaultAsync(o => o.TaxCode == taxCode);
        }
    }
}
