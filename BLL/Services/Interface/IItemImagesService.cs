using Common.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Interface
{
    internal interface IItemImagesService
    {
        Task<ResponseDTO> CraeteItemImageAsync(ItemImageDTO itemImageDTO);
        Task<ResponseDTO> GetALlItemImagesByItemIdAsync(Guid itemId);
        Task<ResponseDTO> DeleteItemImageAsync(Guid itemImageId);
        Task<ResponseDTO> UpdateItemImageAsync(UpdateItemImageDTO updateItemImageDTO);
        Task<ResponseDTO> GetItemImageByIdAsync(Guid itemImageId);
    }
}
