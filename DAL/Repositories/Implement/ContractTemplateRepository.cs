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
    public class ContractTemplateRepository : GenericRepository<ContractTemplate>, IContractTemplateRepository
    {
        private readonly DriverShareAppContext _context;
        public ContractTemplateRepository (DriverShareAppContext context) : base (context)
        {
            _context = context;
        }

    }

}
