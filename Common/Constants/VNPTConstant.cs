using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Constants
{
    public static class VNPTEndpoints
    {
        public const string TokenEndpoint = "/auth/oauth/token";
        public const string CheckRegisterEndpoint = "/bill-service/project-service-plan/sub/check-register";
    }

    public static class VNPTChannelCodes
    {
        public const string EKYC = "eKYC";
        public const string EKYC_NFC = "eKYC_NFC";
        public const string EKYC_ID_CHECK = "eKYC_ID_CHECK";
    }
}
