using DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Repositories.Interface
{
    public interface ITripDeliveryRecordRepository : IGenericRepository<TripDeliveryRecord>
    {
        Task<IEnumerable<TripDeliveryRecord>> GetByTripIdAsync(Guid tripId);
        Task<TripDeliveryRecord?> GetByIdWithDetailsAsync(Guid tripDeliveryRecordId);

    }
}
