using Common.Enums.Type;
using DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Repositories.Interface
{
    public interface IUserTokenRepository : IGenericRepository<UserToken>
    {
        Task<UserToken> GetRefreshTokenByUserID(Guid userId);
        Task<UserToken?> GetValidRefreshTokenWithUserAsync(string tokenValue);
        Task<UserToken?> GetByUserIdAndTokenValueAsync(Guid userId, string tokenValue, TokenType tokenType);
    }
}
