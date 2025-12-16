using BLL.Services.Interface;
using Common.Settings;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace BLL.Services.Impletement
{
    public class SepayService : ISepayService
    {

        //private readonly IHttpClientFactory _httpClientFactory;
        //private readonly string _sepayToken;
        //public SepayService(IHttpClientFactory httpClientFactory, IOptions<SePaySetting> sepayOptions)
        //{
        //    _httpClientFactory = httpClientFactory;
        //    _sepayToken = sepayOptions.Value.Token;
        //}

        //public async Task<string> CreateSepayQRForItemBooking(ItemBooking itemBooking)
        //{
        //    //    // 🏦 Thông tin tài khoản ngân hàng của tổ chức bạn (cấu hình cố định)
        //    var bankCode = "MBBank";
        //    var accountNumber = "0337147985";
        //    var template = "compact";

        //    // 💰 Thông tin từ donation
        //    var amount = (int)itemBooking.TotalPrice;
        //    var itemBookingId = itemBooking.ItemBookingId.ToString();
        //    var description = $"ITEMBOOKING_{itemBookingId}";

        //    // ✅ Encode nội dung chuyển khoản để đảm bảo URL hợp lệ
        //    var encodedDes = HttpUtility.UrlEncode(description);

        //    // 📷 Tạo URL ảnh QR từ SePay
        //    var qrUrl = $"https://qr.sepay.vn/img?bank={bankCode}&acc={accountNumber}&amount={amount}&des={encodedDes}&template={template}";

        //    return qrUrl;

        //}

        //public async Task<string> CreateSepayQRForVehicleBooking(VehicleBooking vehicleBooking)
        //{
        //    //    // 🏦 Thông tin tài khoản ngân hàng của tổ chức bạn (cấu hình cố định)
        //    var bankCode = "MBBank";
        //    var accountNumber = "0337147985";
        //    var template = "compact";

        //    // 💰 Thông tin từ donation
        //    var amount = (int)vehicleBooking.TotalPrice;
        //    var vehicleBookingId = vehicleBooking.VehicleBookingId.ToString();
        //    var description = $"VEHICLEBOOKING_{vehicleBookingId}";

        //    // ✅ Encode nội dung chuyển khoản để đảm bảo URL hợp lệ
        //    var encodedDes = HttpUtility.UrlEncode(description);

        //    // 📷 Tạo URL ảnh QR từ SePay
        //    var qrUrl = $"https://qr.sepay.vn/img?bank={bankCode}&acc={accountNumber}&amount={amount}&des={encodedDes}&template={template}";
        //    return qrUrl;
        //}

        // Thông tin ngân hàng nhận tiền (Cấu hình cứng hoặc lấy từ AppSettings)
        private readonly string _bankCode = "MBBank"; // Ví dụ: MBBank, VCB...
        private readonly string _accountNumber = "0337147985";
        private readonly string _template = "compact";

        public string CreateSepayQR(decimal amount, string description)
        {
            // ✅ Encode nội dung chuyển khoản để đảm bảo URL hợp lệ
            var encodedDes = HttpUtility.UrlEncode(description);

            // 📷 Tạo URL ảnh QR từ SePay
            // Mẫu: https://qr.sepay.vn/img?bank=...&acc=...&amount=...&des=...
            var qrUrl = $"https://qr.sepay.vn/img?bank={_bankCode}&acc={_accountNumber}&amount={amount}&des={encodedDes}&template={_template}";

            return qrUrl;
        }
    }
}
