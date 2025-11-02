using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
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
    public class TripCompensationService : ITripCompensationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;
        public TripCompensationService(IUnitOfWork unitOfWork, UserUtility userUtility)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
        }

        public async Task<ResponseDTO> CreateTripCompensation(TripCompensationCreateDTO dto)
        {
            try
            {
                var UserId = _userUtility.GetUserIdFromToken();
                if (UserId == null)
                {
                    return new ResponseDTO
                    {
                        IsSuccess = false,
                        StatusCode = StatusCodes.Status401Unauthorized,
                        Message = "Unauthorized"
                    };
                }
                var tripCompensation = new TripCompensation
                {
                    TripCompensationId = Guid.NewGuid(),
                    RequesterId = UserId,
                    TripId = dto.TripId,
                    Amount = dto.Amount,
                    Reason = dto.Reason,
                    Status = CompensationStatus.REQUESTED,
                    CreatedAt = DateTime.UtcNow
                };
                await _unitOfWork.TripCompensationRepo.AddAsync(tripCompensation);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status201Created,
                    Message = "Trip compensation request created successfully"
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    IsSuccess = false,
                    Message = ex.Message
                };
            }
        }
    }
}
