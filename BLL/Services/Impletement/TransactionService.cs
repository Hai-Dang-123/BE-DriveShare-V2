using BLL.Services.Impletement;
using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.Enums.Type;
using DAL.Entities;
using DAL.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace BLL.Services.Implement
{
    public class TransactionService : ITransactionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;
        private readonly ISepayService _sepayService;
        private readonly IEmailService _emailService;

        public TransactionService(IUnitOfWork unitOfWork, UserUtility userUtility, ISepayService sepayService, IEmailService emailService)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
            _sepayService = sepayService;
            _emailService = emailService;
        }

        // ==========================================
        // 1. USER TẠO YÊU CẦU NẠP TIỀN (TOPUP)
        // ==========================================
        public async Task<ResponseDTO> CreateTopupAsync(InternalTransactionRequestDTO dto)
        {
            var userId = _userUtility.GetUserIdFromToken();
            if (userId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);
            if (dto.Type != TransactionType.TOPUP) return new ResponseDTO("Invalid transaction type", 400, false);

            using var transactionScope = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var wallet = await _unitOfWork.WalletRepo.FirstOrDefaultAsync(w => w.UserId == userId);
                if (wallet == null) throw new Exception("Wallet not found");

                // B1: Tạo Transaction với status PENDING (Chưa có Token)
                var newTransaction = new Transaction
                {
                    TransactionId = Guid.NewGuid(),
                    WalletId = wallet.WalletId,
                    TripId = null,
                    Amount = dto.Amount,
                    BalanceBefore = wallet.Balance,
                    BalanceAfter = wallet.Balance, // Chưa cộng tiền
                    Type = TransactionType.TOPUP,
                    Status = TransactionStatus.PENDING, // <--- STEP 1: PENDING
                    CreatedAt = DateTime.UtcNow,
                    Description = dto.Description
                };
                await _unitOfWork.TransactionRepo.AddAsync(newTransaction);
                await _unitOfWork.SaveChangeAsync();

                // B2: Tạo Token định danh (Ví dụ: TP + 6 số ngẫu nhiên -> TP839210)
                // Đây là mã user phải nhập vào nội dung chuyển khoản
                string tokenCode = $"TP{new Random().Next(100000, 999999)}";

                var userToken = new UserToken
                {
                    UserTokenId = Guid.NewGuid(),
                    UserId = userId,
                    // Nếu chưa có Enum Payment, dùng tạm ResetPassword hoặc thêm mới
                    TokenType = TokenType.SEPAY_PAYMENT,
                    TokenValue = tokenCode,
                    IsRevoked = false,
                    CreatedAt = DateTime.UtcNow,
                    ExpiredAt = DateTime.UtcNow.AddMinutes(30) // Token/QR hết hạn sau 30p
                };
                await _unitOfWork.UserTokenRepo.AddAsync(userToken);

                // B3: Update Transaction -> WAITING_FOR_CONFIRMATION
                // Lưu tokenCode vào ExternalTransactionCode để lúc Webhook gọi về thì tìm được Transaction này
                newTransaction.Status = TransactionStatus.WAITING_FOR_CONFIRMATION;
                newTransaction.ExternalTransactionCode = tokenCode;

                await _unitOfWork.TransactionRepo.UpdateAsync(newTransaction);
                await _unitOfWork.SaveChangeAsync();

                // B4: Tạo QR SePay. Nội dung CK: "TOPUP [Mã Token]"
                string transferContent = $"TOPUP {tokenCode}";
                string qrUrl = _sepayService.CreateSepayQR(dto.Amount, transferContent);

                await transactionScope.CommitAsync();



                // Trả về QR cho Client hiển thị
                return new ResponseDTO("Created Topup request successfully", 200, true, new
                {
                    QrUrl = qrUrl,
                    TransactionId = newTransaction.TransactionId,
                    TransferContent = transferContent,
                    Amount = dto.Amount
                });
            }
            catch (Exception ex)
            {
                await transactionScope.RollbackAsync();
                return new ResponseDTO($"Error creating topup: {ex.Message}", 500, false);
            }
        }

        // ==========================================
        // 2. USER YÊU CẦU RÚT TIỀN (WITHDRAWAL)
        // ==========================================
        // ==========================================
        // 2. USER YÊU CẦU RÚT TIỀN (WITHDRAWAL) - SỬA ĐỔI: TRỪ NGAY, KHÔNG ĐỢI ADMIN
        // ==========================================
        public async Task<ResponseDTO> RequestWithdrawalAsync(WithdrawalRequestDTO dto)
        {
            using var transactionScope = await _unitOfWork.BeginTransactionAsync(); // [MỚI] Dùng TransactionScope cho an toàn
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty) return new ResponseDTO("Unauthorized", 401, false);

                // 1. Lấy thông tin User để gửi mail
                var user = await _unitOfWork.BaseUserRepo.GetByIdAsync(userId);
                if (user == null) return new ResponseDTO("User not found", 404, false);

                // 2. Lấy Wallet
                var wallet = await _unitOfWork.WalletRepo.FirstOrDefaultAsync(w => w.UserId == userId);
                if (wallet == null) return new ResponseDTO("Wallet not found", 404, false);

                // 3. Validate số dư (Quan trọng)
                if (wallet.Balance < dto.Amount)
                    return new ResponseDTO("Số dư không đủ để thực hiện giao dịch.", 400, false);

                // 4. [LOGIC MỚI] TRỪ TIỀN NGAY LẬP TỨC
                wallet.Balance -= dto.Amount;
                wallet.LastUpdatedAt = DateTime.UtcNow;

                await _unitOfWork.WalletRepo.UpdateAsync(wallet); // Update Wallet

                // 5. [LOGIC MỚI] TẠO TRANSACTION VỚI TRẠNG THÁI SUCCEEDED
                var newTransaction = new Transaction
                {
                    TransactionId = Guid.NewGuid(),
                    WalletId = wallet.WalletId,
                    Amount = -dto.Amount, // Số âm
                    BalanceBefore = wallet.Balance + dto.Amount, // Số dư trước khi trừ
                    BalanceAfter = wallet.Balance,               // Số dư sau khi trừ
                    Type = TransactionType.WITHDRAWAL,
                    Status = TransactionStatus.SUCCEEDED,        // <--- SUCCEEDED NGAY
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow,               // Đã hoàn thành
                    Description = dto.Description
                };

                await _unitOfWork.TransactionRepo.AddAsync(newTransaction);
                await _unitOfWork.SaveChangeAsync();

                // 6. Commit Transaction Database
                await transactionScope.CommitAsync();

                // 7. [MỚI] GỬI EMAIL THÔNG BÁO RÚT TIỀN THÀNH CÔNG
                // (Chạy fire-and-forget hoặc await tùy nhu cầu, ở đây await để đảm bảo)
                if (!string.IsNullOrEmpty(user.Email))
                {
                    await _emailService.SendTransactionSuccessEmailAsync(
                        user.Email,
                        user.FullName ?? "User",
                        "Rút tiền về tài khoản",
                        -dto.Amount,
                        wallet.Balance,
                        newTransaction.TransactionId.ToString().Substring(0, 8).ToUpper()
                    );
                }

                return new ResponseDTO("Rút tiền thành công.", 200, true, MapTransactionToDTO(newTransaction));
            }
            catch (Exception ex)
            {
                await transactionScope.RollbackAsync();
                return new ResponseDTO($"Lỗi xử lý rút tiền: {ex.Message}", 500, false);
            }
        }

        // ==========================================
        // 3. XỬ LÝ LOGIC KHI WEBHOOK GỌI VỀ
        // ==========================================
        // ==========================================
        // 1. CONFIRM TOPUP (CÓ XỬ LÝ TRỪ NỢ)
        // ==========================================
        public async Task<ResponseDTO> ConfirmTopupTransactionAsync(string tokenCode, decimal transferAmount, string bankReferenceCode)
        {
            using var transactionScope = await _unitOfWork.BeginTransactionAsync();
            try
            {
                // B1: Validate Token
                var userToken = await _unitOfWork.UserTokenRepo.GetAll()
                    .FirstOrDefaultAsync(t => t.TokenValue == tokenCode && !t.IsRevoked);

                if (userToken == null) return new ResponseDTO("Token invalid or revoked", 404, false);
                if (userToken.ExpiredAt < DateTime.UtcNow) return new ResponseDTO("Token expired", 400, false);

                // B2: Tìm Transaction Pending
                var transaction = await _unitOfWork.TransactionRepo.GetAll()
                    .FirstOrDefaultAsync(t => t.ExternalTransactionCode == tokenCode && t.Status == TransactionStatus.WAITING_FOR_CONFIRMATION);

                if (transaction == null) return new ResponseDTO("Transaction not found", 404, false);

                // Load Wallet & User
                var wallet = await _unitOfWork.WalletRepo.GetByIdAsync(transaction.WalletId);
                var user = await _unitOfWork.BaseUserRepo.GetByIdAsync(wallet.UserId);

                // B3: Validate Amount
                if (transaction.Amount != transferAmount)
                {
                    if (user != null) await _emailService.SendTopupFailureEmailAsync(user.Email, user.FullName, transferAmount, "Sai số tiền yêu cầu.");
                    return new ResponseDTO("Amount mismatch", 400, false);
                }

                // =========================================================
                // B4: XỬ LÝ CỘNG TIỀN & TÍNH TOÁN TRẢ NỢ (CORE LOGIC)
                // =========================================================

                // 1. Lưu lại số dư cũ để check nợ
                decimal balanceBeforeTopup = wallet.Balance;

                // 2. Cộng tiền vào ví (Thao tác này về mặt toán học đã bao gồm việc trả nợ)
                wallet.Balance += transferAmount;
                wallet.LastUpdatedAt = DateTime.UtcNow;
                await _unitOfWork.WalletRepo.UpdateAsync(wallet);

                // 3. Update Transaction Topup
                transaction.Status = TransactionStatus.SUCCEEDED;
                transaction.BalanceBefore = balanceBeforeTopup;
                transaction.BalanceAfter = wallet.Balance;
                transaction.CompletedAt = DateTime.UtcNow;
                transaction.ExternalTransactionCode = bankReferenceCode;
                await _unitOfWork.TransactionRepo.UpdateAsync(transaction);

                // 4. Revoke Token
                userToken.IsRevoked = true;
                await _unitOfWork.UserTokenRepo.UpdateAsync(userToken);

                // 5. Commit DB
                await _unitOfWork.SaveChangeAsync();
                await transactionScope.CommitAsync();

                // =========================================================
                // B5: GỬI EMAIL THÔNG BÁO (THÔNG MINH)
                // =========================================================
                if (user != null && !string.IsNullOrEmpty(user.Email))
                {
                    // CASE A: User ĐANG CÓ NỢ (Số dư cũ < 0)
                    if (balanceBeforeTopup < 0)
                    {
                        decimal debtAmount = Math.Abs(balanceBeforeTopup); // Nợ gốc (vd: 200k)

                        // Tính số tiền thực sự dùng để trả nợ
                        decimal recoveredAmount = (transferAmount >= debtAmount) ? debtAmount : transferAmount;

                        // Tính nợ còn lại (nếu nạp ít hơn nợ)
                        decimal remainingDebt = (transferAmount >= debtAmount) ? 0 : (debtAmount - transferAmount);

                        // Gửi mail chuyên biệt: "Nạp tiền & Thu hồi nợ"
                        await _emailService.SendDebtRecoveryEmailAsync(
                            user.Email,
                            user.FullName,
                            recoveredAmount,
                            remainingDebt,
                            wallet.Balance
                        );
                    }
                    // CASE B: User KHÔNG NỢ (Bình thường)
                    else
                    {
                        await _emailService.SendTransactionSuccessEmailAsync(
                            user.Email,
                            user.FullName,
                            "Nạp tiền ví điện tử",
                            transferAmount,
                            wallet.Balance,
                            bankReferenceCode
                        );
                    }
                }

                return new ResponseDTO("Topup confirmed successfully", 200, true);
            }
            catch (Exception ex)
            {
                await transactionScope.RollbackAsync();
                return new ResponseDTO($"Error: {ex.Message}", 500, false);
            }
        }
        // 3. Dành cho Hệ thống/Service (Service gọi) -> ⚠️ SỬA ĐỔI: Trả về ResponseDTO
        public async Task<ResponseDTO> CreatePaymentAsync(InternalTransactionRequestDTO dto)
        {
            var userId = _userUtility.GetUserIdFromToken();
            if (userId == Guid.Empty)
                return new ResponseDTO("Unauthorized", 401, false);
            // Trừ tiền (số âm)
            return await ExecuteBalanceChangeAsync(userId, -dto.Amount, dto.Type, dto.TripId, dto.PostId, dto.Description, dto.ExternalCode);
        }

        // 4. Dành cho Hệ thống/Service (Service gọi) -> ⚠️ SỬA ĐỔI: Trả về ResponseDTO
        public async Task<ResponseDTO> CreatePayoutAsync(InternalTransactionRequestDTO dto)
        {
            var userId = _userUtility.GetUserIdFromToken();
            if (userId == Guid.Empty)
                return new ResponseDTO("Unauthorized", 401, false);
            // Cộng tiền (số dương)
            return await ExecuteBalanceChangeAsync(userId, dto.Amount, dto.Type, dto.TripId, dto.PostId ,dto.Description, dto.ExternalCode);
        }


        // ─────── HÀM LÕI (CORE) XỬ LÝ GIAO DỊCH ───────

        // Hàm lõi này VẪN trả về ResponseDTO vì nó cần cho cả hai loại hàm (public và internal)
        private async Task<ResponseDTO> ExecuteBalanceChangeAsync(Guid userId, decimal amount, TransactionType type, Guid? tripId, Guid? postId, string description, string? externalCode = null)
        {
            // [FIX 1] Sử dụng 'using' để quản lý Transaction Scope
            using var transaction = await _unitOfWork.BeginTransactionAsync();
            try
            {
                // 1. Lấy & Validate Wallet
                var wallet = await _unitOfWork.WalletRepo.FirstOrDefaultAsync(w => w.UserId == userId);
                if (wallet == null) throw new Exception($"Wallet not found for user {userId}");
                if (wallet.Status != WalletStatus.ACTIVE) throw new Exception("Wallet is not active");

                // 2. Validate Số dư (nếu trừ tiền)
                if (amount < 0 && (wallet.Balance < Math.Abs(amount)))
                    throw new Exception("Insufficient funds");

                // 3. Cập nhật số dư
                decimal balanceBefore = wallet.Balance;
                wallet.Balance += amount;
                wallet.LastUpdatedAt = DateTime.UtcNow;
                decimal balanceAfter = wallet.Balance;

                // 4. Tạo Transaction Record
                var newTransaction = new Transaction
                {
                    TransactionId = Guid.NewGuid(),
                    WalletId = wallet.WalletId,
                    TripId = tripId,
                    // PostId = postId, // Uncomment nếu Entity Transaction có trường này
                    Amount = amount,
                    Currency = "VND",
                    BalanceBefore = balanceBefore,
                    BalanceAfter = balanceAfter,
                    Type = type,
                    Status = TransactionStatus.SUCCEEDED,
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow,
                    Description = description,
                    ExternalTransactionCode = externalCode
                };

                await _unitOfWork.TransactionRepo.AddAsync(newTransaction);
                await _unitOfWork.WalletRepo.UpdateAsync(wallet);

                // 5. [UPDATE] CẬP NHẬT TRẠNG THÁI POST (CHECK CẢ PACKAGE VÀ TRIP)
                if (postId.HasValue)
                {
                    bool isPostUpdated = false;

                    // A. Thử tìm trong PostPackage
                    var postPackage = await _unitOfWork.PostPackageRepo.GetByIdAsync(postId.Value);
                    if (postPackage != null)
                    {
                        postPackage.Status = PostStatus.OPEN;
                        postPackage.Updated = DateTime.UtcNow; // Nhớ update time
                        await _unitOfWork.PostPackageRepo.UpdateAsync(postPackage);
                        isPostUpdated = true;
                    }

                    // B. Nếu không phải Package, thử tìm trong PostTrip
                    if (!isPostUpdated)
                    {
                        var postTrip = await _unitOfWork.PostTripRepo.GetByIdAsync(postId.Value);
                        if (postTrip != null)
                        {
                            postTrip.Status = PostStatus.OPEN;
                            postTrip.UpdateAt = DateTime.UtcNow; // Nhớ update time
                            await _unitOfWork.PostTripRepo.UpdateAsync(postTrip);
                        }
                    }
                }

                // 6. CẬP NHẬT TRẠNG THÁI TRIP & ASSIGNMENT
                if (tripId.HasValue)
                {
                    var trip = await _unitOfWork.TripRepo.GetByIdAsync(tripId.Value);
                    if (trip != null)
                    {
                        bool tripUpdated = false;
                        switch (type)
                        {
                            // --- GIAI ĐOẠN 2: OWNER THANH TOÁN TIỀN THUÊ TÀI XẾ ---
                            case TransactionType.DRIVER_SERVICE_PAYMENT:
                                // A. Cập nhật trạng thái Trip
                                if (trip.Status == TripStatus.AWAITING_OWNER_PAYMENT)
                                {
                                    trip.Status = TripStatus.READY_FOR_VEHICLE_HANDOVER;
                                    tripUpdated = true;
                                }

                                // B. Cập nhật PaymentStatus của Assignment thành PAID
                                var assignments = await _unitOfWork.TripDriverAssignmentRepo.GetAll()
                                    .Where(a => a.TripId == tripId.Value && a.PaymentStatus == DriverPaymentStatus.UN_PAID)
                                    .ToListAsync();

                                if (assignments.Any())
                                {
                                    foreach (var assign in assignments)
                                    {
                                        assign.PaymentStatus = DriverPaymentStatus.PAID;
                                        assign.UpdateAt = DateTime.UtcNow;
                                        await _unitOfWork.TripDriverAssignmentRepo.UpdateAsync(assign);
                                    }
                                }
                                break;

                            // --- GIAI ĐOẠN 6: QUYẾT TOÁN CHO OWNER ---
                            case TransactionType.OWNER_PAYOUT:
                                if (trip.Status == TripStatus.AWAITING_FINAL_PROVIDER_PAYOUT)
                                {
                                    trip.Status = TripStatus.AWAITING_FINAL_DRIVER_PAYOUT;
                                    tripUpdated = true;
                                }
                                break;

                            // --- GIAI ĐOẠN 6: QUYẾT TOÁN CHO DRIVER ---
                            case TransactionType.DRIVER_PAYOUT:
                                if (trip.Status == TripStatus.AWAITING_FINAL_DRIVER_PAYOUT)
                                {
                                    trip.Status = TripStatus.COMPLETED;
                                    tripUpdated = true;
                                }
                                break;
                        }

                        if (tripUpdated)
                        {
                            trip.UpdateAt = DateTime.UtcNow;
                            await _unitOfWork.TripRepo.UpdateAsync(trip);
                        }
                    }
                }

                // 7. Commit
                await _unitOfWork.SaveChangeAsync();

                // [FIX 2] Gọi Commit từ biến 'transaction'
                await transaction.CommitAsync();

                // Map DTO
                var transactionDto = MapTransactionToDTO(newTransaction);
                return new ResponseDTO($"Transaction {type} successful.", 200, true, transactionDto);
            }
            catch (Exception ex)
            {
                // [FIX 3] Gọi Rollback từ biến 'transaction'
                await transaction.RollbackAsync();
                return new ResponseDTO($"Transaction failed: {ex.Message}", 400, false);
            }
        }
        // ─────── HÀM PRIVATE HELPER (MAPPER) ───────

        private TransactionDTO MapTransactionToDTO(Transaction t)
        {
            return new TransactionDTO
            {
                TransactionId = t.TransactionId,
                WalletId = t.WalletId,
                TripId = t.TripId,
                Amount = t.Amount,
                BalanceBefore = t.BalanceBefore,
                BalanceAfter = t.BalanceAfter,
                Type = t.Type.ToString(),
                Status = t.Status.ToString(),
                CreatedAt = t.CreatedAt,
                CompletedAt = t.CompletedAt,
                Description = t.Description,
                ExternalTransactionCode = t.ExternalTransactionCode
            };
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
                var query = _unitOfWork.TransactionRepo.GetAll()
                                     .AsNoTracking();

                // 3. Lọc theo Vai trò (Authorization)
                if (userRole == "Admin")
                {
                    // Admin: không cần lọc, thấy tất cả
                }
                else
                {
                    // User (Owner/Driver/Provider): Lọc theo WalletId của họ
                    var wallet = await _unitOfWork.WalletRepo.FirstOrDefaultAsync(w => w.UserId == userId);
                    if (wallet == null)
                    {
                        // Mặc dù user tồn tại nhưng không có ví? Trả về rỗng.
                        return new ResponseDTO("User wallet not found.", 404, false);
                    }
                    query = query.Where(t => t.WalletId == wallet.WalletId);
                }

                // 4. Đếm tổng số lượng (sau khi lọc)
                var totalCount = await query.CountAsync();

                // 5. Lấy dữ liệu của trang và Map sang DTO
                var transactions = await query
                    .OrderByDescending(t => t.CreatedAt) // Sắp xếp mới nhất trước
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // 6. Map sang DTO (dùng hàm helper có sẵn)
                var dtoList = transactions.Select(MapTransactionToDTO).ToList();

                // 7. Tạo kết quả PaginatedDTO
                var paginatedResult = new PaginatedDTO<TransactionDTO>(dtoList, totalCount, pageNumber, pageSize);

                return new ResponseDTO("Retrieved transactions successfully", 200, true, paginatedResult);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error getting transactions: {ex.Message}", 500, false);
            }
        }

        // =========================================================
        // 🔹 6. GET BY ID (Lọc theo Admin/User)
        // =========================================================
        public async Task<ResponseDTO> GetByIdAsync(Guid transactionId)
        {
            try
            {
                // 1. Lấy thông tin User
                var userId = _userUtility.GetUserIdFromToken();
                var userRole = _userUtility.GetUserRoleFromToken();
                if (userId == Guid.Empty)
                    return new ResponseDTO("Unauthorized: Invalid token", 401, false);

                // 2. Lấy Transaction (Giả định GetByIdAsync KHÔNG include)
                var transaction = await _unitOfWork.TransactionRepo.GetByIdAsync(transactionId);
                if (transaction == null)
                    return new ResponseDTO("Transaction not found", 404, false);

                // 3. Lọc theo Vai trò (Authorization)
                if (userRole != "Admin")
                {
                    // Nếu không phải Admin, kiểm tra xem có phải chủ Wallet không
                    var wallet = await _unitOfWork.WalletRepo.FirstOrDefaultAsync(w => w.UserId == userId);
                    if (wallet == null || transaction.WalletId != wallet.WalletId)
                    {
                        return new ResponseDTO("Forbidden: You do not have access to this transaction", 403, false);
                    }
                }
                // (Nếu là Admin thì bỏ qua, được phép xem)

                // 4. Map và trả về (dùng hàm helper có sẵn)
                var transactionDto = MapTransactionToDTO(transaction);
                return new ResponseDTO("Transaction retrieved successfully", 200, true, transactionDto);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error getting transaction: {ex.Message}", 500, false);
            }
        }

        // ==========================================
        // 2. CHECK USER CÓ ĐANG BỊ KHÓA VÌ NỢ KHÔNG
        // ==========================================
        public async Task<bool> IsUserRestrictedDueToDebtAsync(Guid userId)
        {
            // Lấy ví (Không cần tracking để tối ưu)
            var wallet = await _unitOfWork.WalletRepo.GetAll()
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.UserId == userId);

            if (wallet == null) return true; // Không có ví -> Block luôn cho an toàn

            // LOGIC 1: Đang nợ (Balance Âm)
            // Đây là chốt chặn quan trọng nhất
            if (wallet.Balance < 0) return true;

            // LOGIC 2: Số dư tối thiểu (Deposit)
            // Ví dụ: Tài xế phải duy trì tối thiểu 50k để hệ thống trừ phí/phạt nguội nếu có
            decimal minRequiredBalance = 50000;
            if (wallet.Balance < minRequiredBalance) return true;

            // LOGIC 3 (Optional): Check bảng Debt riêng nếu bạn có dùng
            // bool hasPendingDebt = await _unitOfWork.DebtRepo.AnyAsync(d => d.UserId == userId && d.Status == DebtStatus.PENDING);
            // if (hasPendingDebt) return true;

            return false; // User sạch, cho phép nhận chuyến
        }


    }

}