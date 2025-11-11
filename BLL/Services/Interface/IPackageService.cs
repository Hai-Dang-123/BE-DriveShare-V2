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
        Task<ResponseDTO> GetAllPackagesAsync(int pageNumber, int pageSize);
        Task<ResponseDTO> UpdatePackageAsync(PackageUpdateDTO updatePackageDTO);
        Task<ResponseDTO> DeletePackageAsync(Guid packageId);
        Task<ResponseDTO> GetPackagesByUserIdAsync(int pageNumber, int pageSize);
        Task<ResponseDTO> GetMyPendingPackagesAsync(int pageNumber, int pageSize);
    }
}
