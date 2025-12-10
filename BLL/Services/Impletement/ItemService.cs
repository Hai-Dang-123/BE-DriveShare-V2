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
                item.Quantity = itemUpdateDTO.Quantity;

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

        // =============================================================================
        // 1. GET ITEMS BY USER ID (Của tôi - Trừ Deleted)
        // =============================================================================
        public async Task<ResponseDTO> GetItemsByUserIdAsync(int pageNumber, int pageSize, string? search, string? sortBy, string? sortOrder)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty) return new ResponseDTO("Unauthorized User", 401, false);

                // 1. Base Query
                var query = _unitOfWork.ItemRepo.GetItemsByUserIdQueryable(userId)
                    .AsNoTracking() // Tối ưu đọc
                    .Where(item => item.Status != ItemStatus.DELETED); // Lọc bỏ Deleted

                // 2. Search
                if (!string.IsNullOrWhiteSpace(search))
                {
                    string k = search.Trim().ToLower();
                    query = query.Where(x => x.ItemName.ToLower().Contains(k) ||
                                             (x.Description != null && x.Description.ToLower().Contains(k)));
                }

                // 3. Sort
                query = ApplySorting(query, sortBy, sortOrder);

                // 4. Paging & Map
                var totalCount = await query.CountAsync();
                var items = await query
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
                        }).ToList()
                    })
                    .ToListAsync();

                return new ResponseDTO("Items retrieved successfully", 200, true, new PaginatedDTO<ItemReadDTO>(items, totalCount, pageNumber, pageSize));
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error: {ex.Message}", 500, false);
            }
        }

        // =============================================================================
        // 2. GET ALL ITEMS (Admin/Public)
        // =============================================================================
        public async Task<ResponseDTO> GetAllItemsAsync(int pageNumber, int pageSize, string? search, string? sortBy, string? sortOrder)
        {
            try
            {
                // 1. Base Query
                var query = _unitOfWork.ItemRepo.GetAllItemsQueryable()
                    .AsNoTracking()
                    .Where(item => item.Status != ItemStatus.DELETED);

                // 2. Search
                if (!string.IsNullOrWhiteSpace(search))
                {
                    string k = search.Trim().ToLower();
                    query = query.Where(x => x.ItemName.ToLower().Contains(k) ||
                                             (x.Description != null && x.Description.ToLower().Contains(k)) ||
                                             (x.OwnerId != null && x.OwnerId.ToString().ToLower().Contains(k)));
                }

                // 3. Sort
                query = ApplySorting(query, sortBy, sortOrder);

                // 4. Paging & Map
                var totalCount = await query.CountAsync();
                var items = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(item => new ItemReadDTO
                    {
                        ItemId = item.ItemId,
                        ItemName = item.ItemName,
                        Currency = item.Currency,
                        DeclaredValue = item.DeclaredValue,
                        Description = item.Description,
                        Quantity = item.Quantity,
                        Unit = item.Unit,
                        OwnerId = item.OwnerId,
                        ProviderId = item.ProviderId,
                        Status = item.Status.ToString(),
                        ImageUrls = item.ItemImages.Select(img => new ItemImageReadDTO
                        {
                            ItemImageId = img.ItemImageId,
                            ItemId = img.ItemId,
                            ImageUrl = img.ItemImageURL
                        }).ToList()
                    })
                    .ToListAsync();

                return new ResponseDTO("Items retrieved successfully", 200, true, new PaginatedDTO<ItemReadDTO>(items, totalCount, pageNumber, pageSize));
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error: {ex.Message}", 500, false);
            }
        }

        // =============================================================================
        // 3. GET PENDING ITEMS BY USER ID (Của tôi - Chỉ Pending)
        // =============================================================================
        public async Task<ResponseDTO> GetPendingItemsByUserIdAsync(int pageNumber, int pageSize, string? search, string? sortBy, string? sortOrder)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty) return new ResponseDTO("Unauthorized User", 401, false);

                // 1. Base Query
                var query = _unitOfWork.ItemRepo.GetItemsByUserIdQueryable(userId)
                    .AsNoTracking()
                    .Where(item => item.Status == ItemStatus.PENDING); // Chỉ lấy Pending

                // 2. Search
                if (!string.IsNullOrWhiteSpace(search))
                {
                    string k = search.Trim().ToLower();
                    query = query.Where(x => x.ItemName.ToLower().Contains(k) ||
                                             (x.Description != null && x.Description.ToLower().Contains(k)));
                }

                // 3. Sort
                query = ApplySorting(query, sortBy, sortOrder);

                // 4. Paging & Map
                var totalCount = await query.CountAsync();
                var items = await query
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
                        }).ToList()
                    })
                    .ToListAsync();

                return new ResponseDTO("Pending items retrieved successfully", 200, true, new PaginatedDTO<ItemReadDTO>(items, totalCount, pageNumber, pageSize));
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error: {ex.Message}", 500, false);
            }
        }

        // =============================================================================
        // PRIVATE HELPER: SORTING (Tái sử dụng cho cả 3 hàm)
        // =============================================================================
        private IQueryable<DAL.Entities.Item> ApplySorting(IQueryable<DAL.Entities.Item> query, string? sortBy, string? sortOrder)
        {
            bool desc = sortOrder?.ToUpper() == "DESC";
            sortBy = sortBy?.ToLower();

            return sortBy switch
            {
                "itemname" => desc ? query.OrderByDescending(x => x.ItemName) : query.OrderBy(x => x.ItemName),
                "declaredvalue" => desc ? query.OrderByDescending(x => x.DeclaredValue) : query.OrderBy(x => x.DeclaredValue),
                "status" => desc ? query.OrderByDescending(x => x.Status) : query.OrderBy(x => x.Status),
                // "createdat" => ... (Nếu Item có trường CreatedAt thì thêm vào đây)
                _ => query.OrderBy(x => x.ItemName) // Default sort
            };
        }
    }
}
