using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.PayGate
{
    public class PayGatePaymentSettings : ISettings
    {
        public bool UseSandbox { get; set; }
        public string PayGateID { get; set; }
        public string EncryptionKey { get; set; }
        public bool EnableIpn { get; set; }

    }
}