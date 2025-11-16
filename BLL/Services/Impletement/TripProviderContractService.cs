using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.Enums.Type;
using DAL.Entities;
using DAL.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Services.Implement
{
    public class TripProviderContractService : ITripProviderContractService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;

        public TripProviderContractService(IUnitOfWork unitOfWork, UserUtility userUtility)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
        }

        // ============================================================
        // 🧩 1️⃣ CREATE CONTRACT (Owner tạo hợp đồng với Provider)
        // ============================================================
        public async Task<ResponseDTO> CreateAsync(CreateTripProviderContractDTO dto)
        {
            try
            {
                // 🔹 Lấy Owner hiện tại từ Token
                var ownerId = _userUtility.GetUserIdFromToken();
                if (ownerId == Guid.Empty)
                    return new ResponseDTO("Unauthorized or invalid token", 401, false);

                // 🔹 Lấy thông tin Trip
                var trip = await _unitOfWork.TripRepo.GetByIdAsync(dto.TripId);
                if (trip == null)
                    return new ResponseDTO("Trip not found", 404, false);

                // 🔹 Lấy thông tin Provider từ DTO
                var provider = await _unitOfWork.ProviderRepo.GetByIdAsync(dto.ProviderId);
                if (provider == null)
                    return new ResponseDTO("Provider not found", 404, false);

                // 🔹 Lấy ContractTemplate mới nhất (Type = PROVIDER_CONTRACT)
                var template = (await _unitOfWork.ContractTemplateRepo.GetAllAsync(
                    filter: t => t.Type == ContractType.PROVIDER_CONTRACT,
                    orderBy: q => q.OrderByDescending(t => t.Version)
                )).FirstOrDefault();

                if (template == null)
                    return new ResponseDTO("No Provider Contract Template found", 404, false);

                // 🔹 Check nếu hợp đồng đã tồn tại (tránh tạo trùng)
                var existingContract = (await _unitOfWork.TripProviderContractRepo.GetAllAsync(
                    filter: c => c.TripId == trip.TripId && c.CounterpartyId == provider.UserId
                )).FirstOrDefault();

                if (existingContract != null)
                    return new ResponseDTO("Contract already exists for this trip and provider", 400, false);

                // 🔹 Tạo hợp đồng mới
                var contract = new TripProviderContract
                {
                    ContractId = Guid.NewGuid(),
                    ContractCode = $"CON-PROV-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                    TripId = trip.TripId,
                    OwnerId = ownerId,
                    CounterpartyId = provider.UserId,
                    ContractTemplateId = template.ContractTemplateId,
                    Version = template.Version,
                    Type = ContractType.PROVIDER_CONTRACT,
                    Status = ContractStatus.PENDING,
                    CreateAt = DateTime.UtcNow
                };

                await _unitOfWork.TripProviderContractRepo.AddAsync(contract);
                await _unitOfWork.SaveChangeAsync();

                // 🔹 Chuẩn bị DTO kết quả
                var result = new TripProviderContractDTO
                {
                    ContractId = contract.ContractId,
                    ContractCode = contract.ContractCode,
                    TripId = contract.TripId,
                    OwnerId = ownerId,
                    ProviderId = provider.UserId,
                    Version = contract.Version,
                    Status = contract.Status,
                    Type = contract.Type,
                    CreateAt = contract.CreateAt
                };

                return new ResponseDTO("Trip Provider Contract created successfully", 200, true, result);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error creating TripProviderContract: {ex.Message}", 500, false);
            }
        }

        // ============================================================
        // 🧩 2️⃣ SIGN CONTRACT (Owner hoặc Provider ký)
        // ============================================================
        public async Task<ResponseDTO> SignAsync(Guid contractId)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                    return new ResponseDTO("Unauthorized", 401, false);

                // 1. Lấy Hợp đồng
                var contract = await _unitOfWork.TripProviderContractRepo.GetByIdAsync(contractId);
                if (contract == null)
                    return new ResponseDTO("Contract not found", 404, false);

                // ⚠️ 2. Lấy Chuyến đi (Trip) liên quan
                var trip = await _unitOfWork.TripRepo.GetByIdAsync(contract.TripId);
                if (trip == null)
                    return new ResponseDTO("Associated Trip not found for this contract", 404, false);

                // 3. Xác thực quyền
                bool isOwner = contract.OwnerId == userId;
                bool isProvider = contract.CounterpartyId == userId;

                if (!isOwner && !isProvider)
                    return new ResponseDTO("You are not authorized to sign this contract", 403, false);

                // 4. Áp dụng chữ ký
                if (isOwner)
                {
                    if (contract.OwnerSigned)
                        return new ResponseDTO("Owner already signed", 400, false);
                    contract.OwnerSigned = true;
                    contract.OwnerSignAt = DateTime.UtcNow;
                }
                else if (isProvider)
                {
                    if (contract.CounterpartySigned)
                        return new ResponseDTO("Provider already signed", 400, false);
                    contract.CounterpartySigned = true;
                    contract.CounterpartySignAt = DateTime.UtcNow;
                }

                // ⚠️ 5. Cập nhật trạng thái (Contract VÀ Trip) dựa trên chữ ký
                if (contract.OwnerSigned && contract.CounterpartySigned)
                {
                    // --- Cả hai đã ký ---
                    contract.Status = ContractStatus.COMPLETED;
                    contract.EffectiveDate = DateTime.UtcNow;

                    // Cập nhật Trip sang trạng thái "Đợi Provider Thanh Toán"
                    trip.Status = TripStatus.AWAITING_PROVIDER_PAYMENT;
                }
                else
                {
                    // --- Mới chỉ có 1 bên ký ---
                    contract.Status = ContractStatus.AWAITING_CONTRACT_SIGNATURE;

                    // Cập nhật Trip (hoặc giữ nguyên) trạng thái "Đợi Ký HĐ Provider"
                    trip.Status = TripStatus.AWAITING_PROVIDER_CONTRACT;
                }

                // 6. Lưu thay đổi cho cả hai
                await _unitOfWork.TripProviderContractRepo.UpdateAsync(contract);
                await _unitOfWork.TripRepo.UpdateAsync(trip); // ⚠️ CẬP NHẬT TRIP
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Contract signed successfully", 200, true, new
                {
                    contract.ContractId,
                    ContractStatus = contract.Status.ToString(), // Trả về status dạng string
                    contract.OwnerSigned,
                    contract.CounterpartySigned,
                    TripStatus = trip.Status.ToString() // Trả về thêm trạng thái Trip
                });
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error signing contract: {ex.Message}", 500, false);
            }
        }

        // ============================================================
        // 🧩 3️⃣ GET CONTRACT BY ID (bao gồm Template + Term)
        // ============================================================
        public async Task<ResponseDTO> GetByIdAsync(Guid contractId)
        {
            try
            {
                // 🧩 Lấy UserId từ Token
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                    return new ResponseDTO("Unauthorized or invalid token", 401, false);

                // 🧩 Truy vấn hợp đồng đầy đủ liên kết
                var contracts = await _unitOfWork.TripProviderContractRepo.GetAllAsync(
                    filter: c => c.ContractId == contractId,
                    includeProperties: "Owner,Counterparty,Trip,ContractTemplate.ContractTerms"
                );

                var contract = contracts.FirstOrDefault();
                if (contract == null)
                    return new ResponseDTO("Contract not found", 404, false);

                // 🧩 Mapping DTO: thông tin hợp đồng chính
                var contractDto = new TripProviderContractDTO
                {
                    ContractId = contract.ContractId,
                    ContractCode = contract.ContractCode,
                    TripId = contract.TripId,
                    TripCode = contract.Trip?.TripCode ?? string.Empty,
                    OwnerId = contract.OwnerId,
                    OwnerName = contract.Owner?.FullName ?? string.Empty,
                    ProviderId = contract.CounterpartyId,
                    ProviderName = contract.Counterparty?.FullName ?? string.Empty,
                    ContractTemplateId = contract.ContractTemplateId,
                    TemplateName = contract.ContractTemplate?.ContractTemplateName ?? string.Empty,
                    Version = contract.Version,
                    ContractValue = contract.ContractValue,
                    Currency = contract.Currency,
                    Status = contract.Status,
                    OwnerSigned = contract.OwnerSigned,
                    OwnerSignAt = contract.OwnerSignAt,
                    ProviderSigned = contract.CounterpartySigned,
                    ProviderSignAt = contract.CounterpartySignAt,
                    FileURL = contract.FileURL,
                    CreateAt = contract.CreateAt,
                    EffectiveDate = contract.EffectiveDate,
                    ExpirationDate = contract.ExpirationDate,
                    Note = contract.Note,
                    Type = contract.Type
                };

                // 🧩 Mapping DTO: thông tin Template
                var templateDto = new ContractTemplateDTO
                {
                    ContractTemplateId = contract.ContractTemplate?.ContractTemplateId ?? Guid.Empty,
                    ContractTemplateName = contract.ContractTemplate?.ContractTemplateName ?? string.Empty,
                    Version = contract.ContractTemplate?.Version ?? string.Empty,
                    CreatedAt = contract.ContractTemplate?.CreatedAt ?? DateTime.MinValue,
                    Type = contract.ContractTemplate?.Type ?? ContractType.PROVIDER_CONTRACT
                };

                var termsDto = contract.ContractTemplate?.ContractTerms?
               .Select(t => new ContractTermDTO
               {
                ContractTermId = t.ContractTermId,
                Content = t.Content,  
                Order = t.Order,
                ContractTemplateId = t.ContractTemplateId
               })
                .OrderBy(t => t.Order)
                .ToList() ?? new List<ContractTermDTO>();



                // 🧩 Gộp các phần lại
                var detailDto = new TripProviderContractDetailDTO
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

        public async Task CreateAndAddContractAsync(Guid tripId, Guid ownerId, Guid providerId, decimal fare)
        {
            // 🔹 Lấy ContractTemplate mới nhất
            var template = (await _unitOfWork.ContractTemplateRepo.GetAllAsync(
                filter: t => t.Type == ContractType.PROVIDER_CONTRACT,
                orderBy: q => q.OrderByDescending(t => t.Version)
            )).FirstOrDefault();

            // 🔹 Validation (Ném lỗi để Transaction bên ngoài Rollback)
            if (template == null)
                throw new Exception("Không tìm thấy Mẫu hợp đồng (Contract Template) cho Provider.");

            // 🔹 Check nếu hợp đồng đã tồn tại
            var existingContract = (await _unitOfWork.TripProviderContractRepo.GetAllAsync(
                filter: c => c.TripId == tripId && c.CounterpartyId == providerId
            )).FirstOrDefault();

            if (existingContract != null)
                throw new Exception("Hợp đồng đã tồn tại cho chuyến đi và nhà cung cấp này.");

            // 🔹 Tạo hợp đồng mới
            var contract = new TripProviderContract
            {
                ContractId = Guid.NewGuid(),
                ContractCode = $"CON-PROV-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                TripId = tripId,         // Từ tham số
                OwnerId = ownerId,      // Từ tham số
                CounterpartyId = providerId, // Từ tham số
                ContractTemplateId = template.ContractTemplateId,
                Version = template.Version,
                Type = ContractType.PROVIDER_CONTRACT,
                Status = ContractStatus.PENDING, // Chờ ký
                CreateAt = DateTime.UtcNow,

                // --- Gán giá trị từ tham số ---
                ContractValue = fare,
                Currency = "VND" // Giả định
            };

            // 🔹 Thêm vào UoW (KHÔNG SAVE)
            await _unitOfWork.TripProviderContractRepo.AddAsync(contract);
        }

        public async Task<ResponseDTO> GetAllAsync(int pageNumber, int pageSize)
        {
            try
            {
                // 1. Lấy thông tin User
                var userId = _userUtility.GetUserIdFromToken();
                var userRole = _userUtility.GetUserRoleFromToken();
                if (userId == Guid.Empty)
                    return new ResponseDTO("Unauthorized: Invalid token", 401, false);

                // 2. Lấy IQueryable cơ sở
                var query = _unitOfWork.TripProviderContractRepo.GetAll()
                                     .AsNoTracking();

                // 3. Lọc theo Vai trò (Authorization)
                if (userRole == "Owner")
                {
                    query = query.Where(c => c.OwnerId == userId);
                }
                else if (userRole == "Provider")
                {
                    query = query.Where(c => c.CounterpartyId == userId);
                }
                else if (userRole == "Admin")
                {
                    // Admin: không cần lọc
                }
                else
                {
                    // Các vai trò khác (ví dụ: Driver) không được xem
                    return new ResponseDTO("Forbidden: You do not have permission", 403, false);
                }

                // 4. Đếm tổng số lượng (sau khi lọc)
                var totalCount = await query.CountAsync();

                // 5. Lấy dữ liệu của trang và Map sang DTO
                // (Dùng DTO tóm tắt, giống DTO trả về của hàm Create/GetById)
                var contractsDto = await query
                    .Include(c => c.Trip)
                    .Include(c => c.Owner)
                    .Include(c => c.Counterparty) // (Provider)
                    .Include(c => c.ContractTemplate)
                    .OrderByDescending(c => c.CreateAt) // Sắp xếp mới nhất trước
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => new TripProviderContractDTO // Map trên DB
                    {
                        ContractId = c.ContractId,
                        ContractCode = c.ContractCode,
                        TripId = c.TripId,
                        TripCode = c.Trip != null ? c.Trip.TripCode : "N/A",
                        OwnerId = c.OwnerId,
                        OwnerName = c.Owner != null ? c.Owner.FullName : "N/A",
                        ProviderId = c.CounterpartyId,
                        ProviderName = c.Counterparty != null ? c.Counterparty.FullName : "N/A",
                        ContractTemplateId = c.ContractTemplateId,
                        TemplateName = c.ContractTemplate != null ? c.ContractTemplate.ContractTemplateName : "N/A",
                        Version = c.Version,
                        Status = c.Status,
                        Type = c.Type,
                        CreateAt = c.CreateAt,
                        EffectiveDate = c.EffectiveDate,
                        OwnerSigned = c.OwnerSigned,
                        ProviderSigned = c.CounterpartySigned
                    })
                    .ToListAsync();

                // 6. Tạo kết quả PaginatedDTO
                var paginatedResult = new PaginatedDTO<TripProviderContractDTO>(contractsDto, totalCount, pageNumber, pageSize);

                return new ResponseDTO("Retrieved provider contracts successfully", 200, true, paginatedResult);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error getting provider contracts: {ex.Message}", 500, false);
            }
        }
    }
}
