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
    public class TripDeliveryRecordRepository : GenericRepository<TripDeliveryRecord>, ITripDeliveryRecordRepository
    {
        private readonly DriverShareAppContext _context;
        public TripDeliveryRecordRepository (DriverShareAppContext context) : base (context)
        {
            _context = context;
        }

        public async Task<TripDeliveryRecord?> GetByIdWithDetailsAsync(Guid tripDeliveryRecordId)
        {
            return await _context.TripDeliveryRecords
                .Include(tdr => tdr.TripContact)

                .Include(tdr => tdr.DeliveryRecordTemplate)
                    .ThenInclude(tpl => tpl.DeliveryRecordTerms)
                .FirstOrDefaultAsync(tdr => tdr.DeliveryRecordId  == tripDeliveryRecordId);
        }

        public async Task<IEnumerable<TripDeliveryRecord>> GetByTripIdAsync(Guid tripId)
        {
            return await _context.TripDeliveryRecords
                .Include(tdr => tdr.TripContact)
                .Include(tdr => tdr.DeliveryRecordTemplate)
                    .ThenInclude(tpl => tpl.DeliveryRecordTerms)
                .Where(tdr => tdr.TripId == tripId)
                .ToListAsync();
        }

        public async Task<TripDeliveryRecord?> GetByIdWithDetailsForDriverAsync(Guid id)
        {
            return await _context.TripDeliveryRecords
                .AsNoTracking() // Tăng tốc độ đọc, không cần theo dõi thay đổi
        .AsSplitQuery() // <--- CHÌA KHÓA: Tách query để tránh Timeout
                .Include(r => r.TripContact) // Include người ký
                .Include(tdr => tdr.Driver)
                .Include(r => r.DeliveryRecordTemplate) // Include template
                    .ThenInclude(t => t.DeliveryRecordTerms) // Include điều khoản
                .Include(r => r.Trip) // Include chuyến đi
                    .ThenInclude(t => t.Packages) // Include gói hàng
                        .ThenInclude(p => p.Item) // Include chi tiết sản phẩm
                            .ThenInclude(i => i.ItemImages) // Include ảnh sản phẩm (nếu cần).
                 .Include(r => r.Trip)
                    .ThenInclude(t => t.Packages)
                        .ThenInclude(p => p.PackageImages) // Include ảnh gói hàng (nếu cần)
                .Include(r => r.Issues) // Include các vấn đề phát sinh
                    .ThenInclude(i => i.DeliveryIssueImages) // Include ảnh của các vấn đề phát sinh
                .Include(r => r.Issues)
                    .ThenInclude(i => i.Surcharges) // Include thông tin tài xế báo cáo vấn đề
                .FirstOrDefaultAsync(r => r.DeliveryRecordId == id || r.DeliveryRecordId == id); // Tùy tên khóa chính của bạn
        }
    }
}
