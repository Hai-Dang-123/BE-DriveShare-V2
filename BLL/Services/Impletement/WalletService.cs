using BLL.Services.Interface;
using BLL.Utilities;
using Common.DTOs;
using Common.Enums.Status;
using Common.Enums.Type;
using DAL.Entities;
using DAL.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BLL.Services.Impletement
{
    public class WalletService : IWalletService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserUtility _userUtility;

        public WalletService(IUnitOfWork unitOfWork, UserUtility userUtility)
        {
            _unitOfWork = unitOfWork;
            _userUtility = userUtility;
        }

        public async Task<ResponseDTO> GetMyWalletAsync()
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                    return new ResponseDTO("Unauthorized", 401, false);

                var wallet = await _unitOfWork.WalletRepo.FirstOrDefaultAsync(w => w.UserId == userId);

                if (wallet == null)
                {
                    // Nếu người dùng chưa có ví, hãy tạo cho họ một cái
                    wallet = new Wallet { WalletId = Guid.NewGuid(), UserId = userId };
                    await _unitOfWork.WalletRepo.AddAsync(wallet);
                    await _unitOfWork.SaveChangeAsync();
                }

                var dto = MapWalletToDTO(wallet);
                return new ResponseDTO("Get wallet successfully", 200, true, dto);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error getting wallet: {ex.Message}", 500, false);
            }
        }

        public async Task<ResponseDTO> GetMyTransactionHistoryAsync(int pageNumber = 1, int pageSize = 10)
        {
            try
            {
                var userId = _userUtility.GetUserIdFromToken();
                if (userId == Guid.Empty)
                    return new ResponseDTO("Unauthorized", 401, false);

                var wallet = await _unitOfWork.WalletRepo.FirstOrDefaultAsync(w => w.UserId == userId);
                if (wallet == null)
                    return new ResponseDTO("Wallet not found", 404, false);

                var query = _unitOfWork.TransactionRepo.GetAll()
                                .Where(t => t.WalletId == wallet.WalletId);

                var totalCount = await query.CountAsync();
                var transactions = await query
                    .OrderByDescending(t => t.CreatedAt)
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .Select(t => MapTransactionToDTO(t)) // Dùng .Select() để tối ưu
                    .ToListAsync();

                var result = new TransactionHistoryDTO
                {
                    WalletInfo = MapWalletToDTO(wallet),
                    Transactions = new PaginatedDTO<TransactionDTO>(
                        transactions, totalCount, pageNumber, pageSize
                    )
                };

                return new ResponseDTO("Get transaction history successfully", 200, true, result);
            }
            catch (Exception ex)
            {
                return new ResponseDTO($"Error getting transaction history: {ex.Message}", 500, false);
            }
        }

       


        // ─────── HÀM PRIVATE HELPER (MAPPER) ───────

        private WalletDTO MapWalletToDTO(Wallet wallet)
        {
            return new WalletDTO
            {
                WalletId = wallet.WalletId,
                UserId = wallet.UserId,
                Balance = wallet.Balance,
                FrozenBalance = wallet.FrozenBalance,
                Currency = wallet.Currency,
                Status = wallet.Status.ToString(),
                LastUpdatedAt = wallet.LastUpdatedAt
            };
        }

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
    }
}
