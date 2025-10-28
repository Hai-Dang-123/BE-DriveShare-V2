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
    public class UserTokenRepository : GenericRepository<UserToken>, IUserTokenRepository
    {
        private readonly DriverShareAppContext _context;
        public UserTokenRepository (DriverShareAppContext context) : base (context)
        {
            _context = context;
        }

        public async Task<UserToken> GetRefreshTokenByUserID(Guid userId)
        {
            // lấy token đúng id và chưa bị thu hồi
            return await _context.UserTokens
                .Where(rt => rt.UserTokenId == userId && !rt.IsRevoked)
                .FirstOrDefaultAsync();
        }
    }
}
