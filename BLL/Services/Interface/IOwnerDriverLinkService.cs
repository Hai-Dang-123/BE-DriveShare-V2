using Common.DTOs;
using Common.Enums.Status;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IOwnerDriverLinkService
    {
        Task<ResponseDTO> CreateOwnerDriverLinkAsync(CreateOwerDriverLinkDTO dto);
        Task<ResponseDTO> ChangeStatusAsync(ChangeStatusOwnerDriverLinkDTO dto);
    }
}
