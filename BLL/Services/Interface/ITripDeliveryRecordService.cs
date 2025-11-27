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
        Task<ResponseDTO> SignDeliveryRecordAsync(SignDeliveryRecordDTO dto);
        Task<ResponseDTO> SendOTPToSignDeliveryRecordAsync(Guid recordId);
        Task CreateTripDeliveryRecordAsync(TripDeliveryRecordCreateDTO dto, Guid driverId);

        Task<ResponseDTO> GetAllAsync(int pageNumber, int pageSize);
        Task<ResponseDTO> GetByIdForDriverAsync(Guid tripDeliveryRecordId);

        Task<ResponseDTO> GetDeliveryRecordForContactAsync(Guid recordId, string accessToken);
        Task<ResponseDTO> SendOTPToContactAsync(Guid recordId, string accessToken);
        Task<ResponseDTO> SignDeliveryRecordForContactAsync(Guid recordId, string otp, string accessToken);
        Task<ResponseDTO> SendAccessLinkToContactAsync(Guid recordId);
    }
        
        
}
