using Nop.Web.Framework.Mvc.ModelBinding;
using Nop.Web.Framework.Mvc.Models;

namespace Nop.Plugin.Payments.PayGate.Models
{
    public class ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayGate.Fields.UseSandbox")]
        public bool UseSandbox { get; set; }
        public bool UseSandbox_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayGate.Fields.PayGateID")]
        public string PayGateID { get; set; }
        public bool PayGateID_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayGate.Fields.EncryptionKey")]
        public string EncryptionKey { get; set; }
        public bool EncryptionKey_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayGate.Fields.EnableIpn")]
        public bool EnableIpn { get; set; }
        public bool EnableIpn_OverrideForStore { get; set; }
    }
}