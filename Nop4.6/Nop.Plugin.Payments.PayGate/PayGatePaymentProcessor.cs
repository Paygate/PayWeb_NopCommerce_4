using System;
using System.Collections.Generic;
using System.Linq;
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
using Nop.Services.Logging;
using Nop.Services.Customers;
using Microsoft.AspNetCore.Http.Features;
using System.Net.Http;
using Nop.Plugin.Payments.PayGate.Components;
using Nop.Plugin.Payments.PayGate.Models;
using Nop.Data;

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
        private readonly ICustomerService _customerService;
        private readonly ICountryService _countryService;
        private readonly IWebHelper _webHelper;
        private readonly ILocalizationService _localizationService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private Dictionary<string, string> dictionaryResponse;
        private ILogger defaultLogger;
        private readonly IRepository<OrderPayMethodRecord> _repository;

        private String payMethod = "";
        #endregion

        #region Ctor

        public PayGatePaymentProcessor(PayGatePaymentSettings payGatePaymentSettings,
            ISettingService settingService, ICurrencyService currencyService,
            CurrencySettings currencySettings, ICustomerService customerService, ICountryService countryService,
            IWebHelper webHelper,
            ILocalizationService localizationService,
            IHttpContextAccessor httpContextAccessor,
            ILogger logger,
            IRepository<OrderPayMethodRecord> repository
            )
        {
            _payGatePaymentSettings = payGatePaymentSettings;
            _settingService = settingService;
            _currencyService = currencyService;
            _countryService = countryService;
            _customerService = customerService;
            _currencySettings = currencySettings;
            _webHelper = webHelper;
            _localizationService = localizationService;
            _httpContextAccessor = httpContextAccessor;
            defaultLogger = logger;
            _repository = repository;
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
        public async Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            payMethod = (string)processPaymentRequest.CustomValues["payMethod"];

            var record = new OrderPayMethodRecord();
            //prepare order details
            record.OrderGuid = processPaymentRequest.OrderGuid.ToString();
            record.PayMethod = payMethod;

            _repository.Insert(record);

            var result = new ProcessPaymentResult();
            result.NewPaymentStatus = PaymentStatus.Pending;
            //return Task.FromResult(result);
            return result;
        }

        //public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        //{
        //    this.defaultLogger.Information("Calling async");
        //    await PostProcessPaymentAsync(postProcessPaymentRequest);
        //    this.defaultLogger.Information("After async");
        //}

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            var orderTotal = Math.Round(postProcessPaymentRequest.Order.OrderTotal, 2);
            var testMode = _payGatePaymentSettings.TestMode;
            var encryptionKey = "";
            var initiated = false;

            using (var client = new HttpClient())
            {
                //var initiateData = new NameValueCollection();
                var initiateData = new Dictionary<String, String>();
                if (testMode)
                {
                    initiateData["PAYGATE_ID"] = "10011072130";
                    encryptionKey = "secret";
                    await this.defaultLogger.InformationAsync("Using test mode");
                }
                else
                {
                    initiateData["PAYGATE_ID"] = _payGatePaymentSettings.PayGateID;
                    encryptionKey = _payGatePaymentSettings.EncryptionKey;
                }
                initiateData["REFERENCE"] = postProcessPaymentRequest.Order.CustomOrderNumber.ToString();
                initiateData["AMOUNT"] = Convert.ToInt32(Convert.ToDouble(orderTotal) * 100).ToString();
                initiateData["CURRENCY"] = (await _currencyService.GetCurrencyByIdAsync(_currencySettings.PrimaryStoreCurrencyId)).CurrencyCode;

                var storeLocation = _webHelper.GetStoreLocation(false);
                if (_payGatePaymentSettings.UseSSL)
                {
                    storeLocation = storeLocation.Replace("http://", "https://");
                }
                initiateData["RETURN_URL"] = storeLocation + "Plugins/PaymentPayGate/PayGateReturnHandler?pgnopcommerce=" + postProcessPaymentRequest.Order.Id.ToString();
                initiateData["TRANSACTION_DATE"] = String.Format("{0:yyyy-MM-dd HH:mm:ss}", DateTime.Now).ToString();
                initiateData["LOCALE"] = "en-za";

                var threeLetterIsoCode = "";
                var billingEmail = "";

                var customer = await _customerService.GetCustomerByIdAsync(postProcessPaymentRequest.Order.CustomerId);
                if (customer != null)
                {
                    var billingAddress = await _customerService.GetCustomerBillingAddressAsync(customer);

                    if (billingAddress != null)
                    {
                        billingEmail = billingAddress.Email;

                        var country = await _countryService.GetCountryByAddressAsync(billingAddress);
                        if (country != null && !string.IsNullOrWhiteSpace(country.ThreeLetterIsoCode))
                        {
                            threeLetterIsoCode = country.ThreeLetterIsoCode;
                        }
                    }
                }

                initiateData["COUNTRY"] = threeLetterIsoCode;
                initiateData["EMAIL"] = billingEmail;

                var orderGuid = postProcessPaymentRequest.Order.OrderGuid.ToString();
                var records = _repository.GetAll(query =>
                {
                    query = query.Where(record => record.OrderGuid == orderGuid);

                    return query;
                });

                var record = new OrderPayMethodRecord();

                if (records.Any())
                {
                    record = records.First();
                    payMethod = record.PayMethod;

                    // Remove record from db as we no longer need it
                    _repository.Delete(record);
                }

                //Use specific pay method if selected
                if (payMethod != "")
                {
                    switch (payMethod)
                    {
                        case "UseCreditCard":
                            initiateData["PAY_METHOD"] = "CC";
                            initiateData["PAY_METHOD_DETAIL"] = "Credit Card";
                            break;
                        case "UseBankTransfer":
                            initiateData["PAY_METHOD"] = "BT";
                            break;
                        case "UseZapper":
                            initiateData["PAY_METHOD"] = "EW";
                            initiateData["PAY_METHOD_DETAIL"] = "ZAPPER";
                            break;
                        case "UseSnapscan":
                            initiateData["PAY_METHOD"] = "EW";
                            initiateData["PAY_METHOD_DETAIL"] = "SNAPSCAN";
                            break;
                        case "UsePaypal":
                            initiateData["PAY_METHOD"] = "EW";
                            initiateData["PAY_METHOD_DETAIL"] = "PAYPAL";
                            break;
                        case "UseMobicred":
                            initiateData["PAY_METHOD"] = "EW";
                            initiateData["PAY_METHOD_DETAIL"] = "MOBICRED";
                            break;
                        case "UseMomoPay":
                            initiateData["PAY_METHOD"] = "EW";
                            initiateData["PAY_METHOD_DETAIL"] = "MOMOPAY";
                            break;
                        case "UseScanToPay":
                            initiateData["PAY_METHOD"] = "EW";
                            initiateData["PAY_METHOD_DETAIL"] = "MASTERPASS";
                            break;
                        case "UseSamsungPay":
                            initiateData["PAY_METHOD"] = "EW";
                            initiateData["PAY_METHOD_DETAIL"] = "SAMSUNGPAY";
                            break;
                        case "UseApplePay":
                            initiateData["PAY_METHOD"] = "CC";
                            initiateData["PAY_METHOD_DETAIL"] = "APPLEPAY";
                            break;
                        case "UseRcsPay":
                            initiateData["PAY_METHOD"] = "EW";
                            initiateData["PAY_METHOD_DETAIL"] = "RCS";
                            break;
                    }
                }

                if (!_payGatePaymentSettings.DisableIpn)
                {
                    initiateData["NOTIFY_URL"] = storeLocation + "Plugins/PaymentPayGate/PayGateNotifyHandler?pgnopcommerce=" + postProcessPaymentRequest.Order.Id.ToString();
                }
                initiateData["USER1"] = postProcessPaymentRequest.Order.Id.ToString();
                initiateData["USER3"] = "nopcommerce-v4.6.0";

                string initiateValues = "";
                foreach (var value in initiateData)
                {
                    initiateValues += value.Value;
                }

                initiateData["CHECKSUM"] = new PayGateHelper().CalculateMD5Hash(initiateValues + encryptionKey);

                var cnt = 0;
                while (!initiated && cnt < 5)
                {
                    var initiateContent = new FormUrlEncodedContent((IEnumerable<KeyValuePair<string, string>>)initiateData);
                    var response = await client.PostAsync("https://secure.paygate.co.za/payweb3/initiate.trans", initiateContent);
                    var initiateResponse = await response.Content.ReadAsStringAsync();
                    await defaultLogger.InformationAsync("Initiate response: " + initiateResponse + " cnt=" + cnt);
                    dictionaryResponse = initiateResponse
                                             .Split('&')
                                             .Select(p => p.Split('='))
                                             .ToDictionary(p => p[0], p => p.Length > 1 ? Uri.UnescapeDataString(p[1]) : null);
                    if (dictionaryResponse.Count == 4 && dictionaryResponse.ContainsKey("PAY_REQUEST_ID"))
                    {
                        await defaultLogger.InformationAsync("PAYGATE_ID = " + dictionaryResponse["PAYGATE_ID"]);
                        await defaultLogger.InformationAsync("PAY_REQUEST_ID = " + dictionaryResponse["PAY_REQUEST_ID"]);
                        await defaultLogger.InformationAsync("REFERENCE = " + dictionaryResponse["REFERENCE"]);
                        await defaultLogger.InformationAsync("CHECKSUM = " + dictionaryResponse["CHECKSUM"]);
                        initiated = true;
                    }
                    cnt++;
                }

                // Redirect to payment portal
                if (initiated)
                {
                    _webHelper.IsPostBeingDone = true;
                    try
                    {
                        await defaultLogger.InformationAsync("Is initiated");
                        var sb = new StringBuilder();
                        var Url = "https://secure.paygate.co.za/payweb3/process.trans";
                        var payRequestId = dictionaryResponse["PAY_REQUEST_ID"];
                        var checksum = dictionaryResponse["CHECKSUM"];
                        sb.Append("<html><head></head>");
                        sb.Append("<body>");
                        sb.Append("<form id=\"PayGate_Form\" method=\"post\" action=\"" + Url + "\" >");
                        sb.Append("<input type=\"hidden\" name=\"PAY_REQUEST_ID\" value=\"" + payRequestId + "\" >");
                        sb.Append("<input type=\"hidden\" name=\"CHECKSUM\" value=\"" + checksum + "\" >");
                        sb.Append("<script>document.getElementById('PayGate_Form').submit();</script>");
                        sb.Append("</form></body></html>");

                        // Synchronous operations disabled by default in DotnetCore >= 3.0
                        var feat = _httpContextAccessor.HttpContext.Features.Get<IHttpBodyControlFeature>();
                        if (feat != null)
                        {
                            feat.AllowSynchronousIO = true;
                        }

                        var response = _httpContextAccessor.HttpContext.Response;
                        var data = Encoding.UTF8.GetBytes(sb.ToString());
                        response.ContentType = "text/html; charset=utf-8";
                        response.ContentLength = data.Length;
                        await defaultLogger.InformationAsync("Start write to body: " + sb.ToString());
                        response.Body.Write(data, 0, data.Length);
                        response.Body.Flush();
                        await defaultLogger.InformationAsync("End write to body: " + sb.ToString());
                        //await Task.Delay(3000);
                        await defaultLogger.InformationAsync("End three second delay: " + sb.ToString());
                    }
                    catch (Exception e)
                    {
                        await defaultLogger.ErrorAsync("Failed to POST: " + e.Message);
                    }
                }
                else
                {
                    await defaultLogger.ErrorAsync("Failed to get valid initiate response after 5 attempts");
                }
            }
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return Task.FromResult(false);
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>Additional handling fee</returns>
        public async Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
        {
            return 0.00M;
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            result.AddError("Capture method not supported");
            return Task.FromResult(result);
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            result.AddError("Refund method not supported");
            return Task.FromResult(result);
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();
            result.AddError("Void method not supported");
            return Task.FromResult(result);
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.AddError("Recurring payment not supported");
            return Task.FromResult(result);
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var result = new CancelRecurringPaymentResult();
            result.AddError("Recurring payment not supported");
            return Task.FromResult(result);
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public Task<bool> CanRePostProcessPaymentAsync(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");

            //let's ensure that at least 5 seconds passed after order is placed
            //P.S. there's no any particular reason for that. we just do it
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalSeconds < 5)
                return Task.FromResult(false);

            return Task.FromResult(true);
        }

        /// <summary>
        /// Validate payment form
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>List of validating errors</returns>
        public Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form)
        {
            return Task.FromResult<IList<string>>(new List<string>());
        }

        /// <summary>
        /// Get payment information
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>Payment info holder</returns>
        public Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
        {
            var processPaymentRequest = new ProcessPaymentRequest();
            var payMethod = "";

            if (form.Keys.Count > 1)
            {
                var method = form["pay-methods"];
                payMethod = method[0];                
            }
            processPaymentRequest.CustomValues["payMethod"] = payMethod;

            return Task.FromResult(processPaymentRequest);
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

        public override async Task InstallAsync()
        {
            //settings
            var settings = new PayGatePaymentSettings
            {
                TestMode = true,
                PayGateID = "10011072130",
                EncryptionKey = "secret",
                UseSSL = true,
                DisableIpn = false,
                EnableSamsungPay = false,
                EnableApplePay = false,
                EnableRcsPay = false,
            };
            await _settingService.SaveSettingAsync(settings);

            //locales
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.RedirectionTip", "You will be redirected to PayGate site to complete the order.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.TestMode", "Test mode");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.TestMode.Hint", "Uses a PayGate test account. Request test cards from PayGate.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.PayGateID", "PayGate ID");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.PayGateID.Hint", "This is the PayGate ID, received from PayGate. <br><br><strong>PayGate ID currently in use. (10011072130 )</strong>");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EncryptionKey", "Encryption Key");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EncryptionKey.Hint", "This is the Encryption Key set in the PayGate Back Office. <br><br>PayGate Encryption Key currently in use. (secret )</strong>");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.DisableIpn", "Disable IPN");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.DisableIpn.Hint", "Disable IPN notify method and use redirect method instead.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.PaymentMethodDescription", "Pay by Credit/Debit Card");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.UseSSL", "SSL for Store");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.UseSSL.Hint", "This will enforce the use of SSL when returning back from the payment page.");

            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableCreditCard", "Enable Credit Card Payments");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableCreditCard.Hint", "Enable Credit Card Payments.");

            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableBankTransfer", "Enable Bank Transfers");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableBankTransfer.Hint", "Enable Bank Transfers.");

            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableZapper", "Enable Zapper");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableZapper.Hint", "Enable Zapper.");

            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableSnapscan", "Enable SnapScan");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableSnapscan.Hint", "Enable SnapScan.");

            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnablePaypal", "Enable PayPal");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnablePaypal.Hint", "Enable PayPal.");

            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableMobicred", "Enable Mobicred");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableMobicred.Hint", "Enable Mobicred.");

            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableMomoPay", "Enable Momopay");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableMomoPay.Hint", "Enable Momopay.");

            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableScanToPay", "Enable MasterPass");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableScanToPay.Hint", "Enable MasterPass.");

            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableSamsungPay", "Enable Samsung Pay");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableSamsungPay.Hint", "Enable Samsung Pay.");

            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableRcsPay", "Enable Rcs Pay");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableRcsPay.Hint", "Enable RCS Payments.");

            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableApplePay", "Enable Apple Pay");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableApplePay.Hint", "Enable Apple Pay.");

            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.PayGate.Instructions", @"<p>
PayGate works by sending the user to <a href=""https://www.paygate.co.za"">PayGate</a> to enter their payment information. 
</p>");


            await base.InstallAsync();
        }

        public override async Task UninstallAsync()
        {
            //settings
            await _settingService.DeleteSettingAsync<PayGatePaymentSettings>();

            //locales
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.RedirectionTip");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.TestMode");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.TestMode.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.PayGateID");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.PayGateID.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EncryptionKey");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EncryptionKey.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.DisableIpn");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.DisableIpn.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.UseSSL");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.UseSSL.Hint");

            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableCreditCard");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableCreditCard.Hint");

            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableBanktransfer");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableBanktransfer.Hint");

            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableZapper");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableZapper.Hint");

            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableSnapscan");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableSnapscan.Hint");

            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnablePaypal");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnablePaypal.Hint");

            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableMobicred");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableMobicred.Hint");

            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableMomoPay");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableMomoPay.Hint");

            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableScanToPay");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableScanToPay.Hint");

            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableSamsungPay");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableSamsungPay.Hint");

            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableApplePay");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableApplePay.Hint");

            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableRcsPay");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Fields.EnableRcsPay.Hint");

            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.PayGate.Instructions");

            await base.UninstallAsync();
        }

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task<string> GetPaymentMethodDescriptionAsync()
        {
            //return description of this payment method to be display on "payment method" checkout step. good practice is to make it localizable
            //for example, for a redirection payment method, description may be like this: "You will be redirected to PayGate site to complete the payment"
            return await _localizationService.GetResourceAsync("Plugins.Payments.PayGate.PaymentMethodDescription");
        }

        public Type GetPublicViewComponent()
        {
            return typeof(PaymentPayGateViewComponent);
        }

        #endregion

        #region Properties
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
