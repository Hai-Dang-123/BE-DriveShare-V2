using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.Settings;
using DAL.Entities;
using DAL.UnitOfWork;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace BLL.Services.Impletement
{
    public class ItemImagesService : IItemImagesService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFirebaseUploadService _firebaseUploadService;
        private readonly UserUtility _userUtility;
        public ItemImagesService(IUnitOfWork unitOfWork, IFirebaseUploadService firebaseUploadService, UserUtility userUtility)
        {
            _unitOfWork = unitOfWork;
            _firebaseUploadService = firebaseUploadService;
            _userUtility = userUtility;
        }
        // Create Item Image
        public async Task<ResponseDTO> CreateItemImageAsync(ItemImageCreateDTO itemImageDTO)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                {
                    return new ResponseDTO
                    {
                        Result = false,
                        StatusCode = StatusCodes.Status401Unauthorized,
                        Message = "Unauthorized"
                    };
                }
                var item = await _unitOfWork.ItemRepo.GetByIdAsync(itemImageDTO.ItemId);
                if (item == null)
                {
                    return new ResponseDTO
                    {
                        IsSuccess = false,
                        StatusCode = StatusCodes.Status404NotFound,
                        Message = "Item not found"
                    };
                }
                var imageUrl = await _firebaseUploadService.UploadFileAsync(itemImageDTO.File, userId, FirebaseFileType.ITEM_IMAGES);
                var itemImage = new ItemImage
                {
                    ItemImageId = Guid.NewGuid(),
                    ItemId = itemImageDTO.ItemId,
                    ItemImageURL = imageUrl,
                    Status = ItemImageStatus.Active
                };
                await _unitOfWork.ItemImageRepo.AddAsync(itemImage);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO
                {
                    Result = true,
                    StatusCode = StatusCodes.Status201Created,
                    Message = "Item image created successfully",
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    Result = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = ex.Message
                };
            }
        }
        // Delete Item Image
        public async Task<ResponseDTO> DeleteItemImageAsync(Guid itemImageId)
        {
            try
            {
                var itemImage = await _unitOfWork.ItemImageRepo.GetByIdAsync(itemImageId);
                if (itemImage == null)
                {
                    return new ResponseDTO
                    {
                        Result = false,
                        StatusCode = StatusCodes.Status404NotFound,
                        Message = "Item image not found"
                    };
                }
                itemImage.Status = ItemImageStatus.Deleted;
                await _unitOfWork.ItemImageRepo.UpdateAsync(itemImage);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO
                {
                    Result = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Item image deleted successfully"
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    Result = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = ex.Message
                };
            }
        }
        // Get All Item Images By Item Id
        public async Task<ResponseDTO> GetALlItemImagesByItemIdAsync(Guid itemId)
        {
            try
            {
                var item = await _unitOfWork.ItemRepo.GetByIdAsync(itemId);
                if (item == null)
                {
                    return new ResponseDTO
                    {
                        IsSuccess = false,
                        StatusCode = StatusCodes.Status404NotFound,
                        Message = "Item not found"
                    };
                }

                var images = await _unitOfWork.ItemImageRepo.GetAllByItemIdAsync(itemId);

                if (!images.Any())
                {
                    return new ResponseDTO
                    {
                        IsSuccess = true,
                        StatusCode = StatusCodes.Status200OK,
                        Message = "No images found for this item",
                        Result = new List<ItemImageDTO>()
                    };
                }

                var result = images.Select(i => new ItemImageReadDTO
                {
                    ItemImageId = i.ItemImageId,
                    ItemId = i.ItemId,
                    ImageUrl = i.ItemImageURL
                });

                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Item images retrieved successfully",
                    Result = result
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    IsSuccess = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = ex.Message
                };
            }
        }
        // Get Item Image By Id
        public async Task<ResponseDTO> GetItemImageByIdAsync(Guid itemImageId)
        {
            try
            {
                var itemImage = await _unitOfWork.ItemImageRepo.GetByIdAsync(itemImageId);
                if (itemImage == null || itemImage.Status == ItemImageStatus.Deleted)
                {
                    return new ResponseDTO
                    {
                        Result = false,
                        StatusCode = StatusCodes.Status404NotFound,
                        Message = "Item image not found"
                    };
                }
                var itemImageDTO = new ItemImageReadDTO
                {
                    ItemImageId = itemImage.ItemImageId,
                    ImageUrl = itemImage.ItemImageURL,
                    ItemId = itemImage.ItemId
                };
                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Item image retrieved successfully",
                    Result = itemImage
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    Result = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = ex.Message
                };
            }
        }
        // Update Item Image
        public async Task<ResponseDTO> UpdateItemImageAsync(UpdateItemImageDTO updateItemImageDTO)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                { 
                  return new ResponseDTO
                    {
                        Result = false,
                        StatusCode = StatusCodes.Status401Unauthorized,
                        Message = "Unauthorized"
                    };
                }
                var itemImage = _unitOfWork.ItemImageRepo.GetById(updateItemImageDTO.ItemImageId);
                if(itemImage == null || itemImage.Status == ItemImageStatus.Deleted)
                {
                    return new ResponseDTO
                    {
                        Result = false,
                        StatusCode = StatusCodes.Status404NotFound,
                        Message = "Item image not found"
                    };
                }
                if (updateItemImageDTO.File != null)
                {
                    var imageUrl = await _firebaseUploadService.UploadFileAsync(updateItemImageDTO.File, userId, FirebaseFileType.ITEM_IMAGES);

                    itemImage.ItemImageURL = imageUrl;
                }
                itemImage.ItemId = updateItemImageDTO.ItemId;
                await _unitOfWork.ItemImageRepo.UpdateAsync(itemImage);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO
                {
                    Result = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Item image updated successfully"
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    Result = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = ex.Message
                };
            }
        }


        //

        public async Task AddImagesToItemAsync(Guid itemId, Guid userId, List<IFormFile> files)
        {
            if (files == null || !files.Any())
            {
                return; // Không có gì để upload
            }

            // Tối ưu: Upload song song tất cả các ảnh
            var uploadTasks = new List<Task<(string Url, IFormFile File)>>();
            foreach (var file in files)
            {
                if (file != null && file.Length > 0)
                {
                    // Thêm task upload vào danh sách
                    uploadTasks.Add(UploadFileAndReturnUrlAsync(file, userId));
                }
            }

            // Chờ tất cả các file upload xong
            var uploadResults = await Task.WhenAll(uploadTasks);

            // Tạo các đối tượng ItemImage và thêm vào Repo
            foreach (var result in uploadResults)
            {
                var itemImage = new ItemImage
                {
                    ItemImageId = Guid.NewGuid(),
                    ItemId = itemId,
                    ItemImageURL = result.Url,
                    Status = ItemImageStatus.Active
                };

                // Chỉ AddAsync, KHÔNG SaveChangeAsync
                await _unitOfWork.ItemImageRepo.AddAsync(itemImage);
            }
        }

        /// <summary>
        /// Hàm helper riêng tư để bọc logic upload
        /// </summary>
        private async Task<(string Url, IFormFile File)> UploadFileAndReturnUrlAsync(IFormFile file, Guid userId)
        {
            var imageUrl = await _firebaseUploadService.UploadFileAsync(file, userId, FirebaseFileType.ITEM_IMAGES);
            return (imageUrl, file);
        }
    }
}