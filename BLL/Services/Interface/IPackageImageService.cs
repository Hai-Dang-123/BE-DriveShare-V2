using Common.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IPackageImageService
    {
        Task<ResponseDTO> CreatePackageImageAsync(PackageImageCreateDTO packageImageCreateDTO);
        Task<ResponseDTO> GetAllPackageImagesByPackageIdAsync(Guid packageId);
        Task<ResponseDTO> DeletePackageImageAsync(Guid packageImageId);
        Task<ResponseDTO> UpdatePackageImageAsync(UpdatePackageImageDTO updatePackageImageDTO);
        Task<ResponseDTO> GetPackageImageByIdAsync(Guid packageImageId);
    }
}
