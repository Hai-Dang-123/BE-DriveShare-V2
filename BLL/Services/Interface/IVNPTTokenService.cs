using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IVNPTTokenService
    {
        Task<string> GetAccessTokenAsync();
        Task<(string TokenKey, string TokenId)> GetServiceTokensAsync(string channelCode);
    }
}
