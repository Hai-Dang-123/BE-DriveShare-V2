using BLL.Services.Interface;
using DAL.UnitOfWork;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DriverShareProject.Controllers
{
    [ApiController]
    [Route("api/webhook")]
    public class SepayWebhookController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWalletService _walletService;
        private readonly IEmailService _emailService;

        public SepayWebhookController(IUnitOfWork unitOfWork, IWalletService walletService, IEmailService emailService)
        {
            _unitOfWork = unitOfWork;
            _walletService = walletService;
            _emailService = emailService;
        }

        //[HttpPost("sepay")]
        //public async Task<IActionResult> SepayCallbackItemBooking([FromBody] SepayWebhookPayload payload)
        //{
        //    var match = Regex.Match(payload.Content ?? "", @"ITEMBOOKING[_]?([a-f0-9\-]+)", RegexOptions.IgnoreCase);
        //    if (!match.Success)
        //        return BadRequest(new { success = false, message = "Không tìm thấy OrderId trong content" });

        //    var orderIdStr = match.Groups[1].Value;
        //    if (!Guid.TryParse(orderIdStr, out var itemBookingId))
        //        return BadRequest(new { success = false, message = "OrderId không hợp lệ" });

        //    var itemBooking = await _unitOfWork.ItemBookingRepo.GetByIdAsync(itemBookingId);
        //    if (itemBooking == null)
        //        return NotFound(new { success = false, message = "Không tìm thấy quyên góp" });

        //    if (itemBooking.Status != BookingStatus.PENDING)
        //        return BadRequest(new { success = false, message = "Trạng thái không hợp lệ" });

        //    //donation.PaymentMethod = PaymentMethod.SEPAY;

        //    bool isSuccess =
        //        string.Equals(payload.TransferType, "in", StringComparison.OrdinalIgnoreCase) &&
        //        payload.TransferAmount > 0;

        //    itemBooking.Status = isSuccess ? BookingStatus.COMPLETED : BookingStatus.PENDING;



        //    await _unitOfWork.ItemBookingRepo.UpdateAsync(itemBooking);

        //    if (itemBooking.Status == BookingStatus.COMPLETED)
        //    {
        //        var ownerId = itemBooking.PostItem.UserId;

        //        var ownerWallet = await _unitOfWork.WalletRepo.GetByUserIdAsync(ownerId);
        //        if (ownerWallet != null)
        //        {
        //            ownerWallet.CurrentBalance += itemBooking.TotalPrice;
        //            await _unitOfWork.WalletRepo.UpdateAsync(ownerWallet);
        //        }

        //        //if (stationUserId.HasValue)
        //        //{
        //        //    userId = stationUserId.Value;

        //        //    var walletResponse = await _walletService.DepositAsync(userId.Value, donation.Amount);
        //        //    if (!walletResponse.IsSuccess)
        //        //        return StatusCode(500, new { success = false, message = "Cập nhật ví thất bại" });

        //        //    var stationUser = await _unitOfWork.UserRepo.GetByIdAsync(userId.Value);
        //        //    //if (stationUser != null && !string.IsNullOrWhiteSpace(stationUser.Email))
        //        //    //{
        //        //    //    await _emailService.SendEmailPayoutResultToStationAsync(
        //        //    //        stationUser.BussinessName,
        //        //    //        stationUser.Email,
        //        //    //        true
        //        //    //    );
        //        //    //}
        //        //}



        //    }

        //    await _unitOfWork.SaveChangeAsync();
        //    return StatusCode(201, new { success = true });
        //}


        //[HttpPost("sepay-item")]
        //public async Task<IActionResult> SepayCallbackItemBooking([FromBody] SepayWebhookPayload payload)
        //{
        //    if (payload == null)
        //        return BadRequest(new { success = false, message = "Payload không hợp lệ" });

        //    try
        //    {
        //        //_logger.LogInformation("Nhận webhook từ Sepay: {@Payload}", payload);

        //        // Tìm orderId trong nội dung chuyển khoản
        //        var match = Regex.Match(payload.Content ?? "", @"ITEMBOOKING[_]?([a-f0-9\-]+)", RegexOptions.IgnoreCase);
        //        if (!match.Success)
        //            return BadRequest(new { success = false, message = "Không tìm thấy OrderId trong nội dung" });

        //        var orderIdStr = match.Groups[1].Value;
        //        if (!Guid.TryParse(orderIdStr, out var itemBookingId))
        //            return BadRequest(new { success = false, message = "OrderId không hợp lệ" });

        //        var itemBooking = await _unitOfWork.ItemBookingRepo.GetByIdIncludePostItemAsync(itemBookingId);
        //        if (itemBooking == null)
        //            return NotFound(new { success = false, message = "Không tìm thấy quyên góp/đơn hàng" });

        //        if (itemBooking.Status != BookingStatus.PENDING)
        //        {
        //            //_logger.LogWarning("Đơn hàng {OrderId} có trạng thái {Status}, bỏ qua.", itemBooking.Id, itemBooking.Status);
        //            return Ok(new { success = true, message = "Đơn hàng đã được xử lý trước đó" });
        //        }

        //        // Kiểm tra tính hợp lệ giao dịch
        //        bool isPaymentIn = string.Equals(payload.TransferType, "in", StringComparison.OrdinalIgnoreCase);
        //        bool isValidAmount = payload.TransferAmount > 0;

        //        if (!isPaymentIn || !isValidAmount)
        //            return BadRequest(new { success = false, message = "Giao dịch không hợp lệ hoặc không phải chuyển vào" });

        //        // Cập nhật trạng thái đơn hàng
        //        itemBooking.Status = BookingStatus.COMPLETED;
        //        await _unitOfWork.ItemBookingRepo.UpdateAsync(itemBooking);

        //        // Cập nhật ví chủ sở hữu
        //        var ownerId = itemBooking.PostItem.UserId;
        //        var ownerWallet = await _unitOfWork.WalletRepo.GetByUserIdAsync(ownerId);
        //        if (ownerWallet != null)
        //        {
        //            ownerWallet.CurrentBalance += itemBooking.TotalPrice;
        //            await _unitOfWork.WalletRepo.UpdateAsync(ownerWallet);
        //        }
        //        //else
        //        //{
        //            //_logger.LogError("Không tìm thấy ví của người dùng {UserId}", ownerId);
        //        //}

        //        // Lưu thay đổi vào DB
        //        await _unitOfWork.SaveChangeAsync();

        //        // Có thể gửi email xác nhận (nếu cần)
        //        // await _emailService.SendPaymentSuccessEmailAsync(...);

        //        //_logger.LogInformation("Cập nhật thành công đơn hàng {OrderId}, số tiền: {Amount}", itemBooking.Id, payload.TransferAmount);

        //        return Created(string.Empty, new { success = true, message = "Cập nhật thành công" });
        //    }
        //    catch (FormatException ex)
        //    {
        //        //_logger.LogError(ex, "Lỗi định dạng ngày hoặc dữ liệu từ payload");
        //        return BadRequest(new { success = false, message = "Dữ liệu từ Sepay không hợp lệ" });
        //    }
        //    catch (Exception ex)
        //    {
        //        //_logger.LogError(ex, "Lỗi xử lý webhook Sepay");
        //        return StatusCode(500, new { success = false, message = "Lỗi hệ thống" });
        //    }
        //}

        //[HttpPost("sepay-Vehicle")]
        //public async Task<IActionResult> SepayCallbackVehicleBooking([FromBody] SepayWebhookPayload payload)
        //{
        //    if (payload == null)
        //        return BadRequest(new { success = false, message = "Payload không hợp lệ" });

        //    try
        //    {
        //        //_logger.LogInformation("Nhận webhook từ Sepay: {@Payload}", payload);

        //        // Tìm orderId trong nội dung chuyển khoản
        //        var match = Regex.Match(payload.Content ?? "", @"VEHICLEBOOKING[_]?([a-f0-9\-]+)", RegexOptions.IgnoreCase);
        //        if (!match.Success)
        //            return BadRequest(new { success = false, message = "Không tìm thấy OrderId trong nội dung" });

        //        var orderIdStr = match.Groups[1].Value;
        //        if (!Guid.TryParse(orderIdStr, out var itemBookingId))
        //            return BadRequest(new { success = false, message = "OrderId không hợp lệ" });

        //        var vehicleBooking = await _unitOfWork.VehicleBookingRepo.GetByIdIncludePostVehicleAsync(itemBookingId);
        //        if (vehicleBooking == null)
        //            return NotFound(new { success = false, message = "Không tìm thấy đơn hàng" });

        //        if (vehicleBooking.Status != BookingStatus.PENDING)
        //        {
        //            //_logger.LogWarning("Đơn hàng {OrderId} có trạng thái {Status}, bỏ qua.", itemBooking.Id, itemBooking.Status);
        //            return Ok(new { success = true, message = "Đơn hàng đã được xử lý trước đó" });
        //        }

        //        // Kiểm tra tính hợp lệ giao dịch
        //        bool isPaymentIn = string.Equals(payload.TransferType, "in", StringComparison.OrdinalIgnoreCase);
        //        bool isValidAmount = payload.TransferAmount > 0;

        //        if (!isPaymentIn || !isValidAmount)
        //            return BadRequest(new { success = false, message = "Giao dịch không hợp lệ hoặc không phải chuyển vào" });

        //        // Cập nhật trạng thái đơn hàng
        //        vehicleBooking.Status = BookingStatus.COMPLETED;
        //        await _unitOfWork.VehicleBookingRepo.UpdateAsync(vehicleBooking);

        //        // Cập nhật ví chủ sở hữu
        //        var ownerId = vehicleBooking.PostVehicle.OwnerId;
        //        var ownerWallet = await _unitOfWork.WalletRepo.GetByUserIdAsync(ownerId);
        //        if (ownerWallet != null)
        //        {
        //            ownerWallet.CurrentBalance += vehicleBooking.TotalPrice;
        //            await _unitOfWork.WalletRepo.UpdateAsync(ownerWallet);
        //        }
        //        //else
        //        //{
        //        //_logger.LogError("Không tìm thấy ví của người dùng {UserId}", ownerId);
        //        //}

        //        // Lưu thay đổi vào DB
        //        await _unitOfWork.SaveChangeAsync();

        //        // Có thể gửi email xác nhận (nếu cần)
        //        // await _emailService.SendPaymentSuccessEmailAsync(...);

        //        //_logger.LogInformation("Cập nhật thành công đơn hàng {OrderId}, số tiền: {Amount}", itemBooking.Id, payload.TransferAmount);

        //        return Created(string.Empty, new { success = true, message = "Cập nhật thành công" });
        //    }
        //    catch (FormatException ex)
        //    {
        //        //_logger.LogError(ex, "Lỗi định dạng ngày hoặc dữ liệu từ payload");
        //        return BadRequest(new { success = false, message = "Dữ liệu từ Sepay không hợp lệ" });
        //    }
        //    catch (Exception ex)
        //    {
        //        //_logger.LogError(ex, "Lỗi xử lý webhook Sepay");
        //        return StatusCode(500, new { success = false, message = "Lỗi hệ thống" });
        //    }
        //}


        //[HttpPost("sepay")]
        //public async Task<IActionResult> SepayCallback([FromBody] SepayWebhookPayload payload)
        //{
        //    if (payload == null)
        //        return BadRequest(new { success = false, message = "Payload không hợp lệ" });

        //    try
        //    {
        //        // Kiểm tra giao dịch có hợp lệ không
        //        bool isPaymentIn = string.Equals(payload.TransferType, "in", StringComparison.OrdinalIgnoreCase);
        //        bool isValidAmount = payload.TransferAmount > 0;
        //        if (!isPaymentIn || !isValidAmount)
        //            return BadRequest(new { success = false, message = "Giao dịch không hợp lệ hoặc không phải chuyển vào" });

        //        string content = payload.Content ?? "";

        //        // 🟢 1. Xử lý Item Booking
        //        if (content.Contains("ITEMBOOKING_", StringComparison.OrdinalIgnoreCase))
        //        {
        //            var match = Regex.Match(content, @"ITEMBOOKING[_]?([a-f0-9\-]+)", RegexOptions.IgnoreCase);
        //            if (!match.Success)
        //                return BadRequest(new { success = false, message = "Không tìm thấy ItemBookingId trong nội dung" });

        //            if (!Guid.TryParse(match.Groups[1].Value, out var itemBookingId))
        //                return BadRequest(new { success = false, message = "ItemBookingId không hợp lệ" });

        //            var itemBooking = await _unitOfWork.ItemBookingRepo.GetByIdIncludePostItemAsync(itemBookingId);
        //            if (itemBooking == null)
        //                return NotFound(new { success = false, message = "Không tìm thấy đơn hàng item" });

        //            if (itemBooking.Status != BookingStatus.PENDING)
        //                return Ok(new { success = true, message = "Đơn hàng item đã được xử lý trước đó" });

        //            itemBooking.Status = BookingStatus.COMPLETED;
        //            await _unitOfWork.ItemBookingRepo.UpdateAsync(itemBooking);

        //            // Cập nhật ví chủ sở hữu
        //            var ownerWallet = await _unitOfWork.WalletRepo.GetByUserIdAsync(itemBooking.PostItem.UserId);
        //            if (ownerWallet != null)
        //            {
        //                ownerWallet.CurrentBalance += itemBooking.TotalPrice;
        //                await _unitOfWork.WalletRepo.UpdateAsync(ownerWallet);
        //            }

        //            await _unitOfWork.SaveChangeAsync();
        //            return Ok(new { success = true, message = "Đã xử lý thanh toán ItemBooking" });
        //        }

        //        // 🟢 2. Xử lý Vehicle Booking
        //        else if (content.Contains("VEHICLEBOOKING_", StringComparison.OrdinalIgnoreCase))
        //        {
        //            var match = Regex.Match(content, @"VEHICLEBOOKING[_]?([a-f0-9\-]+)", RegexOptions.IgnoreCase);
        //            if (!match.Success)
        //                return BadRequest(new { success = false, message = "Không tìm thấy VehicleBookingId trong nội dung" });

        //            if (!Guid.TryParse(match.Groups[1].Value, out var vehicleBookingId))
        //                return BadRequest(new { success = false, message = "VehicleBookingId không hợp lệ" });

        //            var vehicleBooking = await _unitOfWork.VehicleBookingRepo.GetByIdIncludePostVehicleAsync(vehicleBookingId);
        //            if (vehicleBooking == null)
        //                return NotFound(new { success = false, message = "Không tìm thấy đơn hàng xe" });

        //            if (vehicleBooking.Status != BookingStatus.PENDING)
        //                return Ok(new { success = true, message = "Đơn hàng xe đã được xử lý trước đó" });

        //            vehicleBooking.Status = BookingStatus.COMPLETED;
        //            await _unitOfWork.VehicleBookingRepo.UpdateAsync(vehicleBooking);

        //            // Cập nhật ví chủ sở hữu
        //            var ownerWallet = await _unitOfWork.WalletRepo.GetByUserIdAsync(vehicleBooking.PostVehicle.OwnerId);
        //            if (ownerWallet != null)
        //            {
        //                ownerWallet.CurrentBalance += vehicleBooking.TotalPrice;
        //                await _unitOfWork.WalletRepo.UpdateAsync(ownerWallet);
        //            }

        //            await _unitOfWork.SaveChangeAsync();
        //            return Ok(new { success = true, message = "Đã xử lý thanh toán VehicleBooking" });
        //        }

        //        // 🟢 3. Xử lý Nạp tiền ví (Deposit)
        //        else if (content.Contains("DEPOSIT_", StringComparison.OrdinalIgnoreCase))
        //        {
        //            var match = Regex.Match(content, @"DEPOSIT[_]?([a-f0-9\-]+)", RegexOptions.IgnoreCase);
        //            if (!match.Success)
        //                return BadRequest(new { success = false, message = "Không tìm thấy UserId trong nội dung" });

        //            if (!Guid.TryParse(match.Groups[1].Value, out var userId))
        //                return BadRequest(new { success = false, message = "UserId không hợp lệ" });

        //            var wallet = await _unitOfWork.WalletRepo.GetByUserIdAsync(userId);
        //            if (wallet == null)
        //                return NotFound(new { success = false, message = "Không tìm thấy ví người dùng" });

        //            wallet.CurrentBalance += payload.TransferAmount;
        //            await _unitOfWork.WalletRepo.UpdateAsync(wallet);
        //            await _unitOfWork.SaveChangeAsync();

        //            return Ok(new { success = true, message = "Nạp tiền thành công" });
        //        }

        //        // 🚫 Không khớp loại nào
        //        return BadRequest(new { success = false, message = "Không xác định được loại giao dịch từ nội dung" });
        //    }
        //    catch (Exception ex)
        //    {
        //        //_logger.LogError(ex, "Lỗi xử lý webhook Sepay");
        //        return StatusCode(500, new { success = false, message = "Lỗi hệ thống", error = ex.Message });
        //    }
        //}


        public class SepayWebhookPayload
        {
            [JsonPropertyName("gateway")]
            public string Gateway { get; set; }

            [JsonPropertyName("transactionDate")]
            public string TransactionDateRaw { get; set; }

            [JsonIgnore]
            public DateTime TransactionDate
            {
                get
                {
                    if (DateTime.TryParseExact(TransactionDateRaw, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                        return parsed;

                    throw new FormatException("transactionDate không đúng định dạng 'yyyy-MM-dd HH:mm:ss'");
                }
            }

            [JsonPropertyName("accountNumber")]
            public string AccountNumber { get; set; }

            [JsonPropertyName("subAccount")]
            public string? SubAccount { get; set; }

            [JsonPropertyName("code")]
            public string? Code { get; set; }

            [JsonPropertyName("content")]
            public string Content { get; set; }

            [JsonPropertyName("transferType")]
            public string TransferType { get; set; }

            [JsonPropertyName("description")]
            public string Description { get; set; }

            [JsonPropertyName("transferAmount")]
            public decimal TransferAmount { get; set; }

            [JsonPropertyName("referenceCode")]
            public string ReferenceCode { get; set; }

            [JsonPropertyName("accumulated")]
            public decimal Accumulated { get; set; }

            [JsonPropertyName("id")]
            public long Id { get; set; }
        }
    }
}
