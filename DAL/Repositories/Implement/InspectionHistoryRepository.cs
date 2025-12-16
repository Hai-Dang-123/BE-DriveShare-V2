using DAL.Context;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Repositories.Implement
{
    public class InspectionHistoryRepository : GenericRepository<DAL.Entities.InspectionHistory>, Interface.IInspectionHistoryRepository
    {
        private readonly DriverShareAppContext _context;
        public InspectionHistoryRepository(DriverShareAppContext context) : base(context)
        {
            _context = context;
        }
    }
}
