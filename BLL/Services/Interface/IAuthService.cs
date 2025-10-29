using Common.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IAuthService
    {
        Task<ResponseDTO> LoginAsync(LoginDTO dto);
        Task<ResponseDTO> LogoutAsync();
        //
        Task<ResponseDTO> RegisterForOwner (RegisterForOwnerDTO dto);
        Task<ResponseDTO> RegisterForDriver (RegisterForDriverDTO dto);
        Task<ResponseDTO> RegisterForProvider (RegisterForProviderDTO dto);
        //
        Task<ResponseDTO> RegisterForAdmin (RegisterForAdminDTO dto);
        //
        Task<ResponseDTO> RefreshTokenAsync(RefreshTokenDTO dto);

    }
}
