using Common.Enums.Status;
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
    public class PackageRepository : GenericRepository<Package>, IPackageRepository
    {
        private readonly DriverShareAppContext _context;
        public PackageRepository (DriverShareAppContext context) : base (context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Package>> GetAllPackagesAsync()
        {
            return await _context.Packages
                .Include(p => p.Item)
                .Include(p => p.PackageImages)
                .Where(p => p.Status != PackageStatus.Deleted)
                .ToListAsync();
        }
        public async Task<Package?> GetPackageByIdAsync(Guid packageId)
        {
            return await _context.Packages
                .Include(p => p.Item)
                .Include(p => p.PackageImages)
                .FirstOrDefaultAsync(p => p.PackageId == packageId);
        }
        public async Task<IEnumerable<Package>> GetPackagesByUserIdAsync(Guid UserId)
        {
            return await _context.Packages
                .Include(p => p.Item)
                .Include(p => p.PackageImages)
                .Where(p => p.OwnerId == UserId || p.ProviderId == UserId && p.Status != PackageStatus.Deleted)
                .ToListAsync();
        }
    }
}
