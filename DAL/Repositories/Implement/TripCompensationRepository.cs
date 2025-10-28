using DAL.Context;
using DAL.Entities;
using DAL.Repositories.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Repositories.Implement
{
    public class TripCompensationRepository : GenericRepository<TripCompensation>, ITripCompensationRepository
    {
        private readonly DriverShareAppContext _context;
        public TripCompensationRepository (DriverShareAppContext context) : base (context)
        {
            _context = context;
        }
    }
}
