using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Payments.PayGate.Models
{
    public record ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayGate.Fields.TestMode")]
        public bool TestMode { get; set; }
        public bool TestMode_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayGate.Fields.PayGateID")]
        public string PayGateID { get; set; }
        public bool PayGateID_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayGate.Fields.EncryptionKey")]
        public string EncryptionKey { get; set; }
        public bool EncryptionKey_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayGate.Fields.DisableIpn")]
        public bool DisableIpn { get; set; }
        public bool DisableIpn_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayGate.Fields.UseSSL")]
        public bool UseSSL { get; set; }
        public bool UseSSL_OverrideForStore { get; set; }
        
        [NopResourceDisplayName("Plugins.Payments.PayGate.Fields.EnableCreditCard")]
        public bool EnableCreditCard { get; set; }
        public bool EnableCreditCard_OverrideForStore { get; set; }
        
        [NopResourceDisplayName("Plugins.Payments.PayGate.Fields.EnableBankTransfer")]
        public bool EnableBankTransfer { get; set; }
        public bool EnableBankTransfer_OverrideForStore { get; set; }
        
        [NopResourceDisplayName("Plugins.Payments.PayGate.Fields.EnableZapper")]
        public bool EnableZapper { get; set; }
        public bool EnableZapper_OverrideForStore { get; set; }
        
        [NopResourceDisplayName("Plugins.Payments.PayGate.Fields.EnableSnapscan")]
        public bool EnableSnapscan { get; set; }
        public bool EnableSnapscan_OverrideForStore { get; set; }
        
        [NopResourceDisplayName("Plugins.Payments.PayGate.Fields.EnablePaypal")]
        public bool EnablePaypal { get; set; }
        public bool EnablePaypal_OverrideForStore { get; set; }
        
        [NopResourceDisplayName("Plugins.Payments.PayGate.Fields.EnableMobicred")]
        public bool EnableMobicred { get; set; }
        public bool EnableMobicred_OverrideForStore { get; set; }
        
        [NopResourceDisplayName("Plugins.Payments.PayGate.Fields.EnableMomoPay")]
        public bool EnableMomoPay { get; set; }
        public bool EnableMomoPay_OverrideForStore { get; set; }
        
        [NopResourceDisplayName("Plugins.Payments.PayGate.Fields.EnableScanToPay")]
        public bool EnableScanToPay { get; set; }
        public bool EnableScanToPay_OverrideForStore { get; set; }
        
        [NopResourceDisplayName("Plugins.Payments.PayGate.Fields.EnableSamsungPay")]
        public bool EnableSamsungPay { get; set; }
        public bool EnableSamsungPay_OverrideForStore { get; set; }
        
        [NopResourceDisplayName("Plugins.Payments.PayGate.Fields.EnableApplePay")]
        public bool EnableApplePay { get; set; }
        public bool EnableApplePay_OverrideForStore { get; set; }
        
        [NopResourceDisplayName("Plugins.Payments.PayGate.Fields.EnableRcsPay")]
        public bool EnableRcsPay { get; set; }
        public bool EnableRcsPay_OverrideForStore { get; set; }
    }
}