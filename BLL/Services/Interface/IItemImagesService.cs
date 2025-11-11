using Common.DTOs;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    public interface IItemImagesService
    {
        Task<ResponseDTO> CreateItemImageAsync(ItemImageCreateDTO itemImageDTO);
        Task<ResponseDTO> GetALlItemImagesByItemIdAsync(Guid itemId);
        Task<ResponseDTO> DeleteItemImageAsync(Guid itemImageId);
        Task<ResponseDTO> UpdateItemImageAsync(UpdateItemImageDTO updateItemImageDTO);
        Task<ResponseDTO> GetItemImageByIdAsync(Guid itemImageId);


        //

        Task AddImagesToItemAsync(Guid itemId, Guid userId, List<IFormFile> files);
    }
}
