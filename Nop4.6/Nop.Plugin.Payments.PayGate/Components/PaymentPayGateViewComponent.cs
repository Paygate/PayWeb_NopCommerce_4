using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Payments.PayGate.Models;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.PayGate.Components
{
    [ViewComponent(Name = "PaymentPayGate")]
    public class PaymentPayGateViewComponent : NopViewComponent
    {
        private PayGatePaymentSettings payGatePaymentSettings;

        public PaymentPayGateViewComponent(PayGatePaymentSettings payGatePaymentSettings) {
            this.payGatePaymentSettings = payGatePaymentSettings;
        }
        public IViewComponentResult Invoke()
        {
            var model = new PaymentInfoModel(this.payGatePaymentSettings);

            return View("~/Plugins/Payments.PayGate/Views/PaymentInfo.cshtml", model);
        }
    }
}
