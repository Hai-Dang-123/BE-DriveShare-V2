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
    public class PostPackageRepository : GenericRepository<PostPackage>, IPostPackageRepository
    {
        private readonly DriverShareAppContext _context;
        public PostPackageRepository (DriverShareAppContext context) : base (context)
        {
            _context = context;
        }

        // Giả sử bạn có DbContext là _context
        // và DbSet<PostPackage> là _context.PostPackages

        // (Helper riêng tư để tránh lặp code Include)
        private IQueryable<PostPackage> GetBaseQuery()
        {
            return _context.PostPackages
                .AsNoTracking()
                .Include(p => p.Provider)       // Lấy thông tin Provider (tên, avatar)
                .Include(p => p.ShippingRoute)  // Lấy thông tin Lộ trình (địa điểm)
                .Include(p => p.PostContacts)   // Lấy danh sách liên hệ
                .Include(p => p.Packages);      // Lấy các gói hàng để đếm
        }

        public IQueryable<PostPackage> GetAllQueryable()
        {
            return GetBaseQuery()
                .OrderByDescending(p => p.Created); // Luôn OrderBy khi phân trang
        }

        public IQueryable<PostPackage> GetByProviderIdQueryable(Guid providerId)
        {
            return GetBaseQuery()
                .Where(p => p.ProviderId == providerId)
                .Include (p => p.ShippingRoute)
                .OrderByDescending(p => p.Created);
        }

        // (Nhớ import: using Microsoft.EntityFrameworkCore;)

        public async Task<PostPackage?> GetDetailsByIdAsync(Guid postPackageId)
        {
            return await _context.PostPackages
                .AsNoTracking()
                .Where(p => p.PostPackageId == postPackageId)

                // Include tất cả các cấp
                .Include(p => p.Provider)           // Provider
                .Include(p => p.ShippingRoute)      // ShippingRoute
                .Include(p => p.PostContacts)       // PostContacts

                // Include lồng cấp 1: Packages
                .Include(p => p.Packages)
                    // Include lồng cấp 2: Item (từ Package)
                    .ThenInclude(pkg => pkg.Item)
                        // Include lồng cấp 3: ItemImages (từ Item)
                        .ThenInclude(item => item.ItemImages)

                // Phải Include(Packages) lại để ThenInclude tiếp
                .Include(p => p.Packages)
                    // Include lồng cấp 2: PackageImages (từ Package)
                    .ThenInclude(pkg => pkg.PackageImages)

                .FirstOrDefaultAsync();
        }
    }
}
