using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Payments.PayGate.Models
{
    public class ConfigurationModel : BaseNopModel
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

        [NopResourceDisplayName("Plugins.Payments.PayGate.Fields.EnableIpn")]
        public bool EnableIpn { get; set; }
        public bool EnableIpn_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayGate.Fields.EnableRedirect")]
        public bool EnableRedirect { get; set; }
        public bool EnableRedirect_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayGate.Fields.UseSSL")]
        public bool UseSSL { get; set; }
        public bool UseSSL_OverrideForStore { get; set; }
    }
}