using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.PayGate.Models;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;


namespace Nop.Plugin.Payments.PayGate.Controllers
{
    public class PaymentPayGateController : BasePaymentController
    {
        #region Fields

        private readonly IWorkContext _workContext;
        private readonly IStoreService _storeService;
        private readonly IStoreContext storeContext;
        private readonly ISettingService _settingService;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly ILocalizationService _localizationService;
        private readonly PayGatePaymentSettings _payGatePaymentSettings;
        private readonly IPermissionService _permissionService;
        private ILogger _logger;

        #endregion

        #region Ctor

        public PaymentPayGateController(IWorkContext workContext,
            IStoreService storeService,
            ISettingService settingService,
            IOrderService orderService,
            IOrderProcessingService orderProcessingService,
            ILocalizationService localizationService,
            IPermissionService permissionService,
            ILogger logger,
            PayGatePaymentSettings payGatePaymentSettings)
        {
            this._workContext = workContext;
            this._storeService = storeService;
            this._settingService = settingService;
            this._orderService = orderService;
            this._orderProcessingService = orderProcessingService;
            this._localizationService = localizationService;
            this._permissionService = permissionService;
            this._payGatePaymentSettings = payGatePaymentSettings;
            this._logger = logger;
        }

        #endregion

        #region Methods

        //public IActionResult PayGateNotifyHandler(IFormCollection form)
        public void PayGateNotifyHandler(IFormCollection form)
        {
            Response.WriteAsync("OK");

            var reference = Request.Query["pgnopcommerce"];
            bool isPaid;

            _logger.Information("PayGateNotifyHandler start. Order no.: " + reference);

            // var order = _orderService.GetOrderById(Int32.Parse(Request.Query["pgnopcommerce"]));
            Order order = _orderService.GetOrderById(Int32.Parse(Request.Form["USER1"]));
            _logger.Information("PayGateNotifyHandler: Order Payment Status: " + order.PaymentStatus);

            isPaid = order.PaymentStatus == PaymentStatus.Paid ? true : false;

            if (!isPaid)
            {
                var sBuilder = new StringBuilder();
                var query_status = PaymentStatus.Pending;
                var payrequestId = "";
                var transactionStatus = "";

                var testMode = _payGatePaymentSettings.TestMode;
                var paygateId = "10011072130";
                var encryptionKey = "secret";
                if (!testMode)
                {
                    paygateId = _payGatePaymentSettings.PayGateID;
                    encryptionKey = _payGatePaymentSettings.EncryptionKey;
                }

                //Validate checksum for the posted form fields
                var formData = new NameValueCollection();
                var formDataJS = "";
                string[] keys = Request.Form.Keys.ToArray();
                for (int i = 0; i < keys.Length; i++)
                {
                    formData[keys[i]] = Request.Form[keys[i]];
                    formDataJS += keys[i] + "=" + formData[keys[i]] + "&";
                }

                _logger.Information("PayGateNotifyHandler: POST: " + formDataJS);

                var checksum = formData["CHECKSUM"];
                var checkstring = "";
                for (var i = 0; i < formData.Count - 1; i++)
                {
                    checkstring += formData[i];
                }
                checkstring += encryptionKey;

                var ourChecksum = new PayGateHelper().CalculateMD5Hash(checkstring);
                if (ourChecksum.Equals(checksum, StringComparison.OrdinalIgnoreCase))
                {
                    var trans_status = formData["TRANSACTION_STATUS"];
                    transactionStatus = trans_status;
                    var query_status_desc = "";
                    payrequestId = formData["PAY_REQUEST_ID"];
                    switch (trans_status)
                    {
                        case "1":
                            query_status = PaymentStatus.Paid;
                            query_status_desc = "Approved";
                            break;

                        case "2":
                            query_status = PaymentStatus.Voided;
                            query_status_desc = "Declined";
                            break;

                        case "4":
                            query_status = PaymentStatus.Voided;
                            query_status_desc = "Cancelled By Customer with back button on payment page";
                            break;

                        case "0":
                            query_status = PaymentStatus.Voided;
                            query_status_desc = "Not Done";
                            break;
                        default:
                            break;
                    }

                    sBuilder.AppendLine("PayGate Notify Handler");
                    sBuilder.AppendLine("PayGate Query Data");
                    sBuilder.AppendLine("=======================");
                    sBuilder.AppendLine("PayGate Transaction_Id: " + formData["TRANSACTION_ID"]);
                    sBuilder.AppendLine("PayGate Status Desc: " + query_status_desc);
                    sBuilder.AppendLine("");

                    //order note
                    order.OrderNotes.Add(new OrderNote
                    {
                        Note = sBuilder.ToString(),//sbbustring.Format("Order status has been changed to {0}", PaymentStatus.Paid),
                        DisplayToCustomer = false,
                        CreatedOnUtc = DateTime.UtcNow
                    });

                    _orderService.UpdateOrder(order);

                    //load settings for a chosen store scope
                    var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
                    var payGatePaymentSettings = _settingService.LoadSetting<PayGatePaymentSettings>(storeScope);

                    //mark order as paid
                    if (query_status == PaymentStatus.Paid)
                    {
                        if (_orderProcessingService.CanMarkOrderAsPaid(order))
                        {
                            order.AuthorizationTransactionId = payrequestId;
                            _orderService.UpdateOrder(order);

                            _orderProcessingService.MarkOrderAsPaid(order);
                        }
                        RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
                    }
                    else
                    {
                        order.AuthorizationTransactionId = payrequestId;
                        OrderNote note = new OrderNote();
                        note.CreatedOnUtc = DateTime.Now;
                        note.DisplayToCustomer = true;
                        note.Note = "Payment failed with the following description: " + transactionStatus;
                        if (_orderProcessingService.CanCancelOrder(order))
                        {
                            _orderProcessingService.CancelOrder(order, false);
                        }
                        order.OrderNotes.Add(note);
                        _orderService.UpdateOrder(order);

                        RedirectToRoute("OrderDetails", new { orderId = order.Id.ToString().Trim() });
                    }
                }
                else
                {
                    _logger.Error("PayGateNotifyHandler: Checksum mismatch: " + ourChecksum + " : " + checksum);
                }
            }
        }

        public IActionResult PayGateReturnHandler(IFormCollection form)
        {
            var reference = Request.Query["pgnopcommerce"];
            var isPaid = false;
            var isqueried = false;
            _logger.Information("PayGateReturnHandler start. Order no.: " + reference);
            string[] keys = Request.Form.Keys.ToArray();
            var verified = false;
            var testMode = _payGatePaymentSettings.TestMode;
            var paygateId = "10011072130";
            var encryptionKey = "secret";
            if (!testMode)
            {
                paygateId = _payGatePaymentSettings.PayGateID;
                encryptionKey = _payGatePaymentSettings.EncryptionKey;
            }

            //Process form data into name value collection
            var formData = new NameValueCollection();
            var formDataJS = "";
            for (int i = 0; i < keys.Length; i++)
            {
                formData[keys[i]] = Request.Form[keys[i]];
                formDataJS += keys[i] + "=" + formData[keys[i]] + "&";
            }

            //var formDataJS = JsonConvert.SerializeObject(formData);

            _logger.Information("PayGateReturnHandler: POST: " + formDataJS);

            //First check that returned checksum is correct before proceeding any further
            var transactionStatus = formData["TRANSACTION_STATUS"];
            var payrequestId = formData["PAY_REQUEST_ID"];
            var checksum = formData["CHECKSUM"];

            var checkstring = paygateId + payrequestId + transactionStatus + reference + encryptionKey;
            var ourChecksum = new PayGateHelper().CalculateMD5Hash(checkstring);
            if (ourChecksum.Equals(checksum, StringComparison.OrdinalIgnoreCase))
            {
                verified = true;
            }
            _logger.Information("PayGateReturnHandler: Order " + reference + " Checksum Status: " + (verified ? "matched" : "not matched"));


            var order = _orderService.GetOrderById(Int32.Parse(Request.Query["pgnopcommerce"]));
            isPaid = order.PaymentStatus == PaymentStatus.Paid ? true : false;
            _logger.Information("PayGateReturnHandler: Order Payment Status: " + order.PaymentStatus);

            if (order.PaymentStatus == PaymentStatus.Paid)
            {
                _logger.Information("PayGateReturnHandler: Order no. " + reference + " is already paid");
                return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
            }

            if (verified)
            {
                var sBuilder = new StringBuilder();
                var query_status = PaymentStatus.Pending;

                using (var client = new System.Net.WebClient())
                {
                    var queryData = new NameValueCollection();
                    queryData["PAYGATE_ID"] = paygateId;
                    queryData["PAY_REQUEST_ID"] = payrequestId;
                    queryData["REFERENCE"] = reference;
                    string queryValues = string.Join("", queryData.AllKeys.Select(key => queryData[key]));
                    queryData["CHECKSUM"] = new PayGateHelper().CalculateMD5Hash(queryValues + _payGatePaymentSettings.EncryptionKey);
                    var cnt = 0;
                    Dictionary<string, string> dict = null;
                    var responseString = "";
                    if (!isqueried && cnt < 5)
                    {
                        var response = client.UploadValues("https://secure.paygate.co.za/payweb3/query.trans", queryData);
                        responseString = Encoding.Default.GetString(response);

                        dict =
                        responseString.Split('&')
                        .Select(x => x.Split('='))
                        .ToDictionary(y => y[0], y => y[1]);

                        if (dict.Count > 0 && dict.ContainsKey("TRANSACTION_STATUS"))
                        {
                            isqueried = true;
                        }
                        cnt++;
                    }

                    var dictJS = "";
                    foreach(var item in dict)
                    {
                        dictJS += item.Key + "=" + item.Value + "&";
                    }

                    _logger.Information("PayGateReturnHandler: QUERY: " + dictJS);

                    if (isqueried)
                    {
                        try
                        {
                            String trans_status = dict["TRANSACTION_STATUS"].ToString();
                            String query_status_desc = "";
                            switch (trans_status)
                            {
                                case "1":
                                    query_status = PaymentStatus.Paid;
                                    query_status_desc = "Approved";
                                    break;

                                case "2":
                                    query_status = PaymentStatus.Voided;
                                    query_status_desc = "Declined";
                                    break;

                                case "4":
                                    query_status = PaymentStatus.Voided;
                                    query_status_desc = "Cancelled By Customer with back button on payment page";
                                    break;

                                case "0":
                                    query_status = PaymentStatus.Voided;
                                    query_status_desc = "Not Done";
                                    break;
                                default:
                                    break;
                            }

                            sBuilder.AppendLine("PayGate Return Handler");
                            sBuilder.AppendLine("PayGate Query Data");
                            sBuilder.AppendLine("=======================");
                            sBuilder.AppendLine("PayGate Transaction_Id: " + dict["TRANSACTION_ID"]);
                            sBuilder.AppendLine("PayGate Status Desc: " + query_status_desc);
                            sBuilder.AppendLine("");

                        }
                        catch (Exception e)
                        {
                            sBuilder.AppendLine("PayGate Return Handler");
                            sBuilder.AppendLine("PayGate Query Data");
                            sBuilder.AppendLine("=======================");
                            sBuilder.AppendLine("PayGate Query Response: " + responseString);
                            sBuilder.AppendLine("");
                        }

                    }
                    else
                    {
                        sBuilder.AppendLine("PayGate Return Handler");
                        sBuilder.AppendLine("PayGate Return Data");
                        sBuilder.AppendLine("=======================");
                        sBuilder.AppendLine("PayGate PayRequestId: " + payrequestId);
                        sBuilder.AppendLine("PayGate Status Desc: " + transactionStatus);
                        sBuilder.AppendLine("PayGate Query Desc: Failed to get a response from the query");
                    }
                }

                //order note
                order.OrderNotes.Add(new OrderNote
                {
                    Note = sBuilder.ToString(),//sbbustring.Format("Order status has been changed to {0}", PaymentStatus.Paid),
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });

                _orderService.UpdateOrder(order);

                //load settings for a chosen store scope
                var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
                var payGatePaymentSettings = _settingService.LoadSetting<PayGatePaymentSettings>(storeScope);

                //mark order as paid
                if (query_status == PaymentStatus.Paid)
                {
                    if (_orderProcessingService.CanMarkOrderAsPaid(order))
                    {
                        order.AuthorizationTransactionId = payrequestId;
                        _orderService.UpdateOrder(order);

                        _orderProcessingService.MarkOrderAsPaid(order);
                    }
                    _logger.Information("PayGateReturnHandler: Order marked paid");
                    return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
                }
                else
                {
                    order.AuthorizationTransactionId = payrequestId;
                    OrderNote note = new OrderNote();
                    note.CreatedOnUtc = DateTime.Now;
                    note.DisplayToCustomer = true;
                    note.Note = "Payment failed with the following description: " + transactionStatus;
                    _logger.Error("PayGateReturnHandler: Payment failed with the following description: " + transactionStatus);
                    if (_orderProcessingService.CanCancelOrder(order))
                    {
                        _orderProcessingService.CancelOrder(order, false);
                    }
                    order.OrderNotes.Add(note);
                    _orderService.UpdateOrder(order);

                    return RedirectToRoute("OrderDetails", new { orderId = order.Id.ToString().Trim() });
                }
            }
            else
            {
                _logger.Error("PayGateReturnHandler: Checksum mismatch: " + ourChecksum + " : " + checksum);
                OrderNote note = new OrderNote();
                note.CreatedOnUtc = DateTime.Now;
                note.DisplayToCustomer = true;
                note.Note = "Payment failed: The checksum could not be verified";
                if (_orderProcessingService.CanCancelOrder(order))
                {
                    _orderProcessingService.CancelOrder(order, false);
                }
                order.OrderNotes.Add(note);
                _orderService.UpdateOrder(order);

                return RedirectToRoute("OrderDetails", new { orderId = order.Id.ToString().Trim() });
            }
        }

        private int GetActiveStoreScopeConfiguration(IStoreService storeService, IWorkContext workContext)
        {
            var storeId = Nop.Core.Infrastructure.EngineContext.Current.Resolve<Nop.Core.IStoreContext>().CurrentStore.Id;
            return storeId;
        }

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var payGatePaymentSettings = _settingService.LoadSetting<PayGatePaymentSettings>(storeScope);

            var model = new ConfigurationModel();
            model.TestMode = payGatePaymentSettings.TestMode;
            model.PayGateID = payGatePaymentSettings.PayGateID;
            model.EncryptionKey = payGatePaymentSettings.EncryptionKey;
            model.EnableIpn = payGatePaymentSettings.EnableIpn;
            model.EnableRedirect = payGatePaymentSettings.EnableRedirect;
            model.ActiveStoreScopeConfiguration = storeScope;
            model.UseSSL = payGatePaymentSettings.UseSSL;

            if (storeScope > 0)
            {
                model.TestMode_OverrideForStore = _settingService.SettingExists(payGatePaymentSettings, x => x.TestMode, storeScope);
                model.PayGateID_OverrideForStore = _settingService.SettingExists(payGatePaymentSettings, x => x.PayGateID, storeScope);
                model.EncryptionKey_OverrideForStore = _settingService.SettingExists(payGatePaymentSettings, x => x.EncryptionKey, storeScope);
                model.EnableIpn_OverrideForStore = _settingService.SettingExists(payGatePaymentSettings, x => x.EnableIpn, storeScope);
                model.EnableRedirect_OverrideForStore = _settingService.SettingExists(payGatePaymentSettings, x => x.EnableRedirect, storeScope);
                model.UseSSL_OverrideForStore = _settingService.SettingExists(payGatePaymentSettings, x => x.UseSSL, storeScope);
            }

            return View("~/Plugins/Payments.PayGate/Views/Configure.cshtml", model);
        }

        [HttpPost, ActionName("Configure")]
        [AuthorizeAdmin]
        [AdminAntiForgery]
        [Area(AreaNames.Admin)]
        public IActionResult Configure(ConfigurationModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = this.GetActiveStoreScopeConfiguration(_storeService, _workContext);
            var payGatePaymentSettings = _settingService.LoadSetting<PayGatePaymentSettings>(storeScope);

            //save settings
            payGatePaymentSettings.TestMode = model.TestMode;
            payGatePaymentSettings.PayGateID = model.PayGateID;
            payGatePaymentSettings.EncryptionKey = model.EncryptionKey;
            payGatePaymentSettings.EnableIpn = model.EnableIpn;
            payGatePaymentSettings.EnableRedirect = model.EnableRedirect;
            payGatePaymentSettings.UseSSL = model.UseSSL;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */

            _settingService.ClearCache();

            if (model.TestMode_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(payGatePaymentSettings, x => x.TestMode, storeScope, false);
            else if (storeScope > 0)
                _settingService.SaveSetting(payGatePaymentSettings, x => x.TestMode, storeScope);

            if (model.PayGateID_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(payGatePaymentSettings, x => x.PayGateID, storeScope, false);
            else if (storeScope > 0)
                _settingService.SaveSetting(payGatePaymentSettings, x => x.PayGateID, storeScope);

            if (model.EncryptionKey_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(payGatePaymentSettings, x => x.EncryptionKey, storeScope, false);
            else if (storeScope > 0)
                _settingService.SaveSetting(payGatePaymentSettings, x => x.EncryptionKey, storeScope);

            if (model.EnableIpn_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(payGatePaymentSettings, x => x.EnableIpn, storeScope, false);
            else if (storeScope > 0)
                _settingService.SaveSetting(payGatePaymentSettings, x => x.EnableIpn, storeScope);

            if (model.EnableRedirect_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(payGatePaymentSettings, x => x.EnableRedirect, storeScope, false);
            else if (storeScope > 0)
                _settingService.SaveSetting(payGatePaymentSettings, x => x.EnableRedirect, storeScope);

            if (model.UseSSL_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(payGatePaymentSettings, x => x.UseSSL, storeScope, false);
            else if (storeScope > 0)
                _settingService.SaveSetting(payGatePaymentSettings, x => x.UseSSL, storeScope);

            //now clear settings cache
            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }


        #endregion
    }
}