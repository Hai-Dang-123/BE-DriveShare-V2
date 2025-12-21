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
                    ContractCode = $"CON-DRV-{TimeUtil.NowVN():yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                    TripId = trip.TripId,
                    OwnerId = ownerId,
                    CounterpartyId = driver.UserId,
                    ContractTemplateId = template.ContractTemplateId,
                    Version = template.Version,
                    Type = ContractType.DRIVER_CONTRACT,
                    Status = ContractStatus.PENDING,
                    CreateAt = TimeUtil.NowVN()
                };

                await _unitOfWork.TripDriverContractRepo.AddAsync(contract);

                // Chuyển status trip
                trip.Status = TripStatus.PENDING_DRIVER_ASSIGNMENT;
                trip.UpdateAt = TimeUtil.NowVN();
                await _unitOfWork.TripRepo.UpdateAsync(trip);

                await _unitOfWork.SaveChangeAsync();

                // [FIX LỖI MAPPING DTO TẠI ĐÂY]
                var dtoOut = new TripDriverContractDTO
                {
                    ContractId = contract.ContractId,
                    ContractCode = contract.ContractCode,
                    TripId = trip.TripId,
                    TripCode = trip.TripCode,

                    // Thay thế các trường OwnerId, OwnerName... bằng PartyA/PartyB
                    PartyA = new ContractPartyDTO
                    {
                        UserId = ownerId,
                        // Lưu ý: trip.Owner có thể null nếu chưa Include, nên check null
                        FullName = trip.Owner != null ? trip.Owner.FullName : "Owner Info Missing",
                        Role = "Owner"
                        // Các trường khác như CompanyName, TaxCode có thể map nếu trip.Owner đã được include
                    },

                    PartyB = new ContractPartyDTO
                    {
                        UserId = driver.UserId,
                        FullName = driver.FullName,
                        Role = "Driver"
                    },

                    ContractTemplateId = contract.ContractTemplateId.Value,
                    TemplateName = template.ContractTemplateName,
                    Version = contract.Version,

                    // [FIX LỖI ENUM -> STRING]
                    Status = contract.Status.ToString(),
                    Type = contract.Type.ToString(),

                    CreateAt = contract.CreateAt
                };

                return new ResponseDTO("Create driver contract successfully", 200, true, dtoOut);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error creating driver contract: {ex.Message}", 500, false);
            }
        }

        // 2) SIGN CONTRACT (Owner hoặc Driver ký)
        //    - Logic: Ký xong hợp đồng này -> Check xem còn hợp đồng nào khác chưa ký không?
        //    - Nếu hết rồi -> Trip chuyển status DONE
        //    - Nếu chưa -> Trip vẫn PENDING
        // =========================================================
        public async Task<ResponseDTO> SignAsync(SignContractDTO dto)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                    return new ResponseDTO("Unauthorized", 401, false);

                // ========================================================================
                // 🛡️ BƯỚC 0: XÁC THỰC OTP
                // ========================================================================

                // 1. Tìm Token OTP hợp lệ
                var validToken = await _unitOfWork.UserTokenRepo.GetAll()
                    .Where(t => t.UserId == userId
                                && t.TokenType == TokenType.CONTRACT_SIGNING_OTP
                                && !t.IsRevoked
                                && t.ExpiredAt > TimeUtil.NowVN())
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
                // 📝 BƯỚC TIẾP THEO: LOGIC KÝ HỢP ĐỒNG & CẬP NHẬT TRẠNG THÁI
                // ========================================================================

                // 1. Lấy Hợp đồng
                var contract = await _unitOfWork.TripDriverContractRepo.GetByIdAsync(dto.ContractId);
                if (contract == null)
                    return new ResponseDTO("Contract not found", 404, false);

                // 2. Lấy Chuyến đi (Trip) liên quan
                var trip = await _unitOfWork.TripRepo.GetByIdAsync(contract.TripId);
                if (trip == null)
                    return new ResponseDTO("Associated Trip not found for this contract", 404, false);

                // 3. Xác thực quyền (Người ký phải là Owner hoặc Driver của hợp đồng này)
                bool isOwner = contract.OwnerId == userId;
                bool isDriver = contract.CounterpartyId == userId;

                if (!isOwner && !isDriver)
                    return new ResponseDTO("You are not authorized to sign this contract", 403, false);

                // 4. Áp dụng chữ ký
                if (isOwner)
                {
                    if (contract.OwnerSigned)
                        return new ResponseDTO("Owner already signed", 400, false);
                    contract.OwnerSigned = true;
                    contract.OwnerSignAt = TimeUtil.NowVN();
                }
                else if (isDriver)
                {
                    if (contract.CounterpartySigned)
                        return new ResponseDTO("Driver already signed", 400, false);
                    contract.CounterpartySigned = true;
                    contract.CounterpartySignAt = TimeUtil.NowVN();
                }

                // 5. Cập nhật trạng thái HỢP ĐỒNG HIỆN TẠI
                bool isCurrentContractDone = false;
                if (contract.OwnerSigned && contract.CounterpartySigned)
                {
                    // Cả 2 bên đã ký -> Hợp đồng hoàn tất
                    contract.Status = ContractStatus.COMPLETED;
                    contract.EffectiveDate = TimeUtil.NowVN();
                    isCurrentContractDone = true;
                }
                else
                {
                    // Vẫn thiếu 1 bên -> Chờ tiếp
                    contract.Status = ContractStatus.AWAITING_CONTRACT_SIGNATURE;
                }

                // 6. Cập nhật trạng thái CHUYẾN ĐI (TRIP)
                // Logic: Chỉ khi hợp đồng này xong VÀ tất cả các hợp đồng khác trong chuyến cũng xong -> Trip mới Done
                if (isCurrentContractDone)
                {
                    // Kiểm tra xem còn hợp đồng nào CỦA CHUYẾN NÀY mà chưa xong không?
                    // Lưu ý: Loại trừ chính cái 'contract' đang xử lý (vì nó chưa save vào DB là completed)
                    bool areOtherContractsPending = await _unitOfWork.TripDriverContractRepo.AnyAsync(
                        c => c.TripId == trip.TripId
                             && c.ContractId != contract.ContractId // Không tính cái đang ký
                             && c.Status != ContractStatus.COMPLETED // Tìm cái nào chưa xong
                    );

                    if (!areOtherContractsPending)
                    {
                        // Không còn ai chưa ký -> Tất cả đã xong -> Chuyển trạng thái Trip
                        // Trip chuyển sang trạng thái đã gán xong tài xế (hoặc sẵn sàng bàn giao xe)
                        trip.Status = TripStatus.DONE_ASSIGNING_DRIVER;
                        trip.UpdateAt = TimeUtil.NowVN();
                    }
                    else
                    {
                        // Vẫn còn ông khác chưa ký -> Trip vẫn phải chờ
                        trip.Status = TripStatus.PENDING_DRIVER_ASSIGNMENT;
                    }
                }
                else
                {
                    // Hợp đồng này chưa xong thì chắc chắn Trip chưa xong
                    trip.Status = TripStatus.PENDING_DRIVER_ASSIGNMENT;
                }

                // 7. Lưu thay đổi
                await _unitOfWork.TripDriverContractRepo.UpdateAsync(contract);
                await _unitOfWork.TripRepo.UpdateAsync(trip);
                await _unitOfWork.SaveChangeAsync();

                return new ResponseDTO("Driver Contract signed successfully", 200, true, new
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
        // =========================================================
        // 3) GET BY ID (Contract + Template + Terms)
        // =========================================================
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

                var contracts = await _unitOfWork.TripDriverContractRepo.GetAllAsync(
                    filter: c => c.ContractId == contractId,
                    includeProperties: "Owner,Counterparty,Trip,ContractTemplate.ContractTerms"
                );

                var contract = contracts.FirstOrDefault();
                if (contract == null) return new ResponseDTO("Contract not found", 404, false);

                // Cast Counterparty sang Driver để lấy bằng lái
                var driverEntity = contract.Counterparty as DAL.Entities.Driver;

                // 1. Map Contract Info
                var contractDto = new TripDriverContractDTO
                {
                    ContractId = contract.ContractId,
                    ContractCode = contract.ContractCode,
                    TripId = contract.TripId,
                    TripCode = contract.Trip?.TripCode ?? string.Empty,
                    ContractTemplateId = contract.ContractTemplateId.Value,
                    TemplateName = contract.ContractTemplate?.ContractTemplateName ?? string.Empty,
                    Version = contract.Version,
                    ContractValue = contract.ContractValue,
                    Currency = contract.Currency,
                    Status = contract.Status.ToString(),
                    Type = contract.Type.ToString(),

                    OwnerSigned = contract.OwnerSigned,
                    OwnerSignAt = contract.OwnerSignAt,
                    DriverSigned = contract.CounterpartySigned,
                    DriverSignAt = contract.CounterpartySignAt,

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

                    // [MAPPING BÊN B: DRIVER]
                    PartyB = new ContractPartyDTO
                    {
                        UserId = contract.CounterpartyId,
                        FullName = contract.Counterparty?.FullName ?? "N/A",
                        PhoneNumber = contract.Counterparty?.PhoneNumber ?? "",
                        Email = contract.Counterparty?.Email ?? "",
                        // Driver dùng Address thường trú
                        Address = contract.Counterparty?.Address != null ? contract.Counterparty.Address.Address : "N/A",
                        // Lấy số bằng lái từ Entity Driver
                        LicenseNumber = driverEntity?.LicenseNumber ?? "N/A",
                        CompanyName = "", // Driver không có cty
                        TaxCode = "",
                        Role = "Driver"
                    }
                };

                // 2. Map Template & Terms
                var templateDto = new ContractTemplateDTO
                {
                    ContractTemplateId = contract.ContractTemplate?.ContractTemplateId ?? Guid.Empty,
                    ContractTemplateName = contract.ContractTemplate?.ContractTemplateName ?? string.Empty,
                    Version = contract.ContractTemplate?.Version ?? string.Empty,
                    CreatedAt = contract.ContractTemplate?.CreatedAt ?? DateTime.MinValue,
                    Type = contract.ContractTemplate?.Type ?? Common.Enums.Type.ContractType.DRIVER_CONTRACT
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
                    ContractCode = $"CON-DRV-{TimeUtil.NowVN():yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..6].ToUpper()}",
                    TripId = trip.TripId,
                    OwnerId = ownerId,
                    CounterpartyId = driver.UserId, // Driver kế thừa BaseUser -> UserId
                    ContractTemplateId = template.ContractTemplateId,
                    Version = template.Version,
                    Type = ContractType.DRIVER_CONTRACT,

                    // --- TRẠNG THÁI: ĐÃ KÝ (ACTIVE) ---
                    Status = ContractStatus.AWAITING_CONTRACT_SIGNATURE,
                    CreateAt = TimeUtil.NowVN(),
                    EffectiveDate = TimeUtil.NowVN(),

                    // Owner ký
                    OwnerSigned = true,
                    OwnerSignAt = TimeUtil.NowVN(),

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
                trip.UpdateAt = TimeUtil.NowVN();
                await _unitOfWork.TripRepo.UpdateAsync(trip);

                return contract;
            }
            catch (Exception ex)
            {
                // Ném lỗi để Service gọi (TripDriverAssignmentService) có thể Rollback
                throw new Exception($"Error creating internal driver contract: {ex.Message}", ex);
            }
        }

        // =========================================================
        // 3) GET ALL CONTRACTS (PAGING)
        // =========================================================
        public async Task<ResponseDTO> GetAllAsync(int pageNumber, int pageSize)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                var userRole = _userUtility.GetUserRoleFromToken();
                if (userId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                var query = _unitOfWork.TripDriverContractRepo.GetAll().AsNoTracking();

                if (userRole == "Owner") query = query.Where(c => c.OwnerId == userId);
                else if (userRole == "Driver") query = query.Where(c => c.CounterpartyId == userId);
                else if (userRole != "Admin") return new ResponseDTO("Forbidden", 403, false);

                var totalCount = await query.CountAsync();

                // [FIX LỖI MAPPING TRONG SELECT]
                // EF Core không hỗ trợ tạo object phức tạp (như ContractPartyDTO) bên trong .Select() nếu logic quá phức tạp.
                // Tuy nhiên, với cấu trúc đơn giản thì vẫn được.

                var contractsDto = await query
                    .Include(c => c.Trip)
                    .Include(c => c.Owner)
                    .Include(c => c.Counterparty)
                    .Include(c => c.ContractTemplate)
                    .OrderByDescending(c => c.CreateAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(c => new TripDriverContractDTO
                    {
                        ContractId = c.ContractId,
                        ContractCode = c.ContractCode,
                        TripId = c.TripId,
                        TripCode = c.Trip != null ? c.Trip.TripCode : "N/A",

                        // [FIX]: Map PartyA
                        PartyA = new ContractPartyDTO
                        {
                            UserId = c.OwnerId,
                            FullName = c.Owner != null ? c.Owner.FullName : "N/A",
                            Role = "Owner"
                        },

                        // [FIX]: Map PartyB
                        PartyB = new ContractPartyDTO
                        {
                            UserId = c.CounterpartyId,
                            FullName = c.Counterparty != null ? c.Counterparty.FullName : "N/A",
                            Role = "Driver"
                        },

                        // [FIX]: Null Check cho TemplateId (Guid? -> Guid)
                        ContractTemplateId = c.ContractTemplateId ?? Guid.Empty,
                        TemplateName = c.ContractTemplate != null ? c.ContractTemplate.ContractTemplateName : "N/A",
                        Version = c.Version,

                        // [FIX]: Enum -> String
                        Status = c.Status.ToString(),
                        Type = c.Type.ToString(),

                        CreateAt = c.CreateAt,
                        EffectiveDate = c.EffectiveDate,
                        OwnerSigned = c.OwnerSigned,
                        DriverSigned = c.CounterpartySigned
                    })
                    .ToListAsync();

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
