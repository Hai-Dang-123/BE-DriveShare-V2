﻿using DAL.Context;
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
    public class ProviderRepository : GenericRepository<Provider>, IProviderRepository
    {
        private readonly DriverShareAppContext _context;
        public ProviderRepository (DriverShareAppContext context) : base (context)
        {
            _context = context;
        }

        public async Task<Provider?> GetProviderByTaxCodeAsync(string taxCode)
        {
            if (string.IsNullOrWhiteSpace(taxCode))
            {
                return null;
            }
            return await _context.Providers.FirstOrDefaultAsync(o => o.TaxCode == taxCode);
        }
    }
}
