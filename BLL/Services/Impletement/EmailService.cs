using BLL.Services.Interface;
using Common.Settings;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using System;
using System.Threading.Tasks;
using static BLL.Services.Implement.TripService;

namespace BLL.Services.Impletement
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;

        // URL ảnh đã kèm Token (Hiển thị được ngay)
        private const string LogoUrl = "https://firebasestorage.googleapis.com/v0/b/driveshare-964cb.firebasestorage.app/o/Platform-support%2Ficon.png?alt=media&token=383801f5-587a-47dc-a413-ef18a77de128";
        private const string SplashImageUrl = "https://firebasestorage.googleapis.com/v0/b/driveshare-964cb.firebasestorage.app/o/Platform-support%2Fsplash.png?alt=media&token=08a10cf2-dfe9-4d08-9bad-33d3f1d18302";

        // Màu sắc thương hiệu
        private const string PrimaryColor = "#0052cc";     // Xanh DriveShare
        private const string AccentColor = "#00B8D9";      // Xanh nhạt điểm nhấn
        private const string BackgroundColor = "#F4F5F7";  // Nền xám nhẹ
        private const string CardColor = "#FFFFFF";        // Nền trắng
        private const string TextColor = "#1F2937";         // Đen chữ

        public EmailService(IOptions<EmailSettings> emailSettings)
        {
            _emailSettings = emailSettings.Value;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse(_emailSettings.From));
            email.To.Add(MailboxAddress.Parse(to));
            email.Subject = subject;

            var builder = new BodyBuilder { HtmlBody = body };
            email.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.Port, false);
            await smtp.AuthenticateAsync(_emailSettings.Username, _emailSettings.Password);
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }

        public async Task SendContractSigningOtpAsync(string email, string fullName, string otpCode, string contractCode)
        {
            var subject = $"🔐 [DriveShare] Mã OTP xác thực ký hợp đồng #{contractCode}";
            var requestTime = DateTime.UtcNow.AddHours(7).ToString("dd/MM/yyyy HH:mm"); // Giờ Việt Nam

            // HTML Template - Thiết kế theo phong cách Card UI hiện đại
            var body = $@"
<!DOCTYPE html>
<html lang='vi'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Xác thực ký hợp đồng</title>
    <style>
        /* Reset CSS */
        body, table, td, a {{ -webkit-text-size-adjust: 100%; -ms-text-size-adjust: 100%; }}
        table, td {{ mso-table-lspace: 0pt; mso-table-rspace: 0pt; }}
        img {{ -ms-interpolation-mode: bicubic; border: 0; height: auto; line-height: 100%; outline: none; text-decoration: none; }}
        
        /* Main Styles */
        body {{ font-family: 'Segoe UI', Helvetica, Arial, sans-serif; background-color: {BackgroundColor}; margin: 0; padding: 0; width: 100% !important; }}
        .wrapper {{ width: 100%; table-layout: fixed; background-color: {BackgroundColor}; padding-bottom: 40px; }}
        .main-table {{ background-color: {CardColor}; margin: 0 auto; width: 100%; max-width: 600px; border-spacing: 0; font-family: sans-serif; color: #172B4D; border-radius: 12px; overflow: hidden; box-shadow: 0 10px 25px rgba(0,0,0,0.05); }}
        
        /* Header & Hero */
        .header {{ background: linear-gradient(135deg, {PrimaryColor} 0%, #0747A6 100%); padding: 24px; text-align: center; }}
        .header-logo {{ width: 48px; height: auto; filter: brightness(0) invert(1); }} /* Logo trắng */
        .hero-section {{ width: 100%; background-color: #E3FCEF; text-align: center; border-bottom: 1px solid #EBECF0; }}
        .hero-img {{ width: 100%; max-height: 200px; object-fit: cover; display: block; }}

        /* Content */
        .content-padding {{ padding: 40px 32px; }}
        .heading {{ font-size: 24px; font-weight: 700; margin: 0 0 16px; color: {PrimaryColor}; text-align: center; }}
        .text-regular {{ font-size: 16px; line-height: 1.6; margin: 0 0 24px; color: #42526E; text-align: center; }}
        
        /* OTP Box - Điểm nhấn chính */
        .otp-wrapper {{ text-align: center; margin: 32px 0; position: relative; }}
        .otp-code {{ 
            background: #F4F5F7; 
            color: {PrimaryColor}; 
            font-size: 36px; 
            font-weight: 800; 
            letter-spacing: 12px; 
            padding: 20px 40px; 
            border-radius: 8px; 
            border: 2px dashed {PrimaryColor};
            display: inline-block;
            font-family: 'Courier New', monospace;
        }}
        .otp-caption {{ font-size: 13px; color: #DE350B; font-weight: 600; margin-top: 12px; display: block; text-transform: uppercase; letter-spacing: 1px; }}

        /* Info Table */
        .info-box {{ background-color: #FAFBFC; border-radius: 8px; padding: 20px; border: 1px solid #EBECF0; margin-bottom: 24px; }}
        .info-row {{ display: flex; justify-content: space-between; margin-bottom: 12px; border-bottom: 1px solid #EBECF0; padding-bottom: 8px; }}
        .info-row:last-child {{ border-bottom: none; margin-bottom: 0; padding-bottom: 0; }}
        .info-label {{ font-size: 14px; color: #6B778C; font-weight: 500; }}
        .info-val {{ font-size: 14px; color: #172B4D; font-weight: 700; text-align: right; }}

        /* Footer */
        .footer {{ background-color: {BackgroundColor}; padding: 24px; text-align: center; font-size: 12px; color: #6B778C; }}
        .footer a {{ color: {PrimaryColor}; text-decoration: none; font-weight: 600; }}
        .social-icons {{ margin-top: 16px; }}
    </style>
</head>
<body>
    <div class='wrapper'>
        <table class='main-table' role='presentation'>
            <tr>
                <td class='header'>
                    <img src='{LogoUrl}' alt='DriveShare' class='header-logo'/>
                </td>
            </tr>

            <tr>
                <td class='hero-section'>
                    <img src='{SplashImageUrl}' alt='Contract Signing' class='hero-img'/>
                </td>
            </tr>

            <tr>
                <td class='content-padding'>
                    <h1 class='heading'>Xác Thực Chữ Ký Điện Tử</h1>
                    
                    <p class='text-regular'>
                        Xin chào <strong>{fullName}</strong>,<br>
                        Bạn đang thực hiện ký hợp đồng điện tử trên nền tảng <strong>DriveShare</strong>. 
                        Để hoàn tất quy trình pháp lý này, vui lòng nhập mã xác thực bên dưới.
                    </p>

                    <div class='otp-wrapper'>
                        <div class='otp-code'>{otpCode}</div>
                        <span class='otp-caption'>⚠️ Hết hạn trong 5 phút</span>
                    </div>

                    <div class='info-box'>
                        <div class='info-row'>
                            <span class='info-label'>Mã hợp đồng</span>
                            <span class='info-val'>#{contractCode}</span>
                        </div>
                        <div class='info-row'>
                            <span class='info-label'>Loại giao dịch</span>
                            <span class='info-val'>Ký kết điện tử (E-Sign)</span>
                        </div>
                        <div class='info-row'>
                            <span class='info-label'>Thời gian yêu cầu</span>
                            <span class='info-val'>{requestTime}</span>
                        </div>
                         <div class='info-row'>
                            <span class='info-label'>Trạng thái</span>
                            <span class='info-val' style='color: #FFAB00;'>Chờ xác nhận</span>
                        </div>
                    </div>

                    <p class='text-regular' style='font-size: 14px; color: #6B778C;'>
                        *Lưu ý: Mã OTP này có giá trị như chữ ký tay của bạn. Tuyệt đối không chia sẻ cho bất kỳ ai, kể cả nhân viên hỗ trợ.
                    </p>
                </td>
            </tr>
        </table>

        <div class='footer'>
            <p>&copy; {DateTime.Now.Year} DriveShare Logistics Platform.<br>Tầng 12, Tòa nhà Innovation, TP.HCM</p>
            <p>
                <a href='#'>Trung tâm hỗ trợ</a> • <a href='#'>Điều khoản sử dụng</a> • <a href='#'>Chính sách bảo mật</a>
            </p>
        </div>
    </div>
</body>
</html>";

            await SendEmailAsync(email, subject, body);
        }

        // BLL/Services/Impletement/EmailService.cs

        /// <summary>
        /// Gửi Email chứa link xem và ký biên bản giao nhận (CÓ SPLASH IMAGE)
        /// </summary>
        public async Task SendDeliveryRecordLinkEmailAsync(string email, string fullName, string link, string recordCode, string typeName)
        {
            var subject = $"📝 [DriveShare] Yêu cầu ký xác nhận biên bản: {typeName}";
            var requestTime = DateTime.UtcNow.AddHours(7).ToString("dd/MM/yyyy HH:mm");

            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        /* Base */
        body {{ font-family: 'Helvetica Neue', Helvetica, Arial, sans-serif; background-color: {BackgroundColor}; margin: 0; padding: 0; -webkit-font-smoothing: antialiased; }}
        .wrapper {{ width: 100%; padding: 40px 0; }}
        
        /* Card Container */
        .container {{ max-width: 600px; margin: 0 auto; background-color: {CardColor}; border-radius: 12px; overflow: hidden; box-shadow: 0 10px 25px rgba(0,0,0,0.05); }}
        
        /* Header */
        .header {{ background: linear-gradient(135deg, {PrimaryColor} 0%, #0747A6 100%); padding: 24px; text-align: center; }}
        .logo {{ width: 48px; height: auto; filter: brightness(0) invert(1); }}
        
        /* Hero / Splash Image Section */
        .hero-section {{ width: 100%; background-color: #E3FCEF; text-align: center; border-bottom: 1px solid #EBECF0; position: relative; }}
        .hero-img {{ width: 100%; max-height: 220px; object-fit: cover; display: block; }}
        
        /* Content */
        .content {{ padding: 40px 32px; color: {TextColor}; }}
        .h1 {{ font-size: 24px; font-weight: 700; margin: 0 0 16px; color: {PrimaryColor}; text-align: center; letter-spacing: -0.5px; }}
        .p {{ font-size: 16px; line-height: 1.6; margin: 0 0 24px; color: #4B5563; }}
        
        /* Info Box */
        .info-card {{ background-color: #F0F9FF; border: 1px solid #BAE6FD; border-radius: 8px; padding: 20px; margin-bottom: 32px; }}
        .info-row {{ display: flex; justify-content: space-between; margin-bottom: 12px; padding-bottom: 12px; border-bottom: 1px dashed #BAE6FD; }}
        .info-row:last-child {{ border-bottom: none; margin-bottom: 0; padding-bottom: 0; }}
        .label {{ font-size: 13px; color: #64748B; font-weight: 600; text-transform: uppercase; letter-spacing: 0.5px; }}
        .value {{ font-size: 15px; color: #0C4A6E; font-weight: 700; text-align: right; }}

        /* Button */
        .btn-container {{ text-align: center; margin-bottom: 32px; }}
        .btn {{ background-color: #10B981; color: #ffffff; padding: 16px 40px; text-decoration: none; font-weight: 700; border-radius: 50px; font-size: 16px; display: inline-block; box-shadow: 0 4px 12px rgba(16, 185, 129, 0.4); transition: all 0.2s; }}
        
        /* Footer */
        .footer {{ background-color: #F9FAFB; padding: 24px; text-align: center; font-size: 12px; color: #9CA3AF; border-top: 1px solid #E5E7EB; }}
        .footer a {{ color: {PrimaryColor}; text-decoration: none; }}
    </style>
</head>
<body>
    <div class='wrapper'>
        <div class='container'>
            
            <div class='header'>
                <img src='{LogoUrl}' alt='DriveShare' class='logo'/>
            </div>

            <div class='hero-section'>
                <img src='{SplashImageUrl}' alt='Delivery Confirmation' class='hero-img'/>
            </div>

            <div class='content'>
                <h1 class='h1'>Yêu Cầu Xác Nhận</h1>
                
                <p class='p'>
                    Xin chào <strong>{fullName}</strong>,<br>
                    Tài xế đối tác của DriveShare đã cập nhật trạng thái chuyến đi. 
                    Vui lòng kiểm tra thông tin hàng hóa và thực hiện ký xác nhận điện tử biên bản dưới đây.
                </p>

                <div class='info-card'>
                    <div class='info-row'>
                        <span class='label'>Loại biên bản</span>
                        <span class='value'>{typeName}</span>
                    </div>
                    <div class='info-row'>
                        <span class='label'>Mã số</span>
                        <span class='value'>#{recordCode}</span>
                    </div>
                    <div class='info-row'>
                        <span class='label'>Thời gian</span>
                        <span class='value'>{requestTime}</span>
                    </div>
                    <div class='info-row'>
                        <span class='label'>Trạng thái</span>
                        <span class='value' style='color: #D97706;'>⏳ Chờ ký tên</span>
                    </div>
                </div>

                <div class='btn-container'>
                    <a href='{link}' class='btn'>✍️ Xem và Ký Tên Ngay</a>
                </div>

                <p class='p' style='font-size: 13px; text-align: center; color: #6B7280; margin-bottom: 0;'>
                    Liên kết này có giá trị trong vòng <strong>7 ngày</strong>.<br>
                    Nếu bạn không phải là người nhận, vui lòng bỏ qua email này.
                </p>
            </div>

            <div class='footer'>
                <p>&copy; {DateTime.Now.Year} DriveShare Logistics Platform.<br>Tầng 12, Tòa nhà Innovation, TP.HCM</p>
                <p>
                    <a href='#'>Trung tâm hỗ trợ</a> • <a href='#'>Chính sách bảo mật</a>
                </p>
            </div>
        </div>
    </div>
</body>
</html>";

            await SendEmailAsync(email, subject, body);
        }

        // BLL/Services/Impletement/EmailService.cs

        public async Task SendTripCompletionEmailAsync(string toEmail, TripCompletionReportModel model)
        {
            var subject = $"✅ [Báo cáo hoàn thành] Chuyến đi #{model.TripCode}";

            // Màu sắc tài chính
            string amountColor = model.IsIncome ? "#16A34A" : "#DC2626"; // Xanh lá hoặc Đỏ
            string amountSign = model.IsIncome ? "+" : "-";
            string amountBg = model.IsIncome ? "#F0FDF4" : "#FEF2F2";

            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{ font-family: 'Segoe UI', Helvetica, Arial, sans-serif; background-color: #F3F4F6; margin: 0; padding: 0; }}
        .container {{ max-width: 600px; margin: 0 auto; background-color: #FFFFFF; border-radius: 12px; overflow: hidden; box-shadow: 0 10px 15px -3px rgba(0, 0, 0, 0.1); margin-top: 20px; }}
        
        .header {{ background: linear-gradient(to right, #1e40af, #3b82f6); padding: 30px; text-align: center; color: white; }}
        .header h1 {{ margin: 0; font-size: 24px; letter-spacing: 1px; }}
        .trip-code {{ background-color: rgba(255,255,255,0.2); padding: 5px 10px; border-radius: 4px; font-size: 14px; margin-top: 10px; display: inline-block; }}

        .content {{ padding: 30px; color: #374151; }}
        
        /* FINANCIAL CARD */
        .financial-card {{ background-color: {amountBg}; border-left: 5px solid {amountColor}; padding: 20px; border-radius: 8px; margin-bottom: 30px; }}
        .fin-label {{ font-size: 12px; text-transform: uppercase; color: #6B7280; font-weight: 700; letter-spacing: 0.5px; }}
        .fin-amount {{ font-size: 32px; font-weight: 800; color: {amountColor}; margin: 5px 0; }}
        .fin-desc {{ font-size: 14px; color: #4B5563; }}

        /* TRIP DETAILS */
        .section-title {{ font-size: 14px; font-weight: 700; color: #111827; border-bottom: 2px solid #E5E7EB; padding-bottom: 8px; margin-bottom: 16px; text-transform: uppercase; }}
        
        /* ROUTE TIMELINE */
        .route-box {{ display: flex; flex-direction: column; gap: 15px; margin-bottom: 30px; }}
        .route-item {{ display: flex; align-items: flex-start; }}
        .route-icon {{ width: 24px; text-align: center; margin-right: 12px; font-size: 18px; }}
        .route-text {{ flex: 1; }}
        .route-label {{ font-size: 11px; color: #9CA3AF; font-weight: 600; }}
        .route-val {{ font-size: 14px; color: #1F2937; font-weight: 500; }}
        .route-connector {{ margin-left: 11px; height: 20px; border-left: 2px dashed #D1D5DB; margin-top: -5px; margin-bottom: -5px; }}

        /* INFO GRID */
        .grid {{ display: table; width: 100%; border-collapse: collapse; }}
        .row {{ display: table-row; }}
        .cell {{ display: table-cell; width: 50%; padding-bottom: 15px; }}
        .cell-label {{ font-size: 12px; color: #6B7280; display: block; margin-bottom: 4px; }}
        .cell-val {{ font-size: 14px; color: #111827; font-weight: 600; }}

        .footer {{ background-color: #F9FAFB; padding: 20px; text-align: center; font-size: 12px; color: #9CA3AF; border-top: 1px solid #E5E7EB; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>BÁO CÁO HOÀN THÀNH</h1>
            <div class='trip-code'>#{model.TripCode}</div>
        </div>
        
        <div class='content'>
            <p>Xin chào <strong>{model.RecipientName}</strong>,</p>
            <p>Chuyến đi đã kết thúc thành công vào lúc {model.CompletedAt}. Dưới đây là báo cáo chi tiết dành cho bạn.</p>

            <div class='financial-card'>
                <div class='fin-label'>{model.FinancialDescription}</div>
                <div class='fin-amount'>{amountSign}{model.Amount:N0} ₫</div>
                <div class='fin-desc'>Đã được cập nhật vào ví hệ thống.</div>
            </div>

            <div class='section-title'>Lộ trình vận chuyển</div>
            <div class='route-box'>
                <div class='route-item'>
                    <div class='route-icon'>🔵</div>
                    <div class='route-text'>
                        <div class='route-label'>ĐIỂM LẤY HÀNG</div>
                        <div class='route-val'>{model.StartAddress}</div>
                    </div>
                </div>
                <div class='route-connector'></div>
                <div class='route-item'>
                    <div class='route-icon'>🏁</div>
                    <div class='route-text'>
                        <div class='route-label'>ĐIỂM GIAO HÀNG</div>
                        <div class='route-val'>{model.EndAddress}</div>
                    </div>
                </div>
            </div>

            <div class='section-title'>Thông tin vận hành</div>
            <div class='grid'>
                <div class='row'>
                    <div class='cell'>
                        <span class='cell-label'>Phương tiện</span>
                        <span class='cell-val'>{model.VehiclePlate} ({model.VehicleType})</span>
                    </div>
                    <div class='cell'>
                        <span class='cell-label'>Khoảng cách</span>
                        <span class='cell-val'>{model.DistanceKm:N1} km</span>
                    </div>
                </div>
                <div class='row'>
                    <div class='cell'>
                        <span class='cell-label'>Số lượng hàng</span>
                        <span class='cell-val'>{model.PackageCount} kiện</span>
                    </div>
                    <div class='cell'>
                        <span class='cell-label'>Tổng trọng lượng</span>
                        <span class='cell-val'>{model.TotalPayload:N0} kg</span>
                    </div>
                </div>
            </div>
        </div>

        <div class='footer'>
            <p>DriveShare Logistics Platform &copy; {DateTime.Now.Year}</p>
            <p>Email này được gửi tự động, vui lòng không trả lời.</p>
        </div>
    </div>
</body>
</html>";

            await SendEmailAsync(toEmail, subject, body);
        }
    }
}