using BLL.Services.Interface;
using Common.DTOs;
using Common.Enums.Type;
using DAL.Entities;
using DAL.UnitOfWork;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class PostContactService : IPostContactService
    {
        private readonly IUnitOfWork _unitOfWork;

        public PostContactService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task CreateAndAddContactsAsync(Guid postPackageId, PostContactInputDTO senderDto, PostContactInputDTO receiverDto)
        {
            // 1. Tạo Người gửi (Sender)
            var senderContact = new PostContact
            {
                PostContactId = Guid.NewGuid(),
                PostPackageId = postPackageId, // Liên kết với PostPackage
                Type = ContactType.SENDER,     // (Giả sử enum là 'ContactType')
                FullName = senderDto.FullName,
                PhoneNumber = senderDto.PhoneNumber,
                Email = senderDto.Email,
                Note = senderDto.Note
            };
            await _unitOfWork.PostContactRepo.AddAsync(senderContact);

            // 2. Tạo Người nhận (Receiver)
            var receiverContact = new PostContact
            {
                PostContactId = Guid.NewGuid(),
                PostPackageId = postPackageId, // Liên kết với PostPackage
                Type = ContactType.RECEIVER,
                FullName = receiverDto.FullName,
                PhoneNumber = receiverDto.PhoneNumber,
                Email = receiverDto.Email,
                Note = receiverDto.Note
            };
            await _unitOfWork.PostContactRepo.AddAsync(receiverContact);

            // (KHÔNG SAVE)
        }
    }
}
