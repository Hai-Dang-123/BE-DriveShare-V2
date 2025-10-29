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
    public class PackageImageRepository : GenericRepository<PackageImage>, IPackageImageRepository
    {
        private readonly DriverShareAppContext _context;
        public PackageImageRepository (DriverShareAppContext context) : base (context)
        {
            _context = context;
        }

        public async Task<IEnumerable<PackageImage>> GetAllByPackageIdAsync(Guid packageId)
        {
            return await _context.PackageImages
                .Where(pi => pi.PackageId == packageId)
                .ToListAsync();
        }
    }
}
