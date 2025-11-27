using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using DAL.Entities;
using DAL.UnitOfWork;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
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
        private readonly IItemImagesService _itemImagesService;
        public ItemService(IUnitOfWork unitOfWork, UserUtility userUtility, IItemImagesService itemImagesService)
        {
            _userUtility = userUtility;
            _unitOfWork = unitOfWork;
            _itemImagesService = itemImagesService;
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
                    Quantity = item.Quantity,
                    Unit = item.Unit,
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
        //public async Task<ResponseDTO> ProviderCreateItemAsync(ItemCreateDTO itemCreateDTO)
        //{

        //    try
        //    {
        //        var UserId = _userUtility.GetUserIdFromToken();
        //        if (UserId == Guid.Empty)
        //        {
        //            return new ResponseDTO
        //            {
        //                IsSuccess = false,
        //                StatusCode = StatusCodes.Status401Unauthorized,
        //                Message = "Unauthorized User",
        //            };
        //        }
        //        var newItem = new Item
        //        {
        //            ItemId = Guid.NewGuid(),
        //            ProviderId = UserId,
        //            ItemName = itemCreateDTO.ItemName,
        //            Currency = itemCreateDTO.Currency,
        //            DeclaredValue = itemCreateDTO.DeclaredValue,
        //            Description = itemCreateDTO.Description,
        //            Status = ItemStatus.PENDING,
        //        };
        //        await _unitOfWork.ItemRepo.AddAsync(newItem);
        //        await _unitOfWork.SaveChangeAsync();
        //        return new ResponseDTO
        //        {
        //            IsSuccess = true,
        //            StatusCode = StatusCodes.Status201Created,
        //            Message = "Create New Item Succes",

        //        };
        //    }
        //    catch (Exception ex)
        //    {
        //        return new ResponseDTO
        //        {
        //            IsSuccess = false,
        //            StatusCode = StatusCodes.Status500InternalServerError,
        //            Message = "An error occurred while creating the item.",
        //            Result = ex.Message
        //        };
        //    }
        //}
        // Lấy hàng hóa theo User Id
        // Nhớ import: using Common.DTOs;
        // Nhớ import: using Microsoft.EntityFrameworkCore; (cho .CountAsync(), .ToListAsync())

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

                // 1. Tạo đối tượng Item
                var newItem = new Item
                {
                    ItemId = Guid.NewGuid(),
                    ProviderId = UserId, // Lấy từ token
                    ItemName = itemCreateDTO.ItemName,
                    Currency = itemCreateDTO.Currency,
                    DeclaredValue = itemCreateDTO.DeclaredValue,
                    Description = itemCreateDTO.Description,
                    Status = ItemStatus.PENDING,
                    Quantity = itemCreateDTO.Quantity,
                    Unit = itemCreateDTO.Unit,
                    // Lưu ý: DTO có 'Price' nhưng entity Item (cũ) của bạn không có
                    // Tôi sẽ bỏ qua 'Price' giống như code cũ của bạn
                };

                // 2. Thêm Item vào UnitOfWork
                await _unitOfWork.ItemRepo.AddAsync(newItem);

                // 3. (MỚI) Gọi ItemImageService để xử lý upload và thêm ảnh
                if (itemCreateDTO.ItemImages != null && itemCreateDTO.ItemImages.Any())
                {
                    // Truyền ItemId mới, UserId, và danh sách file
                    await _itemImagesService.AddImagesToItemAsync(newItem.ItemId, UserId, itemCreateDTO.ItemImages);
                }

                // 4. (RẤT QUAN TRỌNG) SaveChanges MỘT LẦN DUY NHẤT
                // Lệnh này sẽ lưu cả 'newItem' và tất cả 'itemImages' trong cùng 1 transaction
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status201Created,
                    Message = "Create New Item Success",
                    Result = new { ItemId = newItem.ItemId } // Trả về ID của item vừa tạo
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

        public async Task<ResponseDTO> GetItemsByUserIdAsync(int pageNumber, int pageSize)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                {
                    return new ResponseDTO
                    {
                        IsSuccess = false,
                        StatusCode = StatusCodes.Status401Unauthorized,
                        Message = "Unauthorized User",
                    };
                }

                // BƯỚC 1: Lấy IQueryable từ Repo (chưa gọi DB)
                var itemsQuery = _unitOfWork.ItemRepo.GetItemsByUserIdQueryable(userId);

                // ***** THAY ĐỔI: LỌC BỎ TRẠNG THÁI DELETED *****
                var filteredQuery = itemsQuery.Where(item => item.Status != ItemStatus.DELETED);
                // ***********************************************

                // BƯỚC 2: Đếm tổng số (dùng query đã lọc)
                var totalCount = await filteredQuery.CountAsync(); // <-- Dùng query đã lọc

                // BƯỚC 3: Lấy dữ liệu trang hiện tại (dùng query đã lọc)
                var itemDTOs = await filteredQuery // <-- Dùng query đã lọc
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(item => new ItemReadDTO
                    {
                        ItemId = item.ItemId,
                        ItemName = item.ItemName,
                        Currency = item.Currency,
                        DeclaredValue = item.DeclaredValue,
                        Description = item.Description,
                        OwnerId = item.OwnerId,
                        ProviderId = item.ProviderId,
                        Status = item.Status.ToString(),
                        Quantity = item.Quantity,
                        Unit = item.Unit,
                        ImageUrls = item.ItemImages.Select(pi => new ItemImageReadDTO
                        {
                            ItemImageId = pi.ItemImageId,
                            ItemId = pi.ItemId,
                            ImageUrl = pi.ItemImageURL
                        }).ToList() ?? new List<ItemImageReadDTO>()
                    })
                    .ToListAsync(); // (gọi DB - SELECT)

                // BƯN 4: Tạo đối tượng PaginatedDTO
                var paginatedResult = new PaginatedDTO<ItemReadDTO>(itemDTOs, totalCount, pageNumber, pageSize);

                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Items retrieved successfully",
                    Result = paginatedResult
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

        public async Task<ResponseDTO> GetAllItemsAsync(int pageNumber, int pageSize)
        {
            try
            {
                // BƯỚC 1: Lấy IQueryable
                var itemsQuery = _unitOfWork.ItemRepo.GetAllItemsQueryable();

                // BƯỚC 2: Đếm tổng số
                var totalCount = await itemsQuery.CountAsync();

                // BƯỚC 3: Lấy dữ liệu + Map DTO
                var itemDTOs = await itemsQuery
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(item => new ItemReadDTO
                    {
                        ItemId = item.ItemId,
                        ItemName = item.ItemName,
                        Currency = item.Currency,
                        DeclaredValue = item.DeclaredValue,
                        Description = item.Description,
                        OwnerId = item.OwnerId,
                        ProviderId = item.ProviderId,
                        Status = item.Status.ToString(),
                        ImageUrls = item.ItemImages.Select(pi => new ItemImageReadDTO
                        {
                            ItemImageId = pi.ItemImageId,
                            ItemId = pi.ItemId,
                            ImageUrl = pi.ItemImageURL
                        }).ToList() ?? new List<ItemImageReadDTO>()
                    })
                    .ToListAsync();

                // BƯỚC 4: Tạo PaginatedDTO
                var paginatedResult = new PaginatedDTO<ItemReadDTO>(itemDTOs, totalCount, pageNumber, pageSize);

                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Items retrieved successfully",
                    Result = paginatedResult
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

        public async Task<ResponseDTO> GetPendingItemsByUserIdAsync(int pageNumber, int pageSize)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                {
                    return new ResponseDTO
                    {
                        IsSuccess = false,
                        StatusCode = StatusCodes.Status401Unauthorized,
                        Message = "Unauthorized User",
                    };
                }

                // BƯỚC 1: Lấy IQueryable từ Repo (chưa gọi DB)
                var itemsQuery = _unitOfWork.ItemRepo.GetItemsByUserIdQueryable(userId);

                // ***** THAY ĐỔI DUY NHẤT *****
                // Thêm bộ lọc (filter) cho trạng thái PENDING
                var pendingItemsQuery = itemsQuery.Where(item => item.Status == ItemStatus.PENDING);
                // *******************************

                // BƯỚC 2: Đếm tổng số (sử dụng query đã lọc)
                var totalCount = await pendingItemsQuery.CountAsync();

                // BƯỚC 3: Lấy dữ liệu trang hiện tại (sử dụng query đã lọc)
                var itemDTOs = await pendingItemsQuery // <-- Dùng query đã lọc
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(item => new ItemReadDTO
                    {
                        ItemId = item.ItemId,
                        ItemName = item.ItemName,
                        Currency = item.Currency,
                        DeclaredValue = item.DeclaredValue,
                        Description = item.Description,
                        OwnerId = item.OwnerId,
                        ProviderId = item.ProviderId,
                        Status = item.Status.ToString(), // (Kết quả sẽ luôn là "PENDING")
                        ImageUrls = item.ItemImages.Select(pi => new ItemImageReadDTO
                        {
                            ItemImageId = pi.ItemImageId,
                            ItemId = pi.ItemId,
                            ImageUrl = pi.ItemImageURL
                        }).ToList() ?? new List<ItemImageReadDTO>()
                    })
                    .ToListAsync(); // (gọi DB - SELECT)

                // BƯỚC 4: Tạo đối tượng PaginatedDTO
                var paginatedResult = new PaginatedDTO<ItemReadDTO>(itemDTOs, totalCount, pageNumber, pageSize);

                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Pending items retrieved successfully", // Cập nhật message
                    Result = paginatedResult
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    IsSuccess = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = "An error occurred while retrieving pending items.", // Cập nhật message
                    Result = ex.Message
                };
            }
        }
    }
}
