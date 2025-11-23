using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.ValueObjects;
using DAL.Entities;
using DAL.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class VehicleService : IVehicleService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;
        private readonly IVehicleImageService _vehicleImageService;
        private readonly IVehicleDocumentService _vehicleDocumentService;

        public VehicleService(IUnitOfWork unitOfWork, UserUtility userUtility, IVehicleImageService vehicleImageService, IVehicleDocumentService vehicleDocumentService)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
            _vehicleImageService = vehicleImageService;
            _vehicleDocumentService = vehicleDocumentService;
        }

        // CREATE
        public async Task<ResponseDTO> CreateAsync(VehicleCreateDTO dto)
        {
            try
            {
                var ownerId = _userUtility.GetUserIdFromToken();
                if (ownerId == Guid.Empty)
                    return new ResponseDTO("Unauthorized or invalid token", 401, false);

                // (Nếu CurrentAddress là string, bạn cần Geocode nó ở đây)
                 //Location currentAddress = await _vietMapService.GeocodeAsync(dto.CurrentAddressString);

                // 1. Tạo Vehicle (Đối tượng chính)
                var vehicle = new Vehicle
                {
                    VehicleId = Guid.NewGuid(),
                    OwnerId = ownerId,
                    VehicleTypeId = dto.VehicleTypeId,
                    PlateNumber = dto.PlateNumber,
                    Model = dto.Model,
                    Brand = dto.Brand,
                    YearOfManufacture = dto.YearOfManufacture,
                    Color = dto.Color,
                    PayloadInKg = dto.PayloadInKg,
                    VolumeInM3 = dto.VolumeInM3,
                    Features = dto.Features ?? new(),
                    CurrentAddress = dto.CurrentAddress, // (Gán Location đã Geocode nếu cần)
                    Status = VehicleStatus.ACTIVE,
                    CreatedAt = DateTime.UtcNow
                };
                await _unitOfWork.VehicleRepo.AddAsync(vehicle);

                // 2. Thêm hình ảnh (Gọi Service 1)
                await _vehicleImageService.AddImagesToVehicleAsync(vehicle.VehicleId, ownerId, dto.VehicleImages);

                // 3. Thêm giấy tờ (Gọi Service 2)
                await _vehicleDocumentService.AddDocumentsToVehicleAsync(vehicle.VehicleId, ownerId, dto.Documents);

                // 4. LƯU TẤT CẢ (1 Transaction duy nhất)
                await _unitOfWork.SaveChangeAsync();

                // 5. Map kết quả trả về
                var result = new VehicleDTO // (Map từ 'vehicle' entity)
                {
                    VehicleId = vehicle.VehicleId,
                    PlateNumber = vehicle.PlateNumber,
                    Model = vehicle.Model,
                    Brand = vehicle.Brand,
                    YearOfManufacture = vehicle.YearOfManufacture,
                    Color = vehicle.Color,
                    PayloadInKg = vehicle.PayloadInKg,
                    VolumeInM3 = vehicle.VolumeInM3,
                    Status = vehicle.Status
                };

                // Dùng 201 Created
                return new ResponseDTO("Create Vehicle Successfully !!!", 201, true, result);
            }
            catch (Exception ex)
            {
                return new ResponseDTO("Error while creating Vehicle", 500, false, ex.Message);
            }
        }

        // UPDATE
        public async Task<ResponseDTO> UpdateAsync(VehicleUpdateDTO dto)
        {
            try
            {
                var vehicle = await _unitOfWork.VehicleRepo.GetByIdAsync(dto.VehicleId);
                if (vehicle == null)
                    return new ResponseDTO("Vehicle not found", 404, false);

                vehicle.Model = dto.Model;
                vehicle.Brand = dto.Brand;
                vehicle.Color = dto.Color;
                vehicle.YearOfManufacture = dto.YearOfManufacture;
                vehicle.PayloadInKg = dto.PayloadInKg;
                vehicle.VolumeInM3 = dto.VolumeInM3;
                vehicle.Features = dto.Features ?? new();
                vehicle.CurrentAddress = dto.CurrentAddress;
                vehicle.VehicleTypeId = dto.VehicleTypeId;

                await _unitOfWork.VehicleRepo.UpdateAsync(vehicle);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Vehicle updated successfully", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO("Error while updating Vehicle", 500, false, ex.Message);
            }
        }

        // SOFT DELETE
        public async Task<ResponseDTO> SoftDeleteAsync(Guid id)
        {
            try
            {
                var vehicle = await _unitOfWork.VehicleRepo.GetByIdAsync(id);
                if (vehicle == null)
                    return new ResponseDTO("Vehicle not found", 404, false);

                vehicle.Status = VehicleStatus.DELETED;
                await _unitOfWork.VehicleRepo.UpdateAsync(vehicle);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Vehicle soft-deleted successfully", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO("Error while deleting Vehicle", 500, false, ex.Message);
            }
        }

        // GET ALL
        public async Task<ResponseDTO> GetAllAsync(
      int pageNumber = 1,
      int pageSize = 10,
      string? search = null,
      string? sortBy = null,
      string? sortOrder = "ASC")
        {
            try
            {
                // ========= 1) BASE QUERY =========
                IQueryable<Vehicle> baseQuery = _unitOfWork.VehicleRepo.GetAll()
                    .Where(v => v.Status != VehicleStatus.DELETED)
                    .AsNoTracking()
                    .Include(v => v.Owner)
                    .Include(v => v.VehicleType)
                    .Include(v => v.VehicleImages)
                    .Include(v => v.VehicleDocuments);

                // ========= 2) SEARCH =========
                if (!string.IsNullOrWhiteSpace(search))
                {
                    string s = search.Trim().ToLower();

                    baseQuery = baseQuery.Where(v =>
                        v.PlateNumber.ToLower().Contains(s) ||
                        v.Brand.ToLower().Contains(s) ||
                        v.Model.ToLower().Contains(s) ||
                        v.Owner.FullName.ToLower().Contains(s)
                    );
                }

                // ========= 3) SORTING =========
                sortBy = (sortBy ?? "").Trim().ToLower();
                sortOrder = (sortOrder ?? "ASC").Trim().ToUpper();
                bool desc = sortOrder == "DESC";

                baseQuery = sortBy switch
                {
                    "brand" => desc ? baseQuery.OrderByDescending(v => v.Brand) : baseQuery.OrderBy(v => v.Brand),
                    "model" => desc ? baseQuery.OrderByDescending(v => v.Model) : baseQuery.OrderBy(v => v.Model),
                    "year" => desc ? baseQuery.OrderByDescending(v => v.YearOfManufacture) : baseQuery.OrderBy(v => v.YearOfManufacture),
                    "payload" => desc ? baseQuery.OrderByDescending(v => v.PayloadInKg) : baseQuery.OrderBy(v => v.PayloadInKg),
                    "volume" => desc ? baseQuery.OrderByDescending(v => v.VolumeInM3) : baseQuery.OrderBy(v => v.VolumeInM3),
                    "createdat" => desc ? baseQuery.OrderByDescending(v => v.CreatedAt) : baseQuery.OrderBy(v => v.CreatedAt),
                    _ => baseQuery.OrderByDescending(v => v.CreatedAt) // default
                };

                // ========= 4) PAGINATION =========
                int totalCount = await baseQuery.CountAsync();

                var vehicles = await baseQuery
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(v => new VehicleDetailDTO
                    {
                        VehicleId = v.VehicleId,
                        PlateNumber = v.PlateNumber,
                        Model = v.Model,
                        Brand = v.Brand,
                        Color = v.Color,
                        YearOfManufacture = v.YearOfManufacture,
                        PayloadInKg = v.PayloadInKg,
                        VolumeInM3 = v.VolumeInM3,
                        Status = v.Status,

                        // VEHICLE TYPE
                        VehicleType = new VehicleTypeDTO
                        {
                            VehicleTypeId = v.VehicleType.VehicleTypeId,
                            VehicleTypeName = v.VehicleType.VehicleTypeName,
                            Description = v.VehicleType.Description
                        },

                        // OWNER
                        Owner = new GetDetailOwnerDTO
                        {
                            UserId = v.Owner.UserId,
                            FullName = v.Owner.FullName,
                            CompanyName = v.Owner.CompanyName
                        },

                        // IMAGES
                        ImageUrls = v.VehicleImages
                            .Select(img => new VehicleImageDetailDTO
                            {
                                VehicleImageId = img.VehicleImageId,
                                VehicleId = img.VehicleId,
                                ImageURL = img.ImageURL,
                                Caption = img.Caption,
                                CreatedAt = img.CreatedAt
                            })
                            .ToList(),

                        // DOCUMENTS
                        Documents = v.VehicleDocuments
                            .Select(d => new VehicleDocumentDetailDTO
                            {
                                VehicleDocumentId = d.VehicleDocumentId,
                                DocumentType = d.DocumentType,
                                FrontDocumentUrl = d.FrontDocumentUrl,
                                BackDocumentUrl = d.BackDocumentUrl,
                                ExpirationDate = d.ExpirationDate,
                                Status = d.Status,
                                AdminNotes = d.AdminNotes,
                                CreatedAt = d.CreatedAt,
                                ProcessedAt = d.ProcessedAt
                            })
                            .ToList()
                    })
                    .ToListAsync();

                var paginated = new PaginatedDTO<VehicleDetailDTO>(
                    vehicles, totalCount, pageNumber, pageSize);

                return new ResponseDTO("Success", 200, true, paginated);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error getting vehicles: {ex.Message}", 500, false);
            }
        }




        // GET BY ID
        public async Task<ResponseDTO> GetByIdAsync(Guid id)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                    return new ResponseDTO("Unauthorized or invalid token", 401, false);

                var vehicles = await _unitOfWork.VehicleRepo.GetAllAsync(
                    filter: v => v.VehicleId == id && v.Status != VehicleStatus.DELETED,
                    includeProperties: "Owner,VehicleType,VehicleImages,VehicleDocuments"
                );

                var vehicle = vehicles.FirstOrDefault();
                if (vehicle == null)
                    return new ResponseDTO("Vehicle not found", 404, false);

                if (vehicle.OwnerId != userId)
                    return new ResponseDTO("Forbidden: You are not the owner of this vehicle", 403, false);

                var dto = new VehicleDetailDTO
                {
                    VehicleId = vehicle.VehicleId,
                    PlateNumber = vehicle.PlateNumber,
                    Model = vehicle.Model,
                    Brand = vehicle.Brand,
                    Color = vehicle.Color,
                    YearOfManufacture = vehicle.YearOfManufacture,
                    PayloadInKg = vehicle.PayloadInKg,
                    VolumeInM3 = vehicle.VolumeInM3,
                    Status = vehicle.Status,

                    // 🧩 Thông tin loại xe
                    VehicleType = new VehicleTypeDTO
                    {
                        VehicleTypeId = vehicle.VehicleType.VehicleTypeId,
                        VehicleTypeName = vehicle.VehicleType.VehicleTypeName,
                        Description = vehicle.VehicleType.Description
                    },

                    // 🧩 Thông tin chủ xe
                    Owner = new GetDetailOwnerDTO
                    {
                        UserId = vehicle.Owner.UserId,
                        FullName = vehicle.Owner.FullName,
                        Email = vehicle.Owner.Email,
                        PhoneNumber = vehicle.Owner.PhoneNumber,
                        CreatedAt = vehicle.Owner.CreatedAt,
                        LastUpdatedAt = vehicle.Owner.LastUpdatedAt,
                        Status = vehicle.Owner.Status,
                        DateOfBirth = vehicle.Owner.DateOfBirth,
                        AvatarUrl = vehicle.Owner.AvatarUrl,
                        IsEmailVerified = vehicle.Owner.IsEmailVerified,
                        IsPhoneVerified = vehicle.Owner.IsPhoneVerified,
                        Address = vehicle.Owner.Address,
                        TaxCode = vehicle.Owner.TaxCode,
                        BusinessAddress = vehicle.Owner.BusinessAddress,
                        CompanyName = vehicle.Owner.CompanyName,
                        AverageRating = vehicle.Owner.AverageRating
                    },

                    // 🧩 Hình ảnh xe
                    ImageUrls = vehicle.VehicleImages.Select(vi => new VehicleImageDetailDTO
                    {
                        VehicleImageId = vi.VehicleImageId,
                        ImageURL = vi.ImageURL,
                        Caption = vi.Caption,
                        CreatedAt = vi.CreatedAt
                    }).ToList(),

                    // 🧩 Giấy tờ xe
                    Documents = vehicle.VehicleDocuments.Select(doc => new VehicleDocumentDetailDTO
                    {
                        VehicleDocumentId = doc.VehicleDocumentId,
                        DocumentType = doc.DocumentType,
                        FrontDocumentUrl = doc.FrontDocumentUrl,
                        BackDocumentUrl = doc.BackDocumentUrl,
                        ExpirationDate = doc.ExpirationDate,
                        Status = doc.Status,
                        AdminNotes = doc.AdminNotes,
                        CreatedAt = doc.CreatedAt,
                        ProcessedAt = doc.ProcessedAt
                    }).ToList()
                };

                return new ResponseDTO("Get vehicle successfully", 200, true, dto);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error while fetching vehicle: {ex.Message}", 500, false);
            }
        }


        // (Thêm vào cuối lớp VehicleService của bạn)
        // (Nhớ import: using Microsoft.EntityFrameworkCore;)

        public async Task<ResponseDTO> GetMyVehiclesAsync(int pageNumber, int pageSize)
        {
            try
            {
                var ownerId = _userUtility.GetUserIdFromToken();
                if (ownerId == Guid.Empty)
                    return new ResponseDTO("Unauthorized or invalid token", 401, false);

                // 1. Lấy IQueryable từ Repo
                var query = _unitOfWork.VehicleRepo.GetVehiclesByOwnerIdQueryable(ownerId)
                                 .Where(v => v.Status != VehicleStatus.DELETED); // Lọc bỏ xe đã xóa

                // 2. Đếm tổng số
                var totalCount = await query.CountAsync();

                // 3. Lấy dữ liệu của trang
                var vehicles = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // 4. Map sang DTO (Dùng lại DTO từ hàm GetAll/GetById của bạn)
                var result = vehicles.Select(v => new VehicleDetailDTO
                {
                    VehicleId = v.VehicleId,
                    PlateNumber = v.PlateNumber,
                    Model = v.Model,
                    Brand = v.Brand,
                    Color = v.Color,
                    YearOfManufacture = v.YearOfManufacture,
                    PayloadInKg = v.PayloadInKg,
                    VolumeInM3 = v.VolumeInM3,
                    Status = v.Status,
                    VehicleType = new VehicleTypeDTO
                    {
                        VehicleTypeId = v.VehicleType.VehicleTypeId,
                        VehicleTypeName = v.VehicleType.VehicleTypeName,
                        Description = v.VehicleType.Description
                    },
                    Owner = new GetDetailOwnerDTO
                    {
                        UserId = v.Owner.UserId,
                        FullName = v.Owner.FullName,
                        CompanyName = v.Owner.CompanyName
                    },
                    ImageUrls = v.VehicleImages.Select(i => new VehicleImageDetailDTO
                    {
                        VehicleImageId = i.VehicleImageId,
                        ImageURL = i.ImageURL,
                        Caption = i.Caption,
                        CreatedAt = i.CreatedAt
                    }).ToList()
                    // (Không có Documents, giống hàm GetAll)
                }).ToList();

                // 5. Tạo đối tượng PaginatedDTO
                var paginatedResult = new PaginatedDTO<VehicleDetailDTO>(result, totalCount, pageNumber, pageSize);

                return new ResponseDTO("Get my vehicles successfully", 200, true, paginatedResult);
            }
            catch (Exception ex)
            {
                return new ResponseDTO("Error while getting vehicles", 500, false, ex.Message);
            }
        }


        // (Thêm hàm này vào cuối lớp VehicleService của bạn)
        // (Nhớ import: using Microsoft.EntityFrameworkCore;)
        // (Nhớ import: using Common.DTOs;) // Cho PaginatedDTO

        public async Task<ResponseDTO> GetMyActiveVehiclesAsync(int pageNumber, int pageSize)
        {
            try
            {
                var ownerId = _userUtility.GetUserIdFromToken();
                if (ownerId == Guid.Empty)
                    return new ResponseDTO("Unauthorized or invalid token", 401, false);

                // 1. Lấy IQueryable từ Repo
                var query = _unitOfWork.VehicleRepo.GetVehiclesByOwnerIdQueryable(ownerId)
                                 // ***** THAY ĐỔI LỌC TẠI ĐÂY *****
                                 .Where(v => v.Status == VehicleStatus.ACTIVE);

                // 2. Đếm tổng số (dùng query đã lọc)
                var totalCount = await query.CountAsync();

                // 3. Lấy dữ liệu của trang
                var vehicles = await query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // 4. Map sang DTO (Dùng lại DTO từ hàm GetAll/GetById của bạn)
                var result = vehicles.Select(v => new VehicleDetailDTO
                {
                    VehicleId = v.VehicleId,
                    PlateNumber = v.PlateNumber,
                    Model = v.Model,
                    Brand = v.Brand,
                    Color = v.Color,
                    YearOfManufacture = v.YearOfManufacture,
                    PayloadInKg = v.PayloadInKg,
                    VolumeInM3 = v.VolumeInM3,
                    Status = v.Status, // Sẽ luôn là ACTIVE
                    VehicleType = new VehicleTypeDTO
                    {
                        VehicleTypeId = v.VehicleType.VehicleTypeId,
                        VehicleTypeName = v.VehicleType.VehicleTypeName,
                        Description = v.VehicleType.Description
                    },
                    Owner = new GetDetailOwnerDTO
                    {
                        UserId = v.Owner.UserId,
                        FullName = v.Owner.FullName,
                        CompanyName = v.Owner.CompanyName
                    },
                    ImageUrls = v.VehicleImages.Select(i => new VehicleImageDetailDTO
                    {
                        VehicleImageId = i.VehicleImageId,
                        ImageURL = i.ImageURL,
                        Caption = i.Caption,
                        CreatedAt = i.CreatedAt
                    }).ToList()
                }).ToList();

                // 5. Tạo đối tượng PaginatedDTO
                var paginatedResult = new PaginatedDTO<VehicleDetailDTO>(result, totalCount, pageNumber, pageSize);

                return new ResponseDTO("Get my active vehicles successfully", 200, true, paginatedResult);
            }
            catch (Exception ex)
            {
                return new ResponseDTO("Error while getting active vehicles", 500, false, ex.Message);
            }
        }

    }
}
