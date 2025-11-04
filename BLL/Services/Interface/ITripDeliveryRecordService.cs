using Common.DTOs;
using DAL.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface ITripDeliveryRecordService
    {
        Task<ResponseDTO> CreateTripDeliveryRecordAsync(TripDeliveryRecordCreateDTO tripDeliveryRecordDTO);
        Task<ResponseDTO?> GetByIdAsync(Guid tripDeliveryRecordId);
        Task<ResponseDTO> GetByTripIdAsync(Guid tripId);
       Task <ResponseDTO> SignDeliveryRecordAsync(Guid tripDeliveryRecordId);
    }
}
