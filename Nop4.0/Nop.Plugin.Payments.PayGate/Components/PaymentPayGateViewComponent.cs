using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.PayGate.Components
{
    [ViewComponent(Name = "PaymentPayGate")]
    public class PaymentPayGateViewComponent : NopViewComponent
    {
        public IViewComponentResult Invoke()
        {
            return View("~/Plugins/Payments.PayGate/Views/PaymentInfo.cshtml");
        }
    }
}
