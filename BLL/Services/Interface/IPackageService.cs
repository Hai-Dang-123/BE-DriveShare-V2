using Common.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IPackageService
    {
        Task<ResponseDTO> ProviderCreatePackageAsync(PackageCreateDTO packageDTO);
        Task<ResponseDTO> OwnerCreatePackageAsync(PackageCreateDTO packageDTO);
        Task<ResponseDTO> GetPackageByIdAsync(Guid packageId);
        
        Task<ResponseDTO> UpdatePackageAsync(PackageUpdateDTO updatePackageDTO);
        Task<ResponseDTO> DeletePackageAsync(Guid packageId);
        // 1. Lấy tất cả Packages (Admin/Public)
        Task<ResponseDTO> GetAllPackagesAsync(int pageNumber, int pageSize, string? search, string? sortField, string? sortOrder);

        // 2. Lấy Packages của User (Trừ Deleted)
        Task<ResponseDTO> GetPackagesByUserIdAsync(int pageNumber, int pageSize, string? search, string? sortField, string? sortOrder);

        // 3. Lấy Packages Pending của User
        Task<ResponseDTO> GetMyPendingPackagesAsync(int pageNumber, int pageSize, string? search, string? sortField, string? sortOrder);
    }
}
