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
    }
}
