using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Settings;
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
    public class TripDeliveryIssueImageService : ITripDeliveryIssueImageService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFirebaseUploadService _firebaseService;
        private readonly UserUtility _userUtility;
        public TripDeliveryIssueImageService(IUnitOfWork unitOfWork,IFirebaseUploadService firebaseUploadService, UserUtility userUtility)
        {
            _unitOfWork = unitOfWork;
            _firebaseService = firebaseUploadService;
            _userUtility = userUtility;
        }
        // CREATE TRIP DELIVERY ISSUE IMAGE
        public async Task<ResponseDTO> CreateTripDeliveryIssueImage(TripDeliveryIssueImageCreateDTO dto)
        {
            try
            {
                var UserId = _userUtility.GetUserIdFromToken();
                if (UserId == null)
                {
                    return new ResponseDTO
                    {
                        IsSuccess = false,
                        Message = "Unauthorized"
                    };
                }
                var imageURL = await _firebaseService.UploadFileAsync(dto.Files, UserId, FirebaseFileType.TRIPDELIVERY_ISSUE_IMAGES);
                var tripDeliveryIssueImage = new TripDeliveryIssueImage
                {
                    TripDeliveryIssueImageId = Guid.NewGuid(),
                    TripDeliveryIssueId = dto.TripDeliveryIssueId,
                    ImageUrl = imageURL,
                    Caption = dto.Caption,
                    CreatedAt = TimeUtil.NowVN(),
                };
                await _unitOfWork.TripDeliveryIssueImageRepo.AddAsync(tripDeliveryIssueImage);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status201Created,
                    Message = "Trip delivery issue image created successfully"
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
