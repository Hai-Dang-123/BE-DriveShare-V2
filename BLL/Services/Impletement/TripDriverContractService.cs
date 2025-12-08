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

                // chuyển status trip
                trip.Status = TripStatus.PENDING_DRIVER_ASSIGNMENT;
                trip.UpdateAt = DateTime.UtcNow;

                await _unitOfWork.TripRepo.UpdateAsync(trip);


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
        // ... (Các using cần thiết: BCrypt.Net, Common.Enums.Type, v.v...)

        public async Task<ResponseDTO> SignAsync(SignContractDTO dto)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                    return new ResponseDTO("Unauthorized", 401, false);

                // ========================================================================
                // 🛡️ BƯỚC 0: XÁC THỰC OTP (QUAN TRỌNG NHẤT)
                // ========================================================================

                // 1. Tìm Token OTP hợp lệ
                var validToken = await _unitOfWork.UserTokenRepo.GetAll()
                    .Where(t => t.UserId == userId
                                && t.TokenType == TokenType.CONTRACT_SIGNING_OTP
                                && !t.IsRevoked
                                && t.ExpiredAt > DateTime.UtcNow)
                    .OrderByDescending(t => t.CreatedAt)
                    .FirstOrDefaultAsync();

                if (validToken == null)
                {
                    return new ResponseDTO("Mã OTP không tồn tại hoặc đã hết hạn. Vui lòng lấy mã mới.", 400, false);
                }

                // 2. Verify mã OTP
                bool isOtpCorrect = BCrypt.Net.BCrypt.Verify(dto.Otp, validToken.TokenValue);

                if (!isOtpCorrect)
                {
                    return new ResponseDTO("Mã OTP không chính xác.", 400, false);
                }

                // 3. Hủy hiệu lực Token ngay lập tức
                validToken.IsRevoked = true;
                await _unitOfWork.UserTokenRepo.UpdateAsync(validToken);

                // ========================================================================
                // 📝 BƯỚC TIẾP THEO: LOGIC KÝ HỢP ĐỒNG DRIVER
                // ========================================================================

                // 1. Lấy Hợp đồng (TripDriverContract)
                var contract = await _unitOfWork.TripDriverContractRepo.GetByIdAsync(dto.ContractId);
                if (contract == null)
                    return new ResponseDTO("Contract not found", 404, false);

                // 2. Lấy Chuyến đi (Trip) liên quan
                var trip = await _unitOfWork.TripRepo.GetByIdAsync(contract.TripId);
                if (trip == null)
                    return new ResponseDTO("Associated Trip not found for this contract", 404, false);

                // 3. Xác thực quyền
                bool isOwner = contract.OwnerId == userId;
                bool isDriver = contract.CounterpartyId == userId; // Counterparty ở đây là Driver

                if (!isOwner && !isDriver)
                    return new ResponseDTO("You are not authorized to sign this contract", 403, false);

                // 4. Áp dụng chữ ký
                if (isOwner)
                {
                    if (contract.OwnerSigned)
                        return new ResponseDTO("Owner already signed", 400, false);
                    contract.OwnerSigned = true;
                    contract.OwnerSignAt = DateTime.UtcNow;
                }
                else if (isDriver)
                {
                    if (contract.CounterpartySigned)
                        return new ResponseDTO("Driver already signed", 400, false);
                    contract.CounterpartySigned = true;
                    contract.CounterpartySignAt = DateTime.UtcNow;
                }

                // 5. Cập nhật trạng thái Hợp đồng & Trip
                if (contract.OwnerSigned && contract.CounterpartySigned)
                {
                    // --- Cả hai đã ký ---
                    contract.Status = ContractStatus.COMPLETED; // Hoặc ACTIVE tùy Enum của bạn
                    contract.EffectiveDate = DateTime.UtcNow;

                    // Cập nhật trạng thái Trip
                    trip.Status = TripStatus.DONE_ASSIGNING_DRIVER; // Hoàn tất gán tài xế
                    trip.UpdateAt = DateTime.UtcNow;
                }
                else
                {
                    // --- Mới chỉ có 1 bên ký ---
                    contract.Status = ContractStatus.AWAITING_CONTRACT_SIGNATURE; // Chờ bên kia ký

                    // Trip vẫn ở trạng thái chờ ký
                    // trip.Status = TripStatus.AWAITING_DRIVER_CONTRACT; // Giữ nguyên hoặc set lại cho chắc
                }

                // 6. Lưu thay đổi (Transaction: Token + Contract + Trip)
                await _unitOfWork.TripDriverContractRepo.UpdateAsync(contract);
                await _unitOfWork.TripRepo.UpdateAsync(trip);

                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Driver Contract signed successfully", 200, true, new
                {
                    contract.ContractId,
                    ContractStatus = contract.Status.ToString(),
                    contract.OwnerSigned,
                    contract.CounterpartySigned, // Driver Signed
                    TripStatus = trip.Status.ToString()
                });
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
        public async Task<TripDriverContract> CreateContractInternalAsync(Guid tripId, Guid ownerId, Guid driverId, decimal? fare)
        {
            try
            {
                // 1. Validate Trip
                var trip = await _unitOfWork.TripRepo.GetByIdAsync(tripId);
                if (trip == null || trip.OwnerId != ownerId)
                    throw new Exception("Trip not found or not owned by current user");

                // 2. Validate Driver
                var driver = await _unitOfWork.DriverRepo.GetByIdAsync(driverId);
                if (driver == null)
                    throw new Exception("Driver not found");

                // 3. Lấy Template (Loại DRIVER_CONTRACT)
                var template = (await _unitOfWork.ContractTemplateRepo.GetAllAsync(
                    filter: t => t.Type == ContractType.DRIVER_CONTRACT,
                    orderBy: q => q.OrderByDescending(x => x.Version)
                )).FirstOrDefault();

                if (template == null)
                    throw new Exception("No Driver Contract Template found");

                // 4. Kiểm tra xem hợp đồng đã tồn tại chưa (Tránh tạo trùng)
                var existingContract = (await _unitOfWork.TripDriverContractRepo.GetAllAsync(
                    filter: c => c.TripId == tripId && c.CounterpartyId == driver.UserId
                )).FirstOrDefault();

                if (existingContract != null)
                {
                    return existingContract; // Nếu có rồi thì trả về cái cũ
                }

                // 5. Tạo hợp đồng (AUTO-SIGN)
                var contract = new TripDriverContract
                {
                    ContractId = Guid.NewGuid(),
                    ContractCode = $"CON-DRV-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                    TripId = trip.TripId,
                    OwnerId = ownerId,
                    CounterpartyId = driver.UserId, // Driver kế thừa BaseUser -> UserId
                    ContractTemplateId = template.ContractTemplateId,
                    Version = template.Version,
                    Type = ContractType.DRIVER_CONTRACT,

                    // --- TRẠNG THÁI: ĐÃ KÝ (ACTIVE) ---
                    Status = ContractStatus.AWAITING_CONTRACT_SIGNATURE,
                    CreateAt = DateTime.UtcNow,
                    EffectiveDate = DateTime.UtcNow,

                    // Owner ký
                    OwnerSigned = true,
                    OwnerSignAt = DateTime.UtcNow,

                    // Driver (Counterparty) ký
                    CounterpartySigned = false,
                    CounterpartySignAt = null,

                    // Giá trị hợp đồng (Lấy từ Assignment nếu có)
                    ContractValue = fare ?? 0,
                    Currency = "VND"
                };

                // 6. Add vào UoW (KHÔNG SAVE - Để Transaction bên ngoài lo)
                await _unitOfWork.TripDriverContractRepo.AddAsync(contract);

                trip.Status = TripStatus.PENDING_DRIVER_ASSIGNMENT;
                trip.UpdateAt = DateTime.UtcNow;
                await _unitOfWork.TripRepo.UpdateAsync(trip);

                return contract;
            }
            catch (Exception ex)
            {
                // Ném lỗi để Service gọi (TripDriverAssignmentService) có thể Rollback
                throw new Exception($"Error creating internal driver contract: {ex.Message}", ex);
            }
        }

        public async Task<ResponseDTO> GetAllAsync(int pageNumber, int pageSize)
        {
            try
            {
                // 1. Lấy thông tin User
                var userId = _userUtility.GetUserIdFromToken();
                var userRole = _userUtility.GetUserRoleFromToken(); // (Giả định bạn có hàm này)
                if (userId == Guid.Empty)
                    return new ResponseDTO("Unauthorized: Invalid token", 401, false);

                // 2. Lấy IQueryable cơ sở
                var query = _unitOfWork.TripDriverContractRepo.GetAll()
                                     .AsNoTracking();

                // 3. Lọc theo Vai trò (Authorization)
                if (userRole == "Owner")
                {
                    query = query.Where(c => c.OwnerId == userId);
                }
                else if (userRole == "Driver")
                {
                    query = query.Where(c => c.CounterpartyId == userId);
                }
                else if (userRole == "Admin")
                {
                    // Admin: không cần lọc, thấy tất cả
                }
                else
                {
                    // Các vai trò khác (ví dụ: Provider) không được xem
                    return new ResponseDTO("Forbidden: You do not have permission", 403, false);
                }

                // 4. Đếm tổng số lượng (sau khi lọc)
                var totalCount = await query.CountAsync();

                // 5. Lấy dữ liệu của trang và Map sang DTO
                // (Dùng DTO tóm tắt, giống DTO trả về của hàm Create)
                var contractsDto = await query
                    .Include(c => c.Trip)
                    .Include(c => c.Owner)
                    .Include(c => c.Counterparty) // (Driver)
                    .Include(c => c.ContractTemplate)
                    .OrderByDescending(c => c.CreateAt) // Sắp xếp mới nhất trước
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => new TripDriverContractDTO // Map trên DB
                    {
                        ContractId = c.ContractId,
                        ContractCode = c.ContractCode,
                        TripId = c.TripId,
                        TripCode = c.Trip != null ? c.Trip.TripCode : "N/A",
                        OwnerId = c.OwnerId,
                        OwnerName = c.Owner != null ? c.Owner.FullName : "N/A",
                        DriverId = c.CounterpartyId,
                        DriverName = c.Counterparty != null ? c.Counterparty.FullName : "N/A",
                        ContractTemplateId = c.ContractTemplateId,
                        TemplateName = c.ContractTemplate != null ? c.ContractTemplate.ContractTemplateName : "N/A",
                        Version = c.Version,
                        Status = c.Status,
                        Type = c.Type,
                        CreateAt = c.CreateAt,
                        EffectiveDate = c.EffectiveDate,
                        OwnerSigned = c.OwnerSigned,
                        DriverSigned = c.CounterpartySigned
                    })
                    .ToListAsync();

                // 6. Tạo kết quả PaginatedDTO
                var paginatedResult = new PaginatedDTO<TripDriverContractDTO>(contractsDto, totalCount, pageNumber, pageSize);

                return new ResponseDTO("Retrieved contracts successfully", 200, true, paginatedResult);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error getting contracts: {ex.Message}", 500, false);
            }
        }
    }
}
