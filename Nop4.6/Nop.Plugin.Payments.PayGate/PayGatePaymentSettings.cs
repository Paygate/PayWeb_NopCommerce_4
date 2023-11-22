using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.PayGate
{
    public class PayGatePaymentSettings : ISettings
    {
        public bool TestMode { get; set; }
        public string PayGateID { get; set; }
        public string EncryptionKey { get; set; }
        public bool DisableIpn { get; set; }
        public bool UseSSL { get; set; }
        public bool EnableCreditCard { get; set; }
        public bool EnableBankTransfer { get; set; }
        public bool EnableZapper { get; set; }
        public bool EnableSnapscan { get; set; }
        public bool EnablePaypal { get; set; }
        public bool EnableMobicred { get; set; }
        public bool EnableMomoPay { get; set; }
        public bool EnableScanToPay { get; set; }
        public bool EnableSamsungPay { get; set; }
        public bool EnableApplePay { get; set; }
        public bool EnableRcsPay { get; set; }

    }
}