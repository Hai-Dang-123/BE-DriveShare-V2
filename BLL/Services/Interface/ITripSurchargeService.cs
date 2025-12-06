using Common.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface ITripSurchargeService
    {
        Task<ResponseDTO> CreateSurchargeAsync(TripSurchargeCreateDTO dto);
        Task<ResponseDTO> GetSurchargesByTripIdAsync(Guid tripId);
        Task<ResponseDTO> UpdateStatusAsync(UpdateSurchargeStatusDTO dto);
        Task<ResponseDTO> CreateSurchargeForContactAsync(TripSurchargeCreateDTO dto, string accessToken);
    }
}
