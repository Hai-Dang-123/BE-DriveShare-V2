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

        public TransactionService(IUnitOfWork unitOfWork, UserUtility userUtility)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
        }

        // 1. Dành cho User (Controller gọi) -> Trả về ResponseDTO
        public async Task<ResponseDTO> RequestWithdrawalAsync(WithdrawalRequestDTO dto)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                    return new ResponseDTO("Unauthorized", 401, false);

                // Trừ tiền (số âm)
                // Hàm này trả về DTO vì Controller cần nó
                return await ExecuteBalanceChangeAsync(userId, -dto.Amount, TransactionType.WITHDRAWAL, null, dto.Description, null);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error processing withdrawal request: {ex.Message}", 500, false);
            }
        }

        // 2. Dành cho Admin/Hệ thống (Service gọi) -> ⚠️ SỬA ĐỔI: Trả về ResponseDTO
        public async Task<ResponseDTO> CreateTopupAsync(InternalTransactionRequestDTO dto)
        {
            var userId = _userUtility.GetUserIdFromToken();
            if (userId == Guid.Empty)
                return new ResponseDTO("Unauthorized", 401, false);
            if (dto.Type != TransactionType.TOPUP)
                return new ResponseDTO("Invalid transaction type for Topup", 400, false);

            // Cộng tiền (số dương)
            return await ExecuteBalanceChangeAsync(userId, dto.Amount, dto.Type, dto.TripId, dto.Description, dto.ExternalCode);
        }

        // 3. Dành cho Hệ thống/Service (Service gọi) -> ⚠️ SỬA ĐỔI: Trả về ResponseDTO
        public async Task<ResponseDTO> CreatePaymentAsync(InternalTransactionRequestDTO dto)
        {
            var userId = _userUtility.GetUserIdFromToken();
            if (userId == Guid.Empty)
                return new ResponseDTO("Unauthorized", 401, false);
            // Trừ tiền (số âm)
            return await ExecuteBalanceChangeAsync(userId, -dto.Amount, dto.Type, dto.TripId, dto.Description, dto.ExternalCode);
        }

        // 4. Dành cho Hệ thống/Service (Service gọi) -> ⚠️ SỬA ĐỔI: Trả về ResponseDTO
        public async Task<ResponseDTO> CreatePayoutAsync(InternalTransactionRequestDTO dto)
        {
            var userId = _userUtility.GetUserIdFromToken();
            if (userId == Guid.Empty)
                return new ResponseDTO("Unauthorized", 401, false);
            // Cộng tiền (số dương)
            return await ExecuteBalanceChangeAsync(userId, dto.Amount, dto.Type, dto.TripId, dto.Description, dto.ExternalCode);
        }


        // ─────── HÀM LÕI (CORE) XỬ LÝ GIAO DỊCH ───────

        // Hàm lõi này VẪN trả về ResponseDTO vì nó cần cho cả hai loại hàm (public và internal)
        private async Task<ResponseDTO> ExecuteBalanceChangeAsync(Guid userId, decimal amount, TransactionType type, Guid? tripId, string description, string? externalCode = null)
        {
            await _unitOfWork.BeginTransactionAsync();
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

                // ⚠️ 5. TỰ ĐỘNG CẬP NHẬT TRẠNG THÁI TRIP (THEO FLOW MỚI)
                if (tripId.HasValue)
                {
                    var trip = await _unitOfWork.TripRepo.GetByIdAsync(tripId.Value);
                    if (trip != null)
                    {
                        bool tripUpdated = false;
                        switch (type)
                        {
                            // --- GIAI ĐOẠN 1: PROVIDER THANH TOÁN ---
                            case TransactionType.TRIP_PAYMENT:
                                // Khi Provider thanh toán -> Chuyển sang tìm tài xế
                                if (trip.Status == TripStatus.AWAITING_PROVIDER_PAYMENT)
                                {
                                    trip.Status = TripStatus.PENDING_DRIVER_ASSIGNMENT;
                                    tripUpdated = true;
                                }
                                break;

                            // --- GIAI ĐOẠN 2: OWNER THANH TOÁN TIỀN THUÊ TÀI XẾ ---
                            case TransactionType.DRIVER_SERVICE_PAYMENT:
                                // Khi Owner thanh toán tiền thuê -> Chuyển sang sẵn sàng giao xe
                                if (trip.Status == TripStatus.AWAITING_OWNER_PAYMENT)
                                {
                                    trip.Status = TripStatus.READY_FOR_VEHICLE_HANDOVER;
                                    tripUpdated = true;
                                }
                                break;

                            // --- GIAI ĐOẠN 6: QUYẾT TOÁN CHO OWNER ---
                            case TransactionType.OWNER_PAYOUT:
                                // Khi Hệ thống trả tiền Owner -> Chuyển sang trả tiền Driver
                                if (trip.Status == TripStatus.AWAITING_FINAL_PROVIDER_PAYOUT)
                                {
                                    trip.Status = TripStatus.AWAITING_FINAL_DRIVER_PAYOUT;
                                    tripUpdated = true;
                                }
                                break;

                            // --- GIAI ĐOẠN 6: QUYẾT TOÁN CHO DRIVER ---
                            case TransactionType.DRIVER_PAYOUT:
                                // Khi Hệ thống trả tiền Driver -> Hoàn tất
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

                // 6. Commit
                await _unitOfWork.CommitTransactionAsync();

                var transactionDto = MapTransactionToDTO(newTransaction);
                return new ResponseDTO($"Transaction {type} successful.", 200, true, transactionDto);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
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
    }

}