using BLL.Services.Interface;
using Common.DTOs;
using DAL.Entities;
using DAL.UnitOfWork;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class TripContactService : ITripContactService
    {
        private readonly IUnitOfWork _unitOfWork;
        public TripContactService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ResponseDTO> CreateTripContactAsync(TripContactCreateDTO tripContactDTO)
        {
            try
            {
                var tripContact = new  TripContact
                {
                    TripContactId = Guid.NewGuid(),
                    TripId = tripContactDTO.TripId,
                    Type = tripContactDTO.TripType,
                    FullName = tripContactDTO.Name,
                    PhoneNumber = tripContactDTO.PhoneNumber,
                    Email = tripContactDTO.Email,
                    Note = tripContactDTO.Notes
                };
                await _unitOfWork.TripContactRepo.AddAsync(tripContact);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status201Created,
                    Message = "Trip contact created successfully."
                };
            } catch (Exception ex)
            {
                return new ResponseDTO
                {
                    IsSuccess = false,
                    Message = $"An error occurred while creating the trip contact: {ex.Message}"
                };
            }
           
        }
    }
}
