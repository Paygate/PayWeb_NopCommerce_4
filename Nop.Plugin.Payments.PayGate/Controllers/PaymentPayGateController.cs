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
using Nop.Services;
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

        public IActionResult PayGateNotifyHandler(IFormCollection form)
        {
            _logger.Error("PayGateNotifyHandler start");
            var id = Int32.Parse(Request.Query["pgnopcommerce"]);
            _logger.Error(id.ToString());
            return RedirectToRoute("OrderDetails", new { orderId = id });
        }

        public IActionResult PayGateReturnHandler(IFormCollection form)
        {
            string[] keys = Request.Form.Keys.ToArray();
            
            String transaction_status = "";
            String pay_request_id = "";
            String transaction_status_desc = "";
            Order order = _orderService.GetOrderById(Int32.Parse(Request.Query["pgnopcommerce"]));
            var sBuilder = new StringBuilder();
            var query_status = PaymentStatus.Pending;
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i] == "TRANSACTION_STATUS")
                {
                    transaction_status = Request.Form[keys[i]];
                }

                if (keys[i] == "PAY_REQUEST_ID")
                {
                    pay_request_id = Request.Form[keys[i]];
                }
            }

            using (var client = new System.Net.WebClient())
            {
                var queryData = new NameValueCollection();
                queryData["PAYGATE_ID"] = _payGatePaymentSettings.PayGateID;
                queryData["PAY_REQUEST_ID"] = pay_request_id;
                queryData["REFERENCE"] = Request.Query["pgnopcommerce"];
                string queryValues = string.Join("", queryData.AllKeys.Select(key => queryData[key]));
                queryData["CHECKSUM"] = new PayGateHelper().CalculateMD5Hash(queryValues + _payGatePaymentSettings.EncryptionKey);
                var response = client.UploadValues("https://secure.paygate.co.za/payweb3/query.trans", queryData);

                var responseString = Encoding.Default.GetString(response);
                if (responseString != null)
                {
                    Dictionary<string, string> dict =
                    responseString.Split('&')
                    .Select(x => x.Split('='))
                    .ToDictionary(y => y[0], y => y[1]);

                    try
                    {
                        String trans_id = dict["TRANSACTION_STATUS"].ToString();
                        String query_status_desc = "";
                        switch (trans_id)
                        {
                            case "1":
                                query_status = PaymentStatus.Paid;
                                query_status_desc = "Approved";
                                break;

                            case "2":
                                query_status_desc = "Declined";
                                break;

                            case "4":
                                query_status_desc = "Cancelled By Customer with back button on payment page";
                                break;

                            case "0":
                                query_status_desc = "Not Done";
                                break;
                            default:
                                break;
                        }

                        sBuilder.AppendLine("PayGate Query Data");
                        sBuilder.AppendLine("=======================");
                        sBuilder.AppendLine("PayGate Transaction_Id: " + dict["TRANSACTION_ID"]);
                        sBuilder.AppendLine("PayGate Status Desc: " + query_status_desc);
                        sBuilder.AppendLine("");

                    }
                    catch (Exception e)
                    {
                        sBuilder.AppendLine("PayGate Query Data");
                        sBuilder.AppendLine("=======================");
                        sBuilder.AppendLine("PayGate Query Response: " + responseString);
                        sBuilder.AppendLine("");
                    }

                }

            }

            var new_payment_status = PaymentStatus.Pending;
            switch (transaction_status)
            {
                case "1":
                    new_payment_status = PaymentStatus.Paid;
                    transaction_status_desc = "Approved";
                    break;

                case "2":
                    transaction_status_desc = "Declined";
                    break;

                case "4":
                    transaction_status_desc = "Cancelled By Customer with back button on payment page";
                    break;

                case "0":
                    transaction_status_desc = "Not Done";
                    break;
                default:
                    break;
            }

            sBuilder.AppendLine("PayGate Return Data");
            sBuilder.AppendLine("=======================");
            sBuilder.AppendLine("PayGate PayRequestId: " + pay_request_id);
            sBuilder.AppendLine("PayGate Status Desc: " + transaction_status_desc);

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
                    order.AuthorizationTransactionId = pay_request_id;
                    _orderService.UpdateOrder(order);

                    _orderProcessingService.MarkOrderAsPaid(order);
                }
                return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
            }
            else if (new_payment_status == PaymentStatus.Paid)
            {
                if (_orderProcessingService.CanMarkOrderAsPaid(order))
                {
                    order.AuthorizationTransactionId = pay_request_id;
                    _orderService.UpdateOrder(order);

                    _orderProcessingService.MarkOrderAsPaid(order);
                }
                return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
            }
            else
            {

                order.AuthorizationTransactionId = pay_request_id;
                OrderNote _note = new OrderNote();
                _note.CreatedOnUtc = DateTime.Now;
                _note.DisplayToCustomer = true;
                _note.Note = "Payment failed with the following description: " + transaction_status_desc;
                _orderProcessingService.CancelOrder(order, false);
                order.OrderNotes.Add(_note);
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
            model.UseSandbox = payGatePaymentSettings.UseSandbox;
            model.PayGateID = payGatePaymentSettings.PayGateID;
            model.EncryptionKey = payGatePaymentSettings.EncryptionKey;
            model.EnableIpn = payGatePaymentSettings.EnableIpn;
            model.ActiveStoreScopeConfiguration = storeScope;

            if (storeScope > 0)
            {
                model.UseSandbox_OverrideForStore = _settingService.SettingExists(payGatePaymentSettings, x => x.UseSandbox, storeScope);
                model.PayGateID_OverrideForStore = _settingService.SettingExists(payGatePaymentSettings, x => x.PayGateID, storeScope);
                model.EncryptionKey_OverrideForStore = _settingService.SettingExists(payGatePaymentSettings, x => x.EncryptionKey, storeScope);
                model.EnableIpn_OverrideForStore = _settingService.SettingExists(payGatePaymentSettings, x => x.EnableIpn, storeScope);

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
            payGatePaymentSettings.UseSandbox = model.UseSandbox;
            payGatePaymentSettings.PayGateID = model.PayGateID;
            payGatePaymentSettings.EncryptionKey = model.EncryptionKey;
            payGatePaymentSettings.EnableIpn = model.EnableIpn;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */

            if (model.UseSandbox_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(payGatePaymentSettings, x => x.UseSandbox, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(payGatePaymentSettings, x => x.UseSandbox, storeScope);

            if (model.PayGateID_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(payGatePaymentSettings, x => x.PayGateID, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(payGatePaymentSettings, x => x.PayGateID, storeScope);

            if (model.EncryptionKey_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(payGatePaymentSettings, x => x.EncryptionKey, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(payGatePaymentSettings, x => x.EncryptionKey, storeScope);

            if (model.EnableIpn_OverrideForStore || storeScope == 0)
                _settingService.SaveSetting(payGatePaymentSettings, x => x.EnableIpn, storeScope, false);
            else if (storeScope > 0)
                _settingService.DeleteSetting(payGatePaymentSettings, x => x.EnableIpn, storeScope);


            //now clear settings cache
            _settingService.ClearCache();

            SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        
        #endregion
    }
}