using BLL.Services.Interface;
using Common.DTOs;
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

            // --- KHỐI TÀI CHÍNH (DYNAMIC) ---
            string financialHtml = "";

            if (model.Role == "Owner")
            {
                // ---------------- OWNER: HIỂN THỊ QUYẾT TOÁN TỔNG HỢP ----------------
                var expenseRows = "";
                foreach (var exp in model.DriverExpenses)
                {
                    expenseRows += $@"
            <div style='display:flex; justify-content:space-between; margin-bottom:8px; border-bottom:1px dashed #E5E7EB; padding-bottom:4px;'>
                <span style='font-size:13px; color:#4B5563;'>Trả {exp.Role} ({exp.DriverName})</span>
                <span style='font-size:13px; color:#DC2626; font-weight:600;'>-{exp.Amount:N0} ₫</span>
            </div>";
                }

                financialHtml = $@"
        <div style='background-color:#F3F4F6; border-radius:8px; padding:20px; margin-bottom:30px;'>
            <div style='font-size:12px; font-weight:700; color:#6B7280; text-transform:uppercase; margin-bottom:15px;'>Bảng quyết toán chuyến đi</div>
            
            <div style='display:flex; justify-content:space-between; margin-bottom:12px;'>
                <span style='color:#374151;'>Thu từ Provider (sau phí sàn)</span>
                <span style='color:#16A34A; font-weight:700; font-size:16px;'>+{model.TotalIncome:N0} ₫</span>
            </div>

            {expenseRows}

            <div style='border-top:2px solid #D1D5DB; margin:15px 0;'></div>

            <div style='display:flex; justify-content:space-between; align-items:center;'>
                <span style='font-weight:700; color:#111827;'>LỢI NHUẬN RÒNG</span>
                <span style='color:#2563EB; font-weight:800; font-size:24px;'>{model.NetProfit:N0} ₫</span>
            </div>
        </div>";
            }
            else
            {
                // ---------------- PROVIDER / DRIVER: HIỂN THỊ ĐƠN GIẢN ----------------
                string color = model.IsIncome ? "#16A34A" : "#DC2626"; // Xanh/Đỏ
                string sign = model.IsIncome ? "+" : "-";
                string bg = model.IsIncome ? "#F0FDF4" : "#FEF2F2";

                financialHtml = $@"
        <div style='background-color:{bg}; border-left:5px solid {color}; padding:20px; border-radius:8px; margin-bottom:30px;'>
            <div style='font-size:12px; text-transform:uppercase; color:#6B7280; font-weight:700; letter-spacing:0.5px;'>{model.FinancialDescription}</div>
            <div style='font-size:32px; font-weight:800; color:{color}; margin:5px 0;'>{sign}{model.Amount:N0} ₫</div>
            <div style='font-size:14px; color:#4B5563;'>Giao dịch đã được ghi nhận vào hệ thống.</div>
        </div>";
            }

            // --- TEMPLATE CHUNG ---
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; background-color: #F3F4F6; margin: 0; padding: 0; }}
        .container {{ max-width: 600px; margin: 20px auto; background: #FFF; border-radius: 12px; overflow: hidden; }}
        .header {{ background: #2563EB; padding: 30px; text-align: center; color: white; }}
        .content {{ padding: 30px; color: #374151; }}
        .section-title {{ font-size: 14px; font-weight: 700; border-bottom: 2px solid #E5E7EB; padding-bottom: 8px; margin-bottom: 16px; text-transform: uppercase; }}
        .grid {{ width: 100%; border-collapse: collapse; }}
        .cell {{ width: 50%; padding-bottom: 15px; vertical-align: top; }}
        .label {{ font-size: 12px; color: #6B7280; display: block; margin-bottom: 4px; }}
        .val {{ font-size: 14px; font-weight: 600; color: #111827; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1 style='margin:0; font-size:24px;'>BÁO CÁO HOÀN THÀNH</h1>
            <div style='background:rgba(255,255,255,0.2); padding:5px 10px; border-radius:4px; display:inline-block; margin-top:10px;'>#{model.TripCode}</div>
        </div>
        
        <div class='content'>
            <p>Xin chào <strong>{model.RecipientName}</strong>,</p>
            <p>Chuyến đi đã kết thúc lúc {model.CompletedAt}.</p>

            {financialHtml}

            <div class='section-title'>Thông tin vận hành</div>
            <table class='grid'>
                <tr>
                    <td class='cell'><span class='label'>Điểm đi</span><span class='val'>{model.StartAddress}</span></td>
                    <td class='cell'><span class='label'>Điểm đến</span><span class='val'>{model.EndAddress}</span></td>
                </tr>
                <tr>
                    <td class='cell'><span class='label'>Phương tiện</span><span class='val'>{model.VehiclePlate}</span></td>
                    <td class='cell'><span class='label'>Quãng đường</span><span class='val'>{model.DistanceKm:N1} km</span></td>
                </tr>
            </table>
        </div>
        <div style='background:#F9FAFB; padding:20px; text-align:center; font-size:12px; color:#9CA3AF;'>
            DriveShare Platform &copy; {DateTime.Now.Year}
        </div>
    </div>
</body>
</html>";

            await SendEmailAsync(toEmail, subject, body);
        }

        // BLL/Services/Impletement/EmailService.cs

        /// <summary>
        /// Gửi Email chứa link xác thực tài khoản
        /// </summary>
        public async Task SendEmailVerificationLinkAsync(string email, string fullName, string verificationLink)
        {
            var subject = $"📧 [DriveShare] Xác thực tài khoản của bạn";
            var requestTime = DateTime.UtcNow.AddHours(7).ToString("dd/MM/yyyy HH:mm");

            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        /* CSS tương tự như các template khác (Header, Footer, Button) */
        body {{ font-family: 'Helvetica Neue', Helvetica, Arial, sans-serif; background-color: {BackgroundColor}; margin: 0; padding: 0; }}
        .wrapper {{ width: 100%; padding: 40px 0; }}
        .container {{ max-width: 600px; margin: 0 auto; background-color: {CardColor}; border-radius: 12px; overflow: hidden; box-shadow: 0 10px 25px rgba(0,0,0,0.05); }}
        .header {{ background: linear-gradient(135deg, {PrimaryColor} 0%, #0747A6 100%); padding: 24px; text-align: center; }}
        .logo {{ width: 48px; height: auto; filter: brightness(0) invert(1); }}
        .content {{ padding: 40px 32px; color: {TextColor}; }}
        .h1 {{ font-size: 24px; font-weight: 700; margin: 0 0 16px; color: {PrimaryColor}; text-align: center; letter-spacing: -0.5px; }}
        .p {{ font-size: 16px; line-height: 1.6; margin: 0 0 24px; color: #4B5563; }}
        .btn-container {{ text-align: center; margin-top: 30px; margin-bottom: 20px; }}
        .btn {{ background-color: #10B981; color: #ffffff; padding: 16px 40px; text-decoration: none; font-weight: 700; border-radius: 50px; font-size: 16px; display: inline-block; box-shadow: 0 4px 12px rgba(16, 185, 129, 0.4); transition: all 0.2s; }}
        .info-box {{ background-color: #F0F9FF; border: 1px solid #BAE6FD; border-radius: 8px; padding: 20px; margin-bottom: 32px; text-align: center; }}
        .info-text {{ font-size: 14px; color: #0C4A6E; font-weight: 600; }}
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

            <div class='content'>
                <h1 class='h1'>Kích Hoạt Tài Khoản</h1>
                
                <p class='p' style='text-align: center;'>
                    Xin chào <strong>{fullName}</strong>,<br>
                    Cảm ơn bạn đã đăng ký tài khoản tại DriveShare. Để hoàn tất việc đăng ký và bắt đầu sử dụng dịch vụ, vui lòng nhấp vào nút bên dưới để xác thực địa chỉ email của bạn.
                </p>

                <div class='btn-container'>
                    <a href='{verificationLink}' class='btn'>🔗 Xác Thực Email Ngay</a>
                </div>

                <div class='info-box'>
                    <p class='info-text'>
                        Liên kết xác thực sẽ hết hạn sau **24 giờ** kể từ {requestTime}.
                    </p>
                </div>

                <p class='p' style='font-size: 14px; text-align: center; color: #6B7280; margin-bottom: 0;'>
                    Nếu bạn không yêu cầu đăng ký tài khoản này, vui lòng bỏ qua email này.
                </p>
                <p class='p' style='font-size: 14px; text-align: center; color: #6B7280; margin-bottom: 0;'>
                    Hoặc sao chép đường link: <br><a href='{verificationLink}' style='font-size: 12px; word-break: break-all;'>{verificationLink}</a>
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
    }
}