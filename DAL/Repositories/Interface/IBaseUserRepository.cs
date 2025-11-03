using DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.Repositories.Interface
{
    public interface IBaseUserRepository : IGenericRepository<BaseUser>
    {
        Task<BaseUser> FindByEmailAsync(string email);
        Task<BaseUser?> FindByEmailAndRoleAsync(string email, string roleName);
        Task<BaseUser> FindByPhoneNumberAsync(string phoneNumber);
    }
}
