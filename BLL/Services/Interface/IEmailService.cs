using Common.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BLL.Services.Implement.TripService;

namespace BLL.Services.Interface
{
    public interface IEmailService
    {
        Task SendContractSigningOtpAsync(string email, string fullName, string otpCode, string contractCode);
        Task SendDeliveryRecordLinkEmailAsync(string email, string fullName, string link, string recordCode, string typeName);
        // Trong IEmailService.cs
        Task SendTripCompletionEmailAsync(string toEmail, TripCompletionReportModel model);
        Task SendEmailVerificationLinkAsync(string email, string fullName, string verificationLink);

        Task SendTripLiquidationEmailAsync(ParticipantFinancialReport report, string tripCode);

        // [MỚI] Hàm gửi thông báo biến động số dư (Nạp/Rút thành công)
        Task SendTransactionSuccessEmailAsync(string email, string fullName, string transactionType, decimal amount, decimal newBalance, string transactionCode);

        // [MỚI] Hàm gửi thông báo nạp tiền thất bại (Để user biết mà khiếu nại)
        Task SendTopupFailureEmailAsync(string email, string fullName, decimal amount, string reason);
        Task SendDebtRecoveryEmailAsync(string email, string fullName, decimal recoveredAmount, decimal remainingDebt, decimal newBalance);
        // Trong IEmailService.cs
        Task SendDepositRefundEmailAsync(string email, string fullName, decimal amount, string tripCode, string reason);

        Task SendCancellationCompensationEmailAsync(string email, string fullName, string tripCode, decimal amount, string reason, string ownerName);
    }
}
