using Common.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IPostPackageService
    {
        Task<ResponseDTO> CreateProviderPostPackageAsync(PostPackageCreateDTO postPackageCreateDTO);
        Task<ResponseDTO> ChangePostPackageStatusAsync(ChangePostPackageStatusDTO changePostPackageStatusDTO);

        Task<ResponseDTO> GetAllPostPackagesAsync(int pageNumber, int pageSize);
        Task<ResponseDTO> GetPostPackagesByProviderIdAsync(Guid providerId, int pageNumber, int pageSize);

        Task<ResponseDTO> GetMyPostPackagesAsync(int pageNumber, int pageSize);
        Task<ResponseDTO> GetPostPackageDetailsAsync(Guid postPackageId);

        Task<ResponseDTO> GetOpenPostPackagesAsync(int pageNumber, int pageSize);


    }
}
