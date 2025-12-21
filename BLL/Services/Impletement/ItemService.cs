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

        // =============================================================================
        // 1. DELETE ITEM
        // =============================================================================
        public async Task<ResponseDTO> DeleteItemAsync(Guid itemId)
        {
            try
            {
                var item = await _unitOfWork.ItemRepo.GetByIdAsync(itemId);
                if (item == null)
                {
                    return new ResponseDTO("Item not found", StatusCodes.Status404NotFound, false);
                }

                item.Status = ItemStatus.DELETED;
                await _unitOfWork.ItemRepo.UpdateAsync(item);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Item deleted successfully", StatusCodes.Status200OK, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"An error occurred: {ex.Message}", StatusCodes.Status500InternalServerError, false);
            }
        }

        // =============================================================================
        // 2. GET ITEM BY ID (Optimized)
        // =============================================================================
        public async Task<ResponseDTO> GetItemByIdAsync(Guid itemId)
        {
            try
            {
                // Dùng Projection trực tiếp để tối ưu thay vì lấy entity rồi map
                var itemDto = await _unitOfWork.ItemRepo.GetAll()
                    .AsNoTracking()
                    .Where(i => i.ItemId == itemId)
                    .Select(i => new ItemReadDTO
                    {
                        ItemId = i.ItemId,
                        ItemName = i.ItemName,
                        Currency = i.Currency,
                        DeclaredValue = i.DeclaredValue,
                        Description = i.Description,
                        OwnerId = i.OwnerId,
                        ProviderId = i.ProviderId,
                        Status = i.Status.ToString(),
                        Quantity = i.Quantity,
                        Unit = i.Unit,
                        ImageUrls = i.ItemImages.Select(pi => new ItemImageReadDTO
                        {
                            ItemImageId = pi.ItemImageId,
                            ItemId = pi.ItemId,
                            ImageUrl = pi.ItemImageURL
                        }).ToList()
                    })
                    .FirstOrDefaultAsync();

                if (itemDto == null)
                {
                    return new ResponseDTO("Item not found", StatusCodes.Status404NotFound, false);
                }

                return new ResponseDTO("Item retrieved successfully", StatusCodes.Status200OK, true, itemDto);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error: {ex.Message}", StatusCodes.Status500InternalServerError, false);
            }
        }

        // =============================================================================
        // 3. UPDATE ITEM
        // =============================================================================
        public async Task<ResponseDTO> UpdateItemAsync(ItemUpdateDTO itemUpdateDTO)
        {
            try
            {
                var item = await _unitOfWork.ItemRepo.GetByIdAsync(itemUpdateDTO.ItemId);
                if (item == null)
                {
                    return new ResponseDTO("Item not found", StatusCodes.Status404NotFound, false);
                }

                item.ItemName = itemUpdateDTO.ItemName;
                item.Currency = itemUpdateDTO.Currency;
                item.DeclaredValue = itemUpdateDTO.DeclaredValue;
                item.Description = itemUpdateDTO.Description;
                item.Quantity = itemUpdateDTO.Quantity;
                // Nếu Entity Item có trường Unit, nhớ update thêm ở đây: item.Unit = ...

                await _unitOfWork.ItemRepo.UpdateAsync(item);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Item updated successfully", StatusCodes.Status200OK, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error: {ex.Message}", StatusCodes.Status500InternalServerError, false);
            }
        }

        // =============================================================================
        // 4. OWNER CREATE ITEM
        // =============================================================================
        public async Task<ResponseDTO> OwnerCreateItemAsync(ItemCreateDTO itemCreateDTO)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync(); // Dùng Transaction cho an toàn
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty) return new ResponseDTO("Unauthorized User", StatusCodes.Status401Unauthorized, false);

                var newItem = new Item
                {
                    ItemId = Guid.NewGuid(),
                    OwnerId = userId,
                    ItemName = itemCreateDTO.ItemName,
                    Currency = itemCreateDTO.Currency,
                    DeclaredValue = itemCreateDTO.DeclaredValue,
                    Description = itemCreateDTO.Description,
                    Status = ItemStatus.PENDING,
                    // Map thêm các trường nếu có trong entity
                    Quantity = 1, // Default hoặc lấy từ DTO nếu có
                    Unit = "Kiện" // Default
                };

                await _unitOfWork.ItemRepo.AddAsync(newItem);

                // Xử lý ảnh
                if (itemCreateDTO.ItemImages != null && itemCreateDTO.ItemImages.Any())
                {
                    await _itemImagesService.AddImagesToItemAsync(newItem.ItemId, userId, itemCreateDTO.ItemImages);
                }

                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync();

                return new ResponseDTO("Create New Item Success", StatusCodes.Status201Created, true, new { ItemId = newItem.ItemId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO($"Error: {ex.Message}", StatusCodes.Status500InternalServerError, false);
            }
        }

        // =============================================================================
        // 5. PROVIDER CREATE ITEM
        // =============================================================================
        public async Task<ResponseDTO> ProviderCreateItemAsync(ItemCreateDTO itemCreateDTO)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty) return new ResponseDTO("Unauthorized User", StatusCodes.Status401Unauthorized, false);

                var newItem = new Item
                {
                    ItemId = Guid.NewGuid(),
                    ProviderId = userId, // Lưu ProviderId
                    ItemName = itemCreateDTO.ItemName,
                    Currency = itemCreateDTO.Currency,
                    DeclaredValue = itemCreateDTO.DeclaredValue,
                    Description = itemCreateDTO.Description,
                    Status = ItemStatus.PENDING,
                    Quantity = itemCreateDTO.Quantity,
                    Unit = itemCreateDTO.Unit,
                };

                await _unitOfWork.ItemRepo.AddAsync(newItem);

                if (itemCreateDTO.ItemImages != null && itemCreateDTO.ItemImages.Any())
                {
                    await _itemImagesService.AddImagesToItemAsync(newItem.ItemId, userId, itemCreateDTO.ItemImages);
                }

                await _unitOfWork.SaveChangeAsync();
                await transaction.CommitAsync();

                return new ResponseDTO("Create New Item Success", StatusCodes.Status201Created, true, new { ItemId = newItem.ItemId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new ResponseDTO($"Error: {ex.Message}", StatusCodes.Status500InternalServerError, false);
            }
        }

        // =============================================================================
        // 6. GET ITEMS BY USER ID (Optimized Projection)
        // =============================================================================
        public async Task<ResponseDTO> GetItemsByUserIdAsync(int pageNumber, int pageSize, string? search, string? sortBy, string? sortOrder)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty) return new ResponseDTO("Unauthorized User", 401, false);

                // 1. Base Query
                var query = _unitOfWork.ItemRepo.GetItemsByUserIdQueryable(userId)
                    .AsNoTracking()
                    .Where(item => item.Status != ItemStatus.DELETED);

                // 2. Search
                if (!string.IsNullOrWhiteSpace(search))
                {
                    string k = search.Trim().ToLower();
                    query = query.Where(x => x.ItemName.ToLower().Contains(k) ||
                                             (x.Description != null && x.Description.ToLower().Contains(k)));
                }

                // 3. Sort
                query = ApplySorting(query, sortBy, sortOrder);

                // 4. Count
                var totalCount = await query.CountAsync();

                // 5. [OPTIMIZED] Projection (Select) trực tiếp
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
                        // Map list ảnh ngay trong SQL select
                        ImageUrls = item.ItemImages.Select(img => new ItemImageReadDTO
                        {
                            ItemImageId = img.ItemImageId,
                            ItemId = img.ItemId,
                            ImageUrl = img.ItemImageURL
                        }).ToList()
                    })
                    .ToListAsync(); // Execute query here

                return new ResponseDTO("Items retrieved successfully", 200, true, new PaginatedDTO<ItemReadDTO>(items, totalCount, pageNumber, pageSize));
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error: {ex.Message}", 500, false);
            }
        }

        // =============================================================================
        // 7. GET ALL ITEMS (Optimized Projection)
        // =============================================================================
        public async Task<ResponseDTO> GetAllItemsAsync(int pageNumber, int pageSize, string? search, string? sortBy, string? sortOrder)
        {
            try
            {
                var query = _unitOfWork.ItemRepo.GetAllItemsQueryable()
                    .AsNoTracking()
                    .Where(item => item.Status != ItemStatus.DELETED);

                if (!string.IsNullOrWhiteSpace(search))
                {
                    string k = search.Trim().ToLower();
                    query = query.Where(x => x.ItemName.ToLower().Contains(k) ||
                                             (x.Description != null && x.Description.ToLower().Contains(k)));
                }

                query = ApplySorting(query, sortBy, sortOrder);

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
        // 8. GET PENDING ITEMS BY USER ID (Optimized Projection)
        // =============================================================================
        public async Task<ResponseDTO> GetPendingItemsByUserIdAsync(int pageNumber, int pageSize, string? search, string? sortBy, string? sortOrder)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty) return new ResponseDTO("Unauthorized User", 401, false);

                var query = _unitOfWork.ItemRepo.GetItemsByUserIdQueryable(userId)
                    .AsNoTracking()
                    .Where(item => item.Status == ItemStatus.PENDING);

                if (!string.IsNullOrWhiteSpace(search))
                {
                    string k = search.Trim().ToLower();
                    query = query.Where(x => x.ItemName.ToLower().Contains(k) ||
                                             (x.Description != null && x.Description.ToLower().Contains(k)));
                }

                query = ApplySorting(query, sortBy, sortOrder);

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
                        ImageUrls = item.ItemImages.Select(img => new ItemImageReadDTO
                        {
                            ItemImageId = img.ItemImageId,
                            ItemId = img.ItemId,
                            ImageUrl = img.ItemImageURL
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
        // PRIVATE HELPER: SORTING
        // =============================================================================
        private IQueryable<Item> ApplySorting(IQueryable<Item> query, string? sortBy, string? sortOrder)
        {
            bool desc = sortOrder?.ToUpper() == "DESC";
            sortBy = sortBy?.ToLower();

            return sortBy switch
            {
                "itemname" => desc ? query.OrderByDescending(x => x.ItemName) : query.OrderBy(x => x.ItemName),
                "declaredvalue" => desc ? query.OrderByDescending(x => x.DeclaredValue) : query.OrderBy(x => x.DeclaredValue),
                "status" => desc ? query.OrderByDescending(x => x.Status) : query.OrderBy(x => x.Status),
                _ => query.OrderBy(x => x.ItemName) // Default sort
            };
        }
    }
}