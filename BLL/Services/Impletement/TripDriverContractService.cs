using System;
using System.Linq;
using System.Threading.Tasks;
using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.Enums.Type;
using DAL.Entities;
using DAL.UnitOfWork;

namespace BLL.Services.Implement
{
    public class TripDriverContractService : ITripDriverContractService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;

        public TripDriverContractService(IUnitOfWork unitOfWork, UserUtility userUtility)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
        }

        // =========================================================
        // 1) CREATE CONTRACT (Owner ↔ Driver)
        // =========================================================
        public async Task<ResponseDTO> CreateAsync(CreateTripDriverContractDTO dto)
        {
            try
            {
                var ownerId = _userUtility.GetUserIdFromToken();
                if (ownerId == Guid.Empty)
                    return new ResponseDTO("Unauthorized or invalid token", 401, false);

                // Trip phải thuộc Owner đang đăng nhập
                var trip = await _unitOfWork.TripRepo.GetByIdAsync(dto.TripId);
                if (trip == null || trip.OwnerId != ownerId)
                    return new ResponseDTO("Trip not found or not owned by current user", 403, false);

                // Driver tồn tại?
                var driver = await _unitOfWork.DriverRepo.GetByIdAsync(dto.DriverId);
                if (driver == null)
                    return new ResponseDTO("Driver not found", 404, false);

                // Template mới nhất loại DRIVER_CONTRACT
                var template = (await _unitOfWork.ContractTemplateRepo.GetAllAsync(
                    filter: t => t.Type == ContractType.DRIVER_CONTRACT,
                    orderBy: q => q.OrderByDescending(x => x.Version)
                )).FirstOrDefault();

                if (template == null)
                    return new ResponseDTO("No Driver Contract Template found", 404, false);

                // Tạo hợp đồng
                var contract = new TripDriverContract
                {
                    ContractId = Guid.NewGuid(),
                    ContractCode = $"CON-DRV-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                    TripId = trip.TripId,
                    OwnerId = ownerId,

                    CounterpartyId = driver.UserId, // Driver kế thừa BaseUser → UserId
                    ContractTemplateId = template.ContractTemplateId,
                    Version = template.Version,

                    Type = ContractType.DRIVER_CONTRACT,
                    Status = ContractStatus.PENDING,
                    CreateAt = DateTime.UtcNow
                };

                await _unitOfWork.TripDriverContractRepo.AddAsync(contract);
                await _unitOfWork.SaveChangeAsync();

                var dtoOut = new TripDriverContractDTO
                {
                    ContractId = contract.ContractId,
                    ContractCode = contract.ContractCode,
                    TripId = trip.TripId,
                    TripCode = trip.TripCode,
                    OwnerId = ownerId,
                    OwnerName = trip.Owner?.FullName ?? string.Empty,
                    DriverId = driver.UserId,
                    DriverName = driver.FullName ?? string.Empty,
                    ContractTemplateId = contract.ContractTemplateId,
                    TemplateName = template.ContractTemplateName,
                    Version = contract.Version,
                    Status = contract.Status,
                    Type = contract.Type,
                    CreateAt = contract.CreateAt
                };

                return new ResponseDTO("Create driver contract successfully", 200, true, dtoOut);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error creating driver contract: {ex.Message}", 500, false);
            }
        }

        // =========================================================
        // 2) SIGN CONTRACT (Owner hoặc Driver ký)
        //  - Nếu 1 bên ký → vẫn PENDING
        //  - Nếu cả 2 bên ký → COMPLETED + EffectiveDate
        // =========================================================
        public async Task<ResponseDTO> SignAsync(Guid contractId)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                    return new ResponseDTO("Unauthorized or invalid token", 401, false);

                var contracts = await _unitOfWork.TripDriverContractRepo.GetAllAsync(
                    filter: c => c.ContractId == contractId,
                    includeProperties: "Owner,Counterparty,Trip,ContractTemplate"
                );
                var contract = contracts.FirstOrDefault();
                if (contract == null)
                    return new ResponseDTO("Contract not found", 404, false);

                var now = DateTime.UtcNow;
                var acted = false;

                // Ai ký?
                if (userId == contract.OwnerId)
                {
                    if (!contract.OwnerSigned)
                    {
                        contract.OwnerSigned = true;
                        contract.OwnerSignAt = now;
                        acted = true;
                    }
                }
                else if (userId == contract.CounterpartyId) // Driver
                {
                    if (!contract.CounterpartySigned)
                    {
                        contract.CounterpartySigned = true;
                        contract.CounterpartySignAt = now;
                        acted = true;
                    }
                }
                else
                {
                    return new ResponseDTO("You are not a party of this contract", 403, false);
                }

                if (!acted)
                    return new ResponseDTO("Already signed", 200, true);

                // Nếu cả 2 bên đều ký → COMPLETED
                if (contract.OwnerSigned && contract.CounterpartySigned)
                {
                    contract.Status = ContractStatus.COMPLETED;
                    contract.EffectiveDate = now;
                }

                await _unitOfWork.TripDriverContractRepo.UpdateAsync(contract);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Signed successfully", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error signing contract: {ex.Message}", 500, false);
            }
        }

        // =========================================================
        // 3) GET BY ID (Contract + Template + Terms)
        // =========================================================
        public async Task<ResponseDTO> GetByIdAsync(Guid contractId)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                    return new ResponseDTO("Unauthorized or invalid token", 401, false);

                var contracts = await _unitOfWork.TripDriverContractRepo.GetAllAsync(
                    filter: c => c.ContractId == contractId,
                    includeProperties: "Owner,Counterparty,Trip,ContractTemplate.ContractTerms"
                );
                var contract = contracts.FirstOrDefault();
                if (contract == null)
                    return new ResponseDTO("Contract not found", 404, false);

                // Map hợp đồng
                var contractDto = new TripDriverContractDTO
                {
                    ContractId = contract.ContractId,
                    ContractCode = contract.ContractCode,
                    TripId = contract.TripId,
                    TripCode = contract.Trip?.TripCode ?? string.Empty,

                    OwnerId = contract.OwnerId,
                    OwnerName = contract.Owner?.FullName ?? string.Empty,

                    DriverId = contract.CounterpartyId,
                    DriverName = contract.Counterparty?.FullName ?? string.Empty,

                    ContractTemplateId = contract.ContractTemplateId,
                    TemplateName = contract.ContractTemplate?.ContractTemplateName ?? string.Empty,
                    Version = contract.Version,
                    ContractValue = contract.ContractValue,
                    Currency = contract.Currency,
                    Status = contract.Status,

                    OwnerSigned = contract.OwnerSigned,
                    OwnerSignAt = contract.OwnerSignAt,
                    DriverSigned = contract.CounterpartySigned,
                    DriverSignAt = contract.CounterpartySignAt,

                    FileURL = contract.FileURL,
                    CreateAt = contract.CreateAt,
                    EffectiveDate = contract.EffectiveDate,
                    ExpirationDate = contract.ExpirationDate,
                    Note = contract.Note,
                    Type = contract.Type
                };

                // Map template
                var templateDto = new ContractTemplateDTO
                {
                    ContractTemplateId = contract.ContractTemplate?.ContractTemplateId ?? Guid.Empty,
                    ContractTemplateName = contract.ContractTemplate?.ContractTemplateName ?? string.Empty,
                    Version = contract.ContractTemplate?.Version ?? string.Empty,
                    CreatedAt = contract.ContractTemplate?.CreatedAt ?? DateTime.MinValue,
                    Type = contract.ContractTemplate?.Type ?? ContractType.DRIVER_CONTRACT
                };

                // Map terms (Content/Order đúng với DTO bạn đang dùng)
                var termsDto = contract.ContractTemplate?.ContractTerms?
                    .Select(t => new ContractTermDTO
                    {
                        ContractTermId = t.ContractTermId,
                        Content = t.Content,
                        Order = t.Order,
                        ContractTemplateId = t.ContractTemplateId
                    })
                    .OrderBy(t => t.Order)
                    .ToList() ?? new();

                var detailDto = new TripDriverContractDetailDTO
                {
                    Contract = contractDto,
                    Template = templateDto,
                    Terms = termsDto
                };

                return new ResponseDTO("Get contract successfully", 200, true, detailDto);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error fetching contract detail: {ex.Message}", 500, false);
            }
        }

        /// <summary>
        /// (Nội bộ) Chỉ tạo Entity, KHÔNG SaveChanges, ném Exception nếu lỗi
        /// </summary>
        public async Task<TripDriverContract> CreateContractInternalAsync(CreateTripDriverContractDTO dto, Guid ownerId)
        {
            // (Logic này được lấy từ hàm CreateAsync gốc của bạn)
            try
            {
                // Trip phải thuộc Owner
                var trip = await _unitOfWork.TripRepo.GetByIdAsync(dto.TripId);
                if (trip == null || trip.OwnerId != ownerId)
                    throw new Exception("Trip not found or not owned by current user"); // Ném lỗi

                // Driver tồn tại?
                var driver = await _unitOfWork.DriverRepo.GetByIdAsync(dto.DriverId);
                if (driver == null)
                    throw new Exception("Driver not found"); // Ném lỗi

                // Template mới nhất loại DRIVER_CONTRACT
                var template = (await _unitOfWork.ContractTemplateRepo.GetAllAsync(
                    filter: t => t.Type == ContractType.DRIVER_CONTRACT,
                    orderBy: q => q.OrderByDescending(x => x.Version)
                )).FirstOrDefault();

                if (template == null)
                    throw new Exception("No Driver Contract Template found"); // Ném lỗi

                // Tạo hợp đồng
                var contract = new TripDriverContract
                {
                    ContractId = Guid.NewGuid(),
                    ContractCode = $"CON-DRV-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                    TripId = trip.TripId,
                    OwnerId = ownerId,
                    CounterpartyId = driver.UserId, // Driver kế thừa BaseUser → UserId
                    ContractTemplateId = template.ContractTemplateId,
                    Version = template.Version,
                    Type = ContractType.DRIVER_CONTRACT,
                    Status = ContractStatus.PENDING, // Luôn PENDING khi mới tạo
                    CreateAt = DateTime.UtcNow
                };

                await _unitOfWork.TripDriverContractRepo.AddAsync(contract);

                // KHÔNG GỌI SaveChangeAsync() (Vì đây là hàm nội bộ)

                return contract;
            }
            catch (Exception ex)
            {
                // Ném lỗi để Service gọi (TripDriverAssignmentService) có thể Rollback
                throw new Exception($"Error creating internal driver contract: {ex.Message}", ex);
            }
        }
    }
}
