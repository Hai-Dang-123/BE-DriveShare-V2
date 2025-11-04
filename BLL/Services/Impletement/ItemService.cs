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
    public class ItemService : IItemService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;
        public ItemService(IUnitOfWork unitOfWork, UserUtility userUtility)
        {
            _userUtility = userUtility;
            _unitOfWork = unitOfWork;
        }

        // Get By item ID
        public async Task<ResponseDTO> DeleteItemAsync(Guid itemId)
        {
            try
            {
                var item =  await _unitOfWork.ItemRepo.GetByIdAsync(itemId);
                if (item == null)
                {
                    return new ResponseDTO
                    {
                        IsSuccess = false,
                        StatusCode = StatusCodes.Status404NotFound,
                        Message = "Item not found",
                    };
                }
                item.Status = ItemStatus.DELETED;
                await _unitOfWork.ItemRepo.UpdateAsync(item);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Item deleted successfully",
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    IsSuccess = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = "An error occurred while deleting the item.",
                    Result = ex.Message
                };
            }

        }
        // Get ALL hàng hóa
        public async Task<ResponseDTO> GetAllItemsAsync()
        {
            try
            {
                var items = await _unitOfWork.ItemRepo.GetAllItemsAsync();
                var itemDTOs = items.Select(item => new ItemReadDTO
                {
                    ItemId = item.ItemId,
                    ItemName = item.ItemName,
                    Currency = item.Currency,
                    DeclaredValue = item.DeclaredValue,
                    Description = item.Description,
                    OwnerId = item.OwnerId,
                    ProviderId = item.ProviderId,
                    Status = item.Status.ToString(),
                    //ImageUrls = item.ItemImages.Select(img => img.ItemImageURL).ToList()
                    ImageUrls = item.ItemImages?.Select(pi => new ItemImageReadDTO
                    {
                       ItemImageId = pi.ItemImageId,
                       ItemId = pi.ItemId,
                       ImageUrl = pi.ItemImageURL
                    }).ToList() ?? new List<ItemImageReadDTO>()
                }).ToList();
                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Items retrieved successfully",
                    Result = itemDTOs
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    IsSuccess = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = "An error occurred while retrieving items.",
                    Result = ex.Message
                };
            }
         }
        // Lấy hàng hóa theo Id
        public async Task<ResponseDTO> GetItemByIdAsync(Guid itemId)
        {
            try
            {
                var item = await _unitOfWork.ItemRepo.GetItemByIdAsync(itemId);
                if (item == null)
                {
                    return new ResponseDTO
                    {
                        IsSuccess = false,
                        StatusCode = StatusCodes.Status404NotFound,
                        Message = "Item not found",
                    };
                }
                var itemDTO = new ItemReadDTO
                {
                    ItemId = item.ItemId,
                    ItemName = item.ItemName,
                    Currency = item.Currency,
                    DeclaredValue = item.DeclaredValue,
                    Description = item.Description,
                    OwnerId = item.OwnerId,
                    ProviderId = item.ProviderId,
                    Status = item.Status.ToString(),
                    ImageUrls = item.ItemImages?.Select(pi => new ItemImageReadDTO
                    {
                        ItemImageId = pi.ItemImageId,
                        ItemId = pi.ItemId,
                        ImageUrl = pi.ItemImageURL
                    }).ToList() ?? new List<ItemImageReadDTO>()
                };
                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Item retrieved successfully",
                    Result = itemDTO
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    IsSuccess = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = "An error occurred while retrieving the item.",
                    Result = ex.Message
                };

            }
        }
        // Cập nhật hàng hóa
        public async Task<ResponseDTO> UpdateItemAsync(ItemUpdateDTO itemUpdateDTO)
        {
            try
            {
                var item = await _unitOfWork.ItemRepo.GetByIdAsync(itemUpdateDTO.ItemId);
                if (item == null)
                {
                    return new ResponseDTO
                    {
                        IsSuccess = false,
                        StatusCode = StatusCodes.Status404NotFound,
                        Message = "Item not found",
                    };
                }
                item.ItemName = itemUpdateDTO.ItemName;
                item.Currency = itemUpdateDTO.Currency;
                item.DeclaredValue = itemUpdateDTO.DeclaredValue;
                item.Description = itemUpdateDTO.Description;

                await _unitOfWork.ItemRepo.UpdateAsync(item);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Item updated successfully",
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    IsSuccess = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = "An error occurred while updating the item.",
                    Result = ex.Message
                };
            }
        }
        // Owner tạo hàng hóa
        public  async Task<ResponseDTO> OwnerCreateItemAsync(ItemCreateDTO itemCreateDTO)
        {
            try
            {
                var UserId = _userUtility.GetUserIdFromToken();
                if (UserId == Guid.Empty)
                {
                    return new ResponseDTO
                    {
                        IsSuccess = false,
                        StatusCode = StatusCodes.Status401Unauthorized,
                        Message = "Unauthorized User",
                    };
                }
                var newItem = new Item
                {
                    ItemId = Guid.NewGuid(),
                    OwnerId = UserId,
                    ItemName = itemCreateDTO.ItemName,
                    Currency = itemCreateDTO.Currency,
                    DeclaredValue = itemCreateDTO.DeclaredValue,
                    Description = itemCreateDTO.Description,
                    Status = ItemStatus.PENDING,
                };
                await _unitOfWork.ItemRepo.AddAsync(newItem);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status201Created,
                    Message = "Create New Item Succes",

                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    IsSuccess = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = "An error occurred while creating the item.",
                    Result = ex.Message
                };
            }
        }
        // Provider tạo hàng hóa
        public async Task<ResponseDTO> ProviderCreateItemAsync(ItemCreateDTO itemCreateDTO)
        {
           
            try
            {
                var UserId = _userUtility.GetUserIdFromToken();
                if (UserId == Guid.Empty)
                {
                    return new ResponseDTO
                    {
                        IsSuccess = false,
                        StatusCode = StatusCodes.Status401Unauthorized,
                        Message = "Unauthorized User",
                    };
                }
                var newItem = new Item
                {
                    ItemId = Guid.NewGuid(),
                    ProviderId = UserId,
                    ItemName = itemCreateDTO.ItemName,
                    Currency = itemCreateDTO.Currency,
                    DeclaredValue = itemCreateDTO.DeclaredValue,
                    Description = itemCreateDTO.Description,
                    Status = ItemStatus.PENDING,
                };
                await _unitOfWork.ItemRepo.AddAsync(newItem);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status201Created,
                    Message = "Create New Item Succes",

                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    IsSuccess = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = "An error occurred while creating the item.",
                    Result = ex.Message
                };
            }
        }
        // Lấy hàng hóa theo User Id
        public Task<ResponseDTO> GetItemsByOwnerIdAsync(Guid UserId)
        {
            try
            {
                var items = _unitOfWork.ItemRepo.GetItemsByUserIdAsync(UserId);

                var itemDTOs = items.Result.Select(item => new ItemReadDTO
                {
                    ItemId = item.ItemId,
                    ItemName = item.ItemName,
                    Currency = item.Currency,
                    DeclaredValue = item.DeclaredValue,
                    Description = item.Description,
                    OwnerId = item.OwnerId,
                    ProviderId = item.ProviderId,
                    Status = item.Status.ToString(),
                    ImageUrls = item.ItemImages?.Select(pi => new ItemImageReadDTO
                    {
                        ItemImageId = pi.ItemImageId,
                        ItemId = pi.ItemId,
                        ImageUrl = pi.ItemImageURL
                    }).ToList() ?? new List<ItemImageReadDTO>()
                }).ToList();
                return Task.FromResult(new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Items retrieved successfully",
                    Result = itemDTOs
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new ResponseDTO
                {
                    IsSuccess = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = "An error occurred while retrieving items.",
                    Result = ex.Message
                });
            }
        }
    }
}
