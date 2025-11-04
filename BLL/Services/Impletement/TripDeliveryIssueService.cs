using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.Enums.Type;
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
    public  class TripDeliveryIssueService : ITripDeliveryIssueService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;
        public TripDeliveryIssueService(IUnitOfWork unitOfWork,UserUtility userUtility)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
        }

        public async Task<ResponseDTO> CreateTripDeliveryIssue(TripDeliveryIssueCreateDTO dto)
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
                var tripDeliveryIssue = new TripDeliveryIssue
                {
                    TripDeliveryIssueId = Guid.NewGuid(),
                    TripId = dto.TripId,
                    DeliveryRecordId = dto.DeliveryRecordId,
                    Description = dto.Description,
                    IssueType = dto.IssueType,
                    Status = IssueStatus.REPORTED,
                    CreatedAt = DateTime.UtcNow
                };
                await _unitOfWork.TripDeliveryIssueRepo.AddAsync(tripDeliveryIssue);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status201Created,
                    Message = "Trip delivery issue created successfully"
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
