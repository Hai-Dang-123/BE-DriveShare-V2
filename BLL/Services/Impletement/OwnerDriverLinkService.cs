﻿using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status; // Đảm bảo using Enum này
using DAL.Entities;
using DAL.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class OwnerDriverLinkService : IOwnerDriverLinkService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;

        public OwnerDriverLinkService(IUnitOfWork unitOfWork, UserUtility userUtility)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
        }

        public async Task<ResponseDTO> ChangeStatusAsync(ChangeStatusOwnerDriverLinkDTO dto)
        {
            var currentUserId = _userUtility.GetUserIdFromToken();
            if (currentUserId == Guid.Empty)
            {
                return new ResponseDTO("User is not authenticated.", 401, false);
            }

            var link = await _unitOfWork.OwnerDriverLinkRepo.GetByIdAsync(dto.OwnerDriverLinkId);
            if (link == null)
            {
                // Nên dùng 404 Not Found
                return new ResponseDTO($"Link with ID {dto.OwnerDriverLinkId} not found.", 404, false);
            }


            if (currentUserId != link.OwnerId)
            {
                return new ResponseDTO("You are not authorized to change the status of this link.", 403, false);
            }
            // === KẾT THÚC SỬA LỖI ===

            if (link.Status != FleetJoinStatus.PENDING)
            {
                return new ResponseDTO("Only PENDING links can have their status changed.", 400, false);
            }

            if (dto.Status != FleetJoinStatus.APPROVED && dto.Status != FleetJoinStatus.REJECTED)
            {
                return new ResponseDTO("Invalid target status. Must be APPROVED or REJECTED.", 400, false);
            }


            link.Status = dto.Status;
            if (dto.Status == FleetJoinStatus.APPROVED)
            {
                link.ApprovedAt = DateTime.UtcNow; 
            }
            else 
            {
                link.ApprovedAt = null;
            }


            try
            {

                await _unitOfWork.OwnerDriverLinkRepo.UpdateAsync(link); 
                await _unitOfWork.SaveChangeAsync(); 
                return new ResponseDTO($"Link status changed to {dto.Status} successfully.", 200, true);
            }
            catch (Exception ex)
            {
                // TODO: Log lỗi chi tiết (ex.Message)
                Console.WriteLine($"Error changing OwnerDriverLink status: {ex.Message}");
                return new ResponseDTO("An error occurred while updating the link status.", 500, false);
            }
        }

        public async Task<ResponseDTO> CreateOwnerDriverLinkAsync(CreateOwerDriverLinkDTO dto)
        {
            var currentDriverId = _userUtility.GetUserIdFromToken();
            if (currentDriverId == Guid.Empty)
            {
                return new ResponseDTO("User is not authenticated.", 401, false);
            }

            // 2. Kiểm tra xem người gửi request có đúng là Driver không (tùy chọn)
            var currentUser = await _unitOfWork.BaseUserRepo.GetByIdAsync(currentDriverId);
            // Cần Include Role nếu bạn kiểm tra RoleName
            //var currentUser = await _unitOfWork.BaseUserRepo.FirstOrDefaultAsync(u => u.UserId == currentDriverId, "Role");
            //if (currentUser == null || currentUser.Role.RoleName != "Driver")
            //{
            //    return new ResponseDTO("Only drivers can send join requests.", 403, false);
            //}


            var owner = await _unitOfWork.OwnerRepo.GetByIdAsync(dto.OwnerId);
            if (owner == null)
            {
                // Nên dùng 404 Not Found
                return new ResponseDTO($"Owner with ID {dto.OwnerId} not found.", 404, false);
            }


            bool linkExists = await _unitOfWork.OwnerDriverLinkRepo.CheckLinkExistsAsync(currentDriverId, dto.OwnerId);
            if (linkExists)
            {
                // Nên dùng 409 Conflict
                return new ResponseDTO("A pending or approved link already exists between this driver and owner.", 409, false);
            }

            var newOwnerDriverLink = new OwnerDriverLink
            {
                OwnerDriverLinkId = Guid.NewGuid(),
                Status = FleetJoinStatus.PENDING, 
                RequestedAt = DateTime.UtcNow,
                ApprovedAt = null,
                OwnerId = dto.OwnerId,
                DriverId = currentDriverId,
            };

            try
            {
                await _unitOfWork.OwnerDriverLinkRepo.AddAsync(newOwnerDriverLink);
                await _unitOfWork.SaveChangeAsync();


                return new ResponseDTO("Join request sent successfully.", 201, true, new { LinkId = newOwnerDriverLink.OwnerDriverLinkId }); // Trả về ID nếu cần
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating OwnerDriverLink: {ex.Message}");
                return new ResponseDTO("An error occurred while creating the link.", 500, false);
            }
        }
    }
}