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

    }
}
