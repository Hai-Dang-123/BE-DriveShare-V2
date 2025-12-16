using DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Repositories.Interface
{
    public interface IUserDeviceTokenRepository : IGenericRepository<UserDeviceToken>
    {
        // Có thể thêm hàm tìm kiếm nhanh theo Token nếu cần
        Task<UserDeviceToken?> GetByTokenAsync(string token);
    }
}
