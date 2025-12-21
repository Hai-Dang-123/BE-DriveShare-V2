using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.Enums.Type;
using DAL.Entities;
using DAL.UnitOfWork;
using MailKit.Net.Imap;
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
        private readonly IEmailService _emailService;

        public TripProviderContractService(IUnitOfWork unitOfWork, UserUtility userUtility, IEmailService emailService)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
            _emailService = emailService;
        }

        // ============================================================
        // 🧩 1️⃣ CREATE CONTRACT (Owner tạo hợp đồng với Provider)
        // ============================================================
        public async Task<ResponseDTO> CreateAsync(CreateTripProviderContractDTO dto)
        {
            try
            {
                var ownerId = _userUtility.GetUserIdFromToken();
                if (ownerId == Guid.Empty)
                    return new ResponseDTO("Unauthorized or invalid token", 401, false);

                var trip = await _unitOfWork.TripRepo.GetByIdAsync(dto.TripId);
                if (trip == null) return new ResponseDTO("Trip not found", 404, false);

                var provider = await _unitOfWork.ProviderRepo.GetByIdAsync(dto.ProviderId);
                if (provider == null) return new ResponseDTO("Provider not found", 404, false);

                var template = (await _unitOfWork.ContractTemplateRepo.GetAllAsync(
                    filter: t => t.Type == ContractType.PROVIDER_CONTRACT,
                    orderBy: q => q.OrderByDescending(t => t.Version)
                )).FirstOrDefault();

                if (template == null) return new ResponseDTO("No Provider Contract Template found", 404, false);

                var existingContract = (await _unitOfWork.TripProviderContractRepo.GetAllAsync(
                    filter: c => c.TripId == trip.TripId && c.CounterpartyId == provider.UserId
                )).FirstOrDefault();

                if (existingContract != null)
                    return new ResponseDTO("Contract already exists for this trip and provider", 400, false);

                var contract = new TripProviderContract
                {
                    ContractId = Guid.NewGuid(),
                    ContractCode = $"CON-PROV-{TimeUtil.NowVN():yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                    TripId = trip.TripId,
                    OwnerId = ownerId,
                    CounterpartyId = provider.UserId,
                    ContractTemplateId = template.ContractTemplateId,
                    Version = template.Version,
                    Type = ContractType.PROVIDER_CONTRACT,
                    Status = ContractStatus.PENDING,
                    CreateAt = TimeUtil.NowVN()
                };

                await _unitOfWork.TripProviderContractRepo.AddAsync(contract);
                await _unitOfWork.SaveChangeAsync();

                // [FIX MAPPING DTO]
                var result = new TripProviderContractDTO
                {
                    ContractId = contract.ContractId,
                    ContractCode = contract.ContractCode,
                    TripId = contract.TripId,
                    TripCode = trip.TripCode ?? "",

                    // Thay thế OwnerId/ProviderId bằng PartyA/PartyB
                    PartyA = new ContractPartyDTO
                    {
                        UserId = ownerId,
                        FullName = trip.Owner != null ? trip.Owner.FullName : "Owner Info Missing",
                        Role = "Owner"
                    },

                    PartyB = new ContractPartyDTO
                    {
                        UserId = provider.UserId,
                        FullName = provider.FullName,
                        CompanyName = provider.CompanyName, // Provider có CompanyName
                        TaxCode = provider.TaxCode,
                        Role = "Provider"
                    },

                    ContractTemplateId = contract.ContractTemplateId ?? Guid.Empty,
                    TemplateName = template.ContractTemplateName,
                    Version = contract.Version,

                    // [FIX ENUM -> STRING]
                    Status = contract.Status.ToString(),
                    Type = contract.Type.ToString(),

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

                // 1. Tìm Token OTP hợp lệ trong DB của User này
                var validToken = await _unitOfWork.UserTokenRepo.GetAll()
                    .Where(t => t.UserId == userId
                             && t.TokenType == TokenType.CONTRACT_SIGNING_OTP // Enum bạn đã thêm
                             && !t.IsRevoked
                             && t.ExpiredAt > TimeUtil.NowVN()) // Chưa hết hạn
                    .OrderByDescending(t => t.CreatedAt) // Lấy cái mới nhất
                    .FirstOrDefaultAsync();

                if (validToken == null)
                {
                    return new ResponseDTO("Mã OTP không tồn tại hoặc đã hết hạn. Vui lòng lấy mã mới.", 400, false);
                }

                // 2. Verify mã OTP (So sánh mã nhập vào với mã Hash trong DB)
                bool isOtpCorrect = BCrypt.Net.BCrypt.Verify(dto.Otp, validToken.TokenValue);

                if (!isOtpCorrect)
                {
                    // Tùy chọn: Có thể đếm số lần sai để khóa tạm thời (Advanced feature)
                    return new ResponseDTO("Mã OTP không chính xác.", 400, false);
                }

                // 3. Hủy hiệu lực Token ngay lập tức (Để không dùng lại được lần 2)
                validToken.IsRevoked = true;
                await _unitOfWork.UserTokenRepo.UpdateAsync(validToken);

                // ========================================================================
                // 📝 BƯỚC TIẾP THEO: LOGIC KÝ HỢP ĐỒNG (CODE CŨ CỦA BẠN)
                // ========================================================================

                // 1. Lấy Hợp đồng
                var contract = await _unitOfWork.TripProviderContractRepo.GetByIdAsync(dto.ContractId);
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
                    contract.OwnerSignAt = TimeUtil.NowVN();
                }
                else if (isProvider)
                {
                    if (contract.CounterpartySigned)
                        return new ResponseDTO("Provider already signed", 400, false);
                    contract.CounterpartySigned = true;
                    contract.CounterpartySignAt = TimeUtil.NowVN();
                }

                // ⚠️ 5. Cập nhật trạng thái (Contract VÀ Trip) dựa trên chữ ký
                if (contract.OwnerSigned && contract.CounterpartySigned)
                {
                    // --- Cả hai đã ký ---
                    contract.Status = ContractStatus.COMPLETED;
                    contract.EffectiveDate = TimeUtil.NowVN();

                    trip.Status = TripStatus.PENDING_DRIVER_ASSIGNMENT;
                }
                else
                {
                    // --- Mới chỉ có 1 bên ký ---
                    contract.Status = ContractStatus.AWAITING_CONTRACT_SIGNATURE;

                    //// Có thể giữ nguyên hoặc cập nhật lại cho chắc
                    trip.Status = TripStatus.AWAITING_OWNER_CONTRACT;
                }

                // 6. Lưu thay đổi (Transaction: Token + Contract + Trip cùng lúc)
                await _unitOfWork.TripProviderContractRepo.UpdateAsync(contract);
                await _unitOfWork.TripRepo.UpdateAsync(trip);

                // UnitOfWork sẽ commit cả việc Revoke Token và Update Contract cùng lúc
                // Nếu update contract lỗi -> Token cũng không bị revoke (an toàn tuyệt đối)
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Contract signed successfully", 200, true, new
                {
                    contract.ContractId,
                    ContractStatus = contract.Status.ToString(),
                    contract.OwnerSigned,
                    contract.CounterpartySigned,
                    TripStatus = trip.Status.ToString()
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
        // ============================================================
        // 🧩 GET CONTRACT BY ID (Full Detail for UI)
        // ============================================================
        public async Task<ResponseDTO> GetByIdAsync(Guid contractId)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                    return new ResponseDTO("Unauthorized or invalid token", 401, false);

                // Include Owner và Counterparty (Provider)
                var contracts = await _unitOfWork.TripProviderContractRepo.GetAllAsync(
                    filter: c => c.ContractId == contractId,
                    includeProperties: "Owner,Counterparty,Trip,ContractTemplate.ContractTerms"
                );

                var contract = contracts.FirstOrDefault();
                if (contract == null) return new ResponseDTO("Contract not found", 404, false);

                // Cast Counterparty sang Provider để lấy thông tin doanh nghiệp
                var providerEntity = contract.Counterparty as DAL.Entities.Provider;

                // 1. Map Contract Info
                var contractDto = new TripProviderContractDTO
                {
                    ContractId = contract.ContractId,
                    ContractCode = contract.ContractCode,
                    TripId = contract.TripId,
                    TripCode = contract.Trip?.TripCode ?? string.Empty,
                    ContractTemplateId = contract.ContractTemplateId ?? Guid.Empty,
                    TemplateName = contract.ContractTemplate?.ContractTemplateName ?? string.Empty,
                    Version = contract.Version,
                    ContractValue = contract.ContractValue,
                    Currency = contract.Currency,
                    Status = contract.Status.ToString(),
                    Type = contract.Type.ToString(),

                    OwnerSigned = contract.OwnerSigned,
                    OwnerSignAt = contract.OwnerSignAt,
                    ProviderSigned = contract.CounterpartySigned,
                    ProviderSignAt = contract.CounterpartySignAt,

                    FileURL = contract.FileURL,
                    CreateAt = contract.CreateAt,
                    EffectiveDate = contract.EffectiveDate,
                    ExpirationDate = contract.ExpirationDate,
                    Note = contract.Note,

                    // [MAPPING BÊN A: OWNER]
                    PartyA = new ContractPartyDTO
                    {
                        UserId = contract.OwnerId,
                        FullName = contract.Owner?.FullName ?? "N/A",
                        CompanyName = contract.Owner?.CompanyName ?? "",
                        TaxCode = contract.Owner?.TaxCode ?? "",
                        PhoneNumber = contract.Owner?.PhoneNumber ?? "",
                        Email = contract.Owner?.Email ?? "",
                        Address = contract.Owner?.BusinessAddress != null ? contract.Owner.BusinessAddress.Address : "N/A",
                        Role = "Owner"
                    },

                    // [MAPPING BÊN B: PROVIDER]
                    PartyB = new ContractPartyDTO
                    {
                        UserId = contract.CounterpartyId,
                        FullName = contract.Counterparty?.FullName ?? "N/A",
                        // Lấy thông tin từ Provider Entity
                        CompanyName = providerEntity?.CompanyName ?? "",
                        TaxCode = providerEntity?.TaxCode ?? "",
                        PhoneNumber = contract.Counterparty?.PhoneNumber ?? "",
                        Email = contract.Counterparty?.Email ?? "",
                        Address = providerEntity?.BusinessAddress != null ? providerEntity.BusinessAddress.Address : "N/A",
                        Role = "Provider"
                    }
                };

                // 2. Map Template & Terms
                var templateDto = new ContractTemplateDTO
                {
                    ContractTemplateId = contract.ContractTemplate?.ContractTemplateId ?? Guid.Empty,
                    ContractTemplateName = contract.ContractTemplate?.ContractTemplateName ?? string.Empty,
                    Version = contract.ContractTemplate?.Version ?? string.Empty,
                    CreatedAt = contract.ContractTemplate?.CreatedAt ?? DateTime.MinValue,
                    Type = contract.ContractTemplate?.Type ?? Common.Enums.Type.ContractType.PROVIDER_CONTRACT
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

                // 3. Return Full Detail
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
                ContractCode = $"CON-PROV-{TimeUtil.NowVN():yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                TripId = tripId,         // Từ tham số
                OwnerId = ownerId,      // Từ tham số
                CounterpartyId = providerId, // Từ tham số
                ContractTemplateId = template.ContractTemplateId,
                Version = template.Version,
                Type = ContractType.PROVIDER_CONTRACT,
                Status = ContractStatus.PENDING, // Chờ ký
                CreateAt = TimeUtil.NowVN(),
                OwnerSigned = false,
                OwnerSignAt = null,
                CounterpartySigned = true,
                CounterpartySignAt = TimeUtil.NowVN(),
                // --- Gán giá trị từ tham số ---
                ContractValue = fare,
                Currency = "VND" // Giả định
            };

            // 🔹 Thêm vào UoW (KHÔNG SAVE)
            await _unitOfWork.TripProviderContractRepo.AddAsync(contract);
        }

        // ============================================================
        // 🧩 3️⃣ GET ALL (Fix Mapping Select)
        // ============================================================
        public async Task<ResponseDTO> GetAllAsync(int pageNumber, int pageSize)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                var userRole = _userUtility.GetUserRoleFromToken();
                if (userId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                var query = _unitOfWork.TripProviderContractRepo.GetAll().AsNoTracking();

                if (userRole == "Owner") query = query.Where(c => c.OwnerId == userId);
                else if (userRole == "Provider") query = query.Where(c => c.CounterpartyId == userId);
                else if (userRole != "Admin") return new ResponseDTO("Forbidden", 403, false);

                var totalCount = await query.CountAsync();

                // [FIX MAPPING TRONG SELECT]
                var contractsDto = await query
                    .Include(c => c.Trip)
                    .Include(c => c.Owner)
                    .Include(c => c.Counterparty)
                    .Include(c => c.ContractTemplate)
                    .OrderByDescending(c => c.CreateAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => new TripProviderContractDTO
                    {
                        ContractId = c.ContractId,
                        ContractCode = c.ContractCode,
                        TripId = c.TripId,
                        TripCode = c.Trip != null ? c.Trip.TripCode : "N/A",

                        // [FIX] Map PartyA (Owner)
                        PartyA = new ContractPartyDTO
                        {
                            UserId = c.OwnerId,
                            FullName = c.Owner != null ? c.Owner.FullName : "N/A",
                            CompanyName = c.Owner != null ? c.Owner.CompanyName : "",
                            Role = "Owner"
                        },

                        // [FIX] Map PartyB (Provider)
                        // Lưu ý: Trong LINQ to Entities, ép kiểu (as Provider) có thể gây lỗi.
                        // Tốt nhất là lấy các trường chung từ BaseUser (Counterparty)
                        PartyB = new ContractPartyDTO
                        {
                            UserId = c.CounterpartyId,
                            FullName = c.Counterparty != null ? c.Counterparty.FullName : "N/A",
                            // Nếu muốn CompanyName, cần chắc chắn Counterparty là Provider
                            // EF Core có thể không hỗ trợ 'as Provider' trong Select.
                            // Giải pháp: Chấp nhận lấy thông tin cơ bản ở list view, hoặc dùng discriminator
                            Role = "Provider"
                        },

                        ContractTemplateId = c.ContractTemplateId ?? Guid.Empty,
                        TemplateName = c.ContractTemplate != null ? c.ContractTemplate.ContractTemplateName : "N/A",
                        Version = c.Version,

                        // [FIX] Enum -> String
                        Status = c.Status.ToString(),
                        Type = c.Type.ToString(),

                        CreateAt = c.CreateAt,
                        EffectiveDate = c.EffectiveDate,
                        OwnerSigned = c.OwnerSigned,
                        ProviderSigned = c.CounterpartySigned
                    })
                    .ToListAsync();

                var paginatedResult = new PaginatedDTO<TripProviderContractDTO>(contractsDto, totalCount, pageNumber, pageSize);
                return new ResponseDTO("Retrieved provider contracts successfully", 200, true, paginatedResult);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error getting provider contracts: {ex.Message}", 500, false);
            }
        }

        public async Task<ResponseDTO> SendOTPToSignContract(Guid contractId)
        {
            try
            {
                // 1. Lấy UserId từ Token
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                    return new ResponseDTO("Lỗi xác thực: Không tìm thấy User ID.", 401, false);

                // 2. Lấy thông tin User (để gửi Email)
                var user = await _unitOfWork.BaseUserRepo.GetByIdAsync(userId);
                if (user == null)
                    return new ResponseDTO("Không tìm thấy thông tin người dùng.", 404, false);

                // 3. Lấy thông tin Hợp đồng
                // Cần lấy Contract ra để check quyền sở hữu
                var contract = await _unitOfWork.BaseContractRepo.GetByIdAsync(contractId);
                if (contract == null)
                    return new ResponseDTO("Hợp đồng không tồn tại.", 404, false);

                // 4. Kiểm tra Quyền và Trạng thái đã ký
                bool isOwner = contract.OwnerId == userId;
                bool isCounterparty = false;

                // Kiểm tra nếu là TripDriverContract để check Counterparty (Tài xế)
                if (contract is TripDriverContract tripContract)
                {
                    if (tripContract.CounterpartyId == userId) isCounterparty = true;
                }

                // Nếu không phải Owner cũng không phải Driver -> Chặn
                if (!isOwner && !isCounterparty)
                {
                    return new ResponseDTO("Bạn không có quyền thực hiện ký kết trên hợp đồng này.", 403, false);
                }

                // Kiểm tra xem đã ký chưa (Tránh spam OTP khi đã ký rồi)
                if (isOwner && contract.OwnerSigned)
                    return new ResponseDTO("Bạn đã ký hợp đồng này rồi, không cần lấy OTP nữa.", 400, false);

                if (isCounterparty && contract.CounterpartySigned)
                    return new ResponseDTO("Bạn đã ký hợp đồng này rồi, không cần lấy OTP nữa.", 400, false);

                // 5. Tạo mã OTP (6 số ngẫu nhiên)
                string rawOtp = new Random().Next(100000, 999999).ToString();

                // 6. Hash OTP để lưu vào DB (Bảo mật: Admin vào DB cũng không biết OTP là gì)
                string hashedOtp = BCrypt.Net.BCrypt.HashPassword(rawOtp);

                // 7. Xử lý Token cũ (Revoke các OTP cũ chưa dùng của user này cho loại ký hợp đồng)
                // Để tránh việc user request 10 lần có 10 mã hiệu lực cùng lúc
                var oldTokens = await _unitOfWork.UserTokenRepo.GetAll()
                    .Where(t => t.UserId == userId
                             && t.TokenType == TokenType.CONTRACT_SIGNING_OTP
                             && !t.IsRevoked)
                    .ToListAsync();

                foreach (var t in oldTokens)
                {
                    t.IsRevoked = true; // Hủy hiệu lực token cũ
                }
                // Không SaveChange ngay để gộp transaction bên dưới

                // 8. Tạo UserToken mới
                var newToken = new UserToken
                {
                    UserTokenId = Guid.NewGuid(),
                    UserId = userId,
                    TokenType = TokenType.CONTRACT_SIGNING_OTP,
                    TokenValue = hashedOtp, // Lưu bản mã hóa
                    CreatedAt = TimeUtil.NowVN(),
                    ExpiredAt = TimeUtil.NowVN().AddMinutes(5), // Hết hạn sau 5 phút
                    IsRevoked = false,
                };

                await _unitOfWork.UserTokenRepo.AddAsync(newToken);

                // Cập nhật Token cũ (nếu có) và Thêm Token mới cùng lúc
                if (oldTokens.Any()) _unitOfWork.UserTokenRepo.UpdateRange(oldTokens);

                await _unitOfWork.SaveChangeAsync();

                // 9. Gửi Email (Gọi hàm Design Hoành tráng)
                // rawOtp: Gửi mã thô cho user (để họ đọc)
                // contract.ContractCode: Mã hợp đồng để hiển thị trong email
                await _emailService.SendContractSigningOtpAsync(user.Email, user.FullName, rawOtp, contract.ContractCode);

                // 10. Trả về kết quả (Giấu số điện thoại/Email đi cho gọn)
                return new ResponseDTO($"Mã OTP xác thực đã được gửi đến email {HideEmail(user.Email)}", 200, true);
            }
            catch (Exception ex)
            {
                return new ResponseDTO("Lỗi hệ thống khi tạo OTP: " + ex.Message, 500, false);
            }
        }

        // Hàm phụ để che bớt email (vui vẻ tí cho UI đẹp)
        // user@gmail.com -> u***@gmail.com
        private string HideEmail(string email)
        {
            if (string.IsNullOrEmpty(email)) return "";
            var parts = email.Split('@');
            if (parts.Length != 2) return email;
            if (parts[0].Length <= 2) return email;

            return $"{parts[0].Substring(0, 1)}***@{parts[1]}";
        }
    }
}
