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
                .Where(p => p.Status != PackageStatus.DELETED)
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
                .Where(p =>
                (p.OwnerId == UserId || p.ProviderId == UserId)
                && p.Status != PackageStatus.DELETED
                && p.TripId == null)
                .ToListAsync();
        }

        public IQueryable<Package> GetPackagesByUserIdQueryable(Guid userId)
        {
            // Rất quan trọng: Không .ToListAsync() hay await ở đây.
            // Chúng ta trả về chính đối tượng truy vấn.
            return _context.Packages
                .AsNoTracking() // Thêm AsNoTracking() để tăng hiệu suất cho truy vấn chỉ đọc
                .Where(p => p.ProviderId == userId || p.OwnerId == userId) // (Đây là logic tôi suy đoán, bạn hãy thay bằng logic nghiệp vụ của mình để lấy package theo user)
                .Include(p => p.Item)
                    .ThenInclude(i => i.ItemImages) // Include bảng con của Item
                .Include(p => p.PackageImages)
                .OrderByDescending(p => p.CreatedAt); // (Ví dụ: Sắp xếp theo ngày tạo, bạn nên thêm OrderBy)
        }

        public IQueryable<Package> GetAllPackagesQueryable()
        {
            // Tương tự, không .ToListAsync() hay await
            return _context.Packages
                .AsNoTracking()
                .Include(p => p.Item)
                    .ThenInclude(i => i.ItemImages)
                .Include(p => p.PackageImages)
                .OrderByDescending(p => p.CreatedAt); // (Nên có OrderBy)
        }
    }
}
