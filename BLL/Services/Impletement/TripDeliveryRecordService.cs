using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using DAL.Entities;
using DAL.Repositories.Interface;
using DAL.UnitOfWork;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class TripDeliveryRecordService : ITripDeliveryRecordService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFirebaseUploadService _firebaseUploadService;
        private readonly UserUtility _userUtility;   
        public TripDeliveryRecordService(IUnitOfWork unitOfWork, UserUtility userUtility, IFirebaseUploadService firebaseUploadService)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
            _firebaseUploadService = firebaseUploadService;
        }
        // create trip delivery record
        public async Task<ResponseDTO> CreateTripDeliveryRecordAsync(TripDeliveryRecordCreateDTO tripDeliveryRecordDTO)
        {
            try
            {
                var userid = _userUtility.GetUserIdFromToken();
                if (userid == null)
                {
                    return new ResponseDTO
                    {
                        IsSuccess = false,
                        StatusCode = StatusCodes.Status401Unauthorized,
                        Message = "Unauthorized: User ID not found in token."
                    };
                }
                var newTripDeliveryRecord = new TripDeliveryRecord
            {
                DeliveryRecordId = Guid.NewGuid(),
                TripId = tripDeliveryRecordDTO.TripId,
                DeliveryRecordTemplateId = tripDeliveryRecordDTO.DeliveryRecordTempalteId,
                TripContactId = tripDeliveryRecordDTO.StripContractId,
                DriverId = userid,
                CreatedAt = DateTime.UtcNow,
                Notes = tripDeliveryRecordDTO.Notes,
                Type = tripDeliveryRecordDTO.type,
                Status = DeliveryRecordStatus.PENDING
                };
                await _unitOfWork.TripDeliveryRecordRepo.AddAsync(newTripDeliveryRecord);
                await _unitOfWork.SaveChangeAsync();
                return new ResponseDTO
                {
                    IsSuccess = true,
                    Message = "Trip Delivery Record created successfully",
                };
            } catch (Exception ex)
            {
                return new ResponseDTO
                {
                    IsSuccess = false,
                    Message = $"Error creating Trip Delivery Record: {ex.Message}"
                };
            }
        }
        // get trip delivery record by id
        public async Task<ResponseDTO?> GetByIdAsync(Guid tripDeliveryRecordId)
        {
            try
            {
                var record = await _unitOfWork.TripDeliveryRecordRepo.GetByIdWithDetailsAsync(tripDeliveryRecordId);

                if (record == null)
                    return new ResponseDTO { 
                        IsSuccess = false, 
                        Message = "No record found for this TripDeliveryRecordId." 
                    };

                var dto = new TripDeliveryRecordReadDTO
                {
                    TripId = record.TripId,
                    DeliveryRecordTempalteId = record.DeliveryRecordTemplateId,
                    type = record.Type.ToString(),
                    Status = record.Status.ToString(),
                    Notes = record.Notes,
                    DriverSignatureUrl = record.DriverSignatureUrl,
                    DriverSignedAt = record.DriverSignedAt,
                    ContactSignatureUrl = record.ContactSignatureUrl,
                    ContactSignedAt = record.ContactSignedAt,

                    tripContact = record.TripContact != null
                        ? new TripContactDTO
                        {
                            TripContactId = record.TripContact.TripContactId,
                            Type = record.TripContact.Type.ToString(),
                            FullName = record.TripContact.FullName,
                            PhoneNumber = record.TripContact.PhoneNumber,
                            Note = record.TripContact.Note
                        }
                        : null,

                    deliveryRecordTemplate = record.DeliveryRecordTemplate != null
                        ? new DeliveryRecordTemplateDTO
                        {
                            DeliveryRecordTemplateId = record.DeliveryRecordTemplate.DeliveryRecordTemplateId,
                            TemplateName = record.DeliveryRecordTemplate.TemplateName,
                            Version = record.DeliveryRecordTemplate.Version,
                            Type = record.DeliveryRecordTemplate.Type.ToString(),
                            CreatedAt = record.DeliveryRecordTemplate.CreatedAt,
                            DeliveryRecordTerms = record.DeliveryRecordTemplate.DeliveryRecordTerms
                                .Select(term => new DeliveryRecordTermDTO
                                {
                                    DeliveryRecordTermId = term.DeliveryRecordTermId,
                                    Content = term.Content,
                                    DisplayOrder = term.DisplayOrder
                                }).ToList()
                        }
                        : null
                };

                return new ResponseDTO
                {
                    IsSuccess = true,
                    Message = "Trip delivery records retrieved successfully.",
                    Result = dto
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    IsSuccess = false,
                    Message = $"Error retrieving Trip Delivery Record: {ex.Message}"
                };
            }
        }
        // get trip delivery records by trip id
        public async Task<ResponseDTO> GetByTripIdAsync(Guid tripId)
        {
            try
            {
                var records = await _unitOfWork.TripDeliveryRecordRepo.GetByTripIdAsync(tripId);

                if (records == null || !records.Any())
                {
                    return new ResponseDTO
                    {
                        IsSuccess = false,
                        StatusCode = StatusCodes.Status404NotFound,
                        Message = "No Trip Delivery Records found for this TripId."
                    };
                }

                var dtoList = records.Select(r => new TripDeliveryRecordReadDTO
                {
                    TripId = r.TripId,
                    DeliveryRecordTempalteId = r.DeliveryRecordTemplateId,
                    StripContractId = r.TripContactId,
                    type = r.Type.ToString(),
                    Status = r.Status.ToString(),
                    Notes = r.Notes,
                    DriverSignatureUrl = r.DriverSignatureUrl,
                    DriverSignedAt = r.DriverSignedAt,
                    ContactSignatureUrl = r.ContactSignatureUrl,
                    ContactSignedAt = r.ContactSignedAt,

                    tripContact = r.TripContact != null
                        ? new TripContactDTO
                        {
                            TripContactId = r.TripContact.TripContactId,
                            Type = r.TripContact.Type.ToString(),
                            FullName = r.TripContact.FullName,
                            PhoneNumber = r.TripContact.PhoneNumber,
                            Note = r.TripContact.Note
                        }
                        : null,

                    deliveryRecordTemplate = r.DeliveryRecordTemplate != null
                        ? new DeliveryRecordTemplateDTO
                        {
                            DeliveryRecordTemplateId = r.DeliveryRecordTemplate.DeliveryRecordTemplateId,
                            TemplateName = r.DeliveryRecordTemplate.TemplateName,
                            Version = r.DeliveryRecordTemplate.Version,
                            Type = r.DeliveryRecordTemplate.Type.ToString(),
                            CreatedAt = r.DeliveryRecordTemplate.CreatedAt,
                            DeliveryRecordTerms = r.DeliveryRecordTemplate.DeliveryRecordTerms
                                .Select(term => new DeliveryRecordTermDTO
                                {
                                    DeliveryRecordTermId = term.DeliveryRecordTermId,
                                    Content = term.Content,
                                    DisplayOrder = term.DisplayOrder
                                }).ToList()
                        }
                        : null
                }).ToList();

                return new ResponseDTO
                {
                    IsSuccess = true,
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Trip Delivery Records retrieved successfully.",
                    Result = dtoList
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    IsSuccess = false,
                    StatusCode = StatusCodes.Status500InternalServerError,
                    Message = $"Error retrieving Trip Delivery Records: {ex.Message}"
                };
            }

        }
        // sign delivery record
        public async Task<ResponseDTO> SignDeliveryRecordAsync(Guid tripDeliveryRecordId)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                    return new ResponseDTO("Unauthorized", 401, false);

                var record = await _unitOfWork.TripDeliveryRecordRepo.GetByIdWithDetailsAsync(tripDeliveryRecordId);
                if (record == null)
                    return new ResponseDTO("Delivery record not found", 404, false);

                bool isDriver = record.DriverId == userId;
                bool isContact = record.Trip.OwnerId == userId;

                if (!isDriver && !isContact)
                    return new ResponseDTO("You are not authorized to sign this delivery record", 403, false);

                // --- Ký biên bản ---
                if (isDriver)
                {
                    if (record.DriverSigned == true)
                        return new ResponseDTO("Driver already signed", 400, false);

                    record.DriverSigned = true;
                    record.DriverSignedAt = DateTime.UtcNow;
                }
                else if (isContact)
                {
                    if (record.ContactSigned == true)
                        return new ResponseDTO("Contact already signed", 400, false);

                    record.ContactSigned = true;
                    record.ContactSignedAt = DateTime.UtcNow;
                }

                // --- Cập nhật trạng thái biên bản ---
                if (record.DriverSigned == true && record.ContactSigned == true)
                {
                    record.Status = DeliveryRecordStatus.COMPLETED;

                }
                else
                {
                    // Nếu 1 bên đã ký thì chuyển sang trạng thái "IN_PROGRESS"
                    record.Status = DeliveryRecordStatus.AWAITING_DELIVERY_RECORD_SIGNATURE;
                }

                await _unitOfWork.TripDeliveryRecordRepo.UpdateAsync(record);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO{
                    IsSuccess = true,
                    Message = "Delivery record signed successfully",
                    StatusCode = StatusCodes.Status200OK                   
                };
            }
            catch (Exception ex)
            {
                return new ResponseDTO
                {
                    IsSuccess = false,
                    Message = $"Error signing Delivery Record: {ex.Message}"
                };
            }
        }

        public async Task CreateTripDeliveryRecordAsync(TripDeliveryRecordCreateDTO dto, Guid driverId)
        {
            try
            {
                // Validate DriverId (service gọi đến phải đảm bảo driverId hợp lệ)
                if (driverId == Guid.Empty)
                {
                    throw new ArgumentException("Invalid DriverId provided.", nameof(driverId));
                }

                var newTripDeliveryRecord = new TripDeliveryRecord
                {
                    DeliveryRecordId = Guid.NewGuid(),
                    TripId = dto.TripId,
                    DeliveryRecordTemplateId = dto.DeliveryRecordTempalteId,
                    TripContactId = dto.StripContractId,
                    DriverId = driverId, // ⚠️ SỬA ĐỔI: Lấy từ tham số
                    CreatedAt = DateTime.UtcNow,
                    Notes = dto.Notes,
                    Type = dto.type,
                    Status = DeliveryRecordStatus.PENDING
                };

                await _unitOfWork.TripDeliveryRecordRepo.AddAsync(newTripDeliveryRecord);

                // ⚠️ LƯU Ý: 
                // Hàm này không gọi SaveChangeAsync() hoặc CommitTransaction().
                // Hàm GỌI (ví dụ: TripDriverAssignmentService) sẽ chịu trách nhiệm
                // Commit Transaction sau khi gọi hàm này.
            }
            catch (Exception ex)
            {
                // Ghi log lỗi (nếu có logger)
                // _logger.LogError(ex, "Error creating Trip Delivery Record internal.");

                // ⚠️ SỬA ĐỔI: Ném Exception để hàm gọi xử lý Rollback
                throw new Exception($"Error creating Trip Delivery Record: {ex.Message}", ex);
            }
        }

    }
}
