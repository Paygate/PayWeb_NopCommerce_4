using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Payments.PayGate.Models
{
    public record PaymentInfoModel : BaseNopModel
    {
        public readonly PayGatePaymentSettings payGatePaymentSettings;

        public PaymentInfoModel(PayGatePaymentSettings payGatePaymentSettings) {
            this.payGatePaymentSettings = payGatePaymentSettings;
        }

        [NopResourceDisplayName("Plugins.Payments.PayGate.Payment.UseCreditCard")]
        public bool UseCreditCard { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PayGate.Payment.UseBankTransfer")]
        public bool UseBankTransfer { get; set; }
        
        [NopResourceDisplayName("Plugins.Payments.PayGate.Payment.UseZapper")]
        public bool UseZapper { get; set; }
        
        [NopResourceDisplayName("Plugins.Payments.PayGate.Payment.UseSnapscan")]
        public bool UseSnapscan { get; set; }
        
        [NopResourceDisplayName("Plugins.Payments.PayGate.Payment.UsePaypal")]
        public bool UsePaypal { get; set; }
        
        [NopResourceDisplayName("Plugins.Payments.PayGate.Payment.UseMobicred")]
        public bool UseMobicred { get; set; }
        
        [NopResourceDisplayName("Plugins.Payments.PayGate.Payment.UseMomoPay")]
        public bool UseMomoPay { get; set; }
        
        [NopResourceDisplayName("Plugins.Payments.PayGate.Payment.UseScanToPay")]
        public bool UseScanToPay { get; set; }
        
        [NopResourceDisplayName("Plugins.Payments.PayGate.Payment.UseSamsungPay")]
        public bool UseSamsungPay { get; set; }
        
        [NopResourceDisplayName("Plugins.Payments.PayGate.Payment.UseApplePay")]
        public bool UseApplePay { get; set; }
        
        [NopResourceDisplayName("Plugins.Payments.PayGate.Payment.UseRcsPay")]
        public bool UseRcsPay { get; set; }        
    }
}
