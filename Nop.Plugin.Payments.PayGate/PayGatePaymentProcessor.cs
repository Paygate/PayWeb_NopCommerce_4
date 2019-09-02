using System;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Services.Plugins;
using Nop.Plugin.Payments.PayGate.Controllers;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Payments;
using Nop.Web.Framework;

namespace Nop.Plugin.Payments.PayGate
{
    /// <summary>
    /// PayGate payment processor
    /// </summary>
    public class PayGatePaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly PayGatePaymentSettings _payGatePaymentSettings;
        private readonly ISettingService _settingService;
        private readonly ICurrencyService _currencyService;
        private readonly CurrencySettings _currencySettings;
        private readonly IWebHelper _webHelper;
        private readonly ILocalizationService _localizationService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private Dictionary<string, string> dictionaryResponse;
        #endregion

        #region Ctor

        public PayGatePaymentProcessor(PayGatePaymentSettings payGatePaymentSettings,
            ISettingService settingService, ICurrencyService currencyService,
            CurrencySettings currencySettings, IWebHelper webHelper,
            ILocalizationService localizationService,
            IHttpContextAccessor httpContextAccessor
            )
        {
            this._payGatePaymentSettings = payGatePaymentSettings;
            this._settingService = settingService;
            this._currencyService = currencyService;
            this._currencySettings = currencySettings;
            this._webHelper = webHelper;
            this._localizationService = localizationService;
            this._httpContextAccessor = httpContextAccessor;
        }

        #endregion

        #region Utilities

        #endregion

        #region Methods

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.NewPaymentStatus = PaymentStatus.Pending;
            return result;
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            var orderTotal = Math.Round(postProcessPaymentRequest.Order.OrderTotal, 2);

            using (var client = new WebClient())
            {
                var initiateData = new NameValueCollection();
                initiateData["PAYGATE_ID"] = _payGatePaymentSettings.PayGateID;
                initiateData["REFERENCE"] = postProcessPaymentRequest.Order.Id.ToString();
                initiateData["AMOUNT"] = (Convert.ToDouble(orderTotal) * 100).ToString();
                initiateData["CURRENCY"] = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId).CurrencyCode;
				var storeLocation = _webHelper.GetStoreLocation(false);
                if (_payGatePaymentSettings.UseSSL)
                {
                    storeLocation = storeLocation.Replace("http://", "https://");
                }
                initiateData["RETURN_URL"] = storeLocation + "Plugins/PaymentPayGate/PayGateReturnHandler?pgnopcommerce=" + postProcessPaymentRequest.Order.Id.ToString();
                initiateData["TRANSACTION_DATE"] = String.Format("{0:yyyy-MM-dd HH:mm:ss}", DateTime.Now).ToString();
                initiateData["LOCALE"] = "en-za";
                initiateData["COUNTRY"] = postProcessPaymentRequest.Order.BillingAddress.Country.ThreeLetterIsoCode;
                initiateData["EMAIL"] = postProcessPaymentRequest.Order.BillingAddress.Email;
                //initiateData["NOTIFY_URL"] = _webHelper.GetStoreLocation(false) + "Plugins/PaymentPayGate/PayGateNotifyHandler?pgnopcommerce=" + postProcessPaymentRequest.Order.Id.ToString();
                initiateData["USER3"] = "nopcommerce-v1.0.0";

                string initiateValues = string.Join("", initiateData.AllKeys.Select(key => initiateData[key]));

                initiateData["CHECKSUM"] = new PayGateHelper().CalculateMD5Hash(initiateValues + _payGatePaymentSettings.EncryptionKey);
                var initiateResponse = client.UploadValues("https://secure.paygate.co.za/payweb3/initiate.trans", "POST", initiateData);
                dictionaryResponse = Encoding.Default.GetString(initiateResponse)
                                         .Split('&')
                                         .Select(p => p.Split('='))
                                         .ToDictionary(p => p[0], p => p.Length > 1 ? Uri.UnescapeDataString(p[1]) : null);
            }

            // New Code to fix it
            RemotePost remotePost = new RemotePost();
            remotePost.FormName = "PayGate";
            remotePost.Method = "POST";
            remotePost.Url = "https://secure.paygate.co.za/payweb3/process.trans";
            remotePost.Add("PAY_REQUEST_ID", dictionaryResponse["PAY_REQUEST_ID"]);
            remotePost.Add("CHECKSUM", dictionaryResponse["CHECKSUM"]);
            remotePost.Post();
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return false;
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>Additional handling fee</returns>
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart) { return 0; }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            result.AddError("Capture method not supported");
            return result;
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            result.AddError("Refund method not supported");
            return result;
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();
            result.AddError("Void method not supported");
            return result;
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.AddError("Recurring payment not supported");
            return result;
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var result = new CancelRecurringPaymentResult();
            result.AddError("Recurring payment not supported");
            return result;
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");

            //let's ensure that at least 5 seconds passed after order is placed
            //P.S. there's no any particular reason for that. we just do it
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalSeconds < 5)
                return false;

            return true;
        }

        /// <summary>
        /// Validate payment form
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>List of validating errors</returns>
        public IList<string> ValidatePaymentForm(IFormCollection form)
        {
            return new List<string>();
        }

        /// <summary>
        /// Get payment information
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>Payment info holder</returns>
        public ProcessPaymentRequest GetPaymentInfo(IFormCollection form)
        {
            return new ProcessPaymentRequest();
        }

        /// <summary>
        /// Gets a view component for displaying plugin in public store ("payment info" checkout step)
        /// </summary>
        /// <param name="viewComponentName">View component name</param>
        public void GetPublicViewComponent(out string viewComponentName)
        {
            viewComponentName = "PaymentPayGate";
        }

        /// <summary>
        /// Gets a name of a view component for displaying plugin in public store ("payment info" checkout step)
        /// </summary>
        /// <returns>View component name</returns>
        public string GetPublicViewComponentName()
        {
            return "PaymentPayGate";
        }

        /// <summary>
        /// Gets a route for provider configuration
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "PaymentPayGate";
            routeValues = new RouteValueDictionary { { "Namespaces", "Nop.Plugin.Payments.PayGate.Controllers" }, { "area", null } };
        }

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentPayGate/Configure";
        }

        /// <summary>
        /// Gets a route for payment info
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetPaymentInfoRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "PaymentInfo";
            controllerName = "PaymentPayGate";
            routeValues = new RouteValueDictionary { { "Namespaces", "Nop.Plugin.Payments.PayGate.Controllers" }, { "area", null } };
        }

        public Type GetControllerType()
        {
            return typeof(PaymentPayGateController);
        }

        public override void Install()
        {
            //settings
            var settings = new PayGatePaymentSettings
            {
                UseSandbox = true,
                PayGateID = "10011072130",
                EncryptionKey = "secret",
                UseSSL = false,
                EnableIpn = false
            };
            _settingService.SaveSetting(settings);

            //locales
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayGate.Fields.RedirectionTip", "You will be redirected to PayGate site to complete the order.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayGate.Fields.UseSandbox", "Use Sandbox");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayGate.Fields.UseSandbox.Hint", "Check to enable Sandbox (testing environment).");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayGate.Fields.PayGateID", "PayGate ID");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayGate.Fields.PayGateID.Hint", "Specify your PayGate ID.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayGate.Fields.EncryptionKey", "Encryption Key");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayGate.Fields.EncryptionKey.Hint", "Specify Encryption Key");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayGate.Fields.EnableIpn", "Enable IPN (Instant Payment Notification)");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayGate.Fields.EnableIpn.Hint", "Check if IPN is enabled.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayGate.Fields.EnableIpn.Hint2", "Leave blank to use the default IPN handler URL.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayGate.PaymentMethodDescription", "Pay by Credit/Debit Card");
			_localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayGate.Fields.UseSSL", "Use SSL for Store");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayGate.Fields.UseSSL.Hint", "Enforce use of SSL for Store.");

            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PayGate.Instructions", @"<p>

<b>Open an Account:</b>
                <br />
                <br />
                1.	You need a Merchant Account with PayGate to accept online payments. Register a new
                Merchant Account with PayGate by completing the online registration form at
                <a href=""https://www.paygate.co.za/get-started-with-paygate/"">https://www.paygate.co.za/get-started-with-paygate/</a>
                <br />
                <br />
                2.  One of our sales agents will contact you to complete the registration process.
                <br />
                3.  You will be provided with your Paygate Merchant Credentials required to set up your Store.
                <br /></p>");


            base.Install();
        }

        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<PayGatePaymentSettings>();

            //locales
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayGate.Fields.RedirectionTip");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayGate.Fields.UseSandbox");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayGate.Fields.UseSandbox.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayGate.Fields.PayGateID");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayGate.Fields.PayGateID.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayGate.Fields.EncryptionKey");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayGate.Fields.EncryptionKey.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayGate.Fields.EnableIpn");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayGate.Fields.EnableIpn.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayGate.Fields.EnableIpn.Hint2");
			_localizationService.DeletePluginLocaleResource("Plugins.Payments.PayGate.Fields.UseSSL");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayGate.Fields.UseSSL.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayGate.Fields.UseSSL.Hint2");

            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PayGate.Instructions");

            base.Uninstall();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public string PaymentMethodDescription
        {
            //return description of this payment method to be display on "payment method" checkout step. good practice is to make it localizable
            //for example, for a redirection payment method, description may be like this: "You will be redirected to PayGate site to complete the payment"
            get { return _localizationService.GetResource("Plugins.Payments.PayGate.PaymentMethodDescription"); }
        }

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get
            {
                return RecurringPaymentType.NotSupported;
            }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get
            {
                return PaymentMethodType.Redirection;
            }
        }

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo
        {
            get
            {
                return false;
            }
        }

        public string X2 { get; private set; }

        #endregion
    }
}
