using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Payments;
using BTCPayServer.Plugins.Zano.Configuration;
using BTCPayServer.Plugins.Zano.Payments;
using BTCPayServer.Plugins.Zano.RPC.Models;
using BTCPayServer.Plugins.Zano.Services;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Services.Stores;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;

namespace BTCPayServer.Plugins.Zano.Controllers
{
    [Route("stores/{storeId}/zanolike")]
    [Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie)]
    [Authorize(Policy = Policies.CanModifyStoreSettings, AuthenticationSchemes = AuthenticationSchemes.Cookie)]
    [Authorize(Policy = Policies.CanModifyServerSettings, AuthenticationSchemes = AuthenticationSchemes.Cookie)]
    public class UIZanoLikeStoreController : Controller
    {
        private readonly ZanoLikeConfiguration _zanoLikeConfiguration;
        private readonly StoreRepository _StoreRepository;
        private readonly ZanoRPCProvider _ZanoRpcProvider;
        private readonly PaymentMethodHandlerDictionary _handlers;
        private IStringLocalizer StringLocalizer { get; }

        public UIZanoLikeStoreController(ZanoLikeConfiguration zanoLikeConfiguration,
            StoreRepository storeRepository, ZanoRPCProvider zanoRpcProvider,
            PaymentMethodHandlerDictionary handlers,
            IStringLocalizer stringLocalizer)
        {
            _zanoLikeConfiguration = zanoLikeConfiguration;
            _StoreRepository = storeRepository;
            _ZanoRpcProvider = zanoRpcProvider;
            _handlers = handlers;
            StringLocalizer = stringLocalizer;
        }

        public StoreData StoreData => HttpContext.GetStoreData();

        [HttpGet()]
        public async Task<IActionResult> GetStoreZanoLikePaymentMethods()
        {
            return View("/Views/Zano/GetStoreZanoLikePaymentMethods.cshtml", await GetVM(StoreData));
        }
        [NonAction]
        public async Task<ZanoLikePaymentMethodListViewModel> GetVM(StoreData storeData)
        {
            var excludeFilters = storeData.GetStoreBlob().GetExcludedPaymentMethods();


            return new ZanoLikePaymentMethodListViewModel()
            {
                Items = _zanoLikeConfiguration.ZanoLikeConfigurationItems.Select(pair =>
                    GetZanoLikePaymentMethodViewModel(storeData, pair.Key, excludeFilters))
            };
        }

        private Task<GetAccountsResponse> GetAccounts(string cryptoCode)
        {
            try
            {
                if (_ZanoRpcProvider.Summaries.TryGetValue(cryptoCode, out var summary) && summary.WalletAvailable)
                {

                    return _ZanoRpcProvider.WalletRpcClients[cryptoCode].SendCommandAsync<GetAccountsRequest, GetAccountsResponse>("getbalance", new GetAccountsRequest());
                }
            }
            catch
            {
                // ignored
            }

            return Task.FromResult<GetAccountsResponse>(null);
        }

        private ZanoLikePaymentMethodViewModel GetZanoLikePaymentMethodViewModel(
            StoreData storeData, string cryptoCode,
            IPaymentFilter excludeFilters)
        {
            var zano = storeData.GetPaymentMethodConfigs(_handlers)
                .Where(s => s.Value is ZanoPaymentPromptDetails)
                .Select(s => (PaymentMethodId: s.Key, Details: (ZanoPaymentPromptDetails)s.Value));
            var pmi = PaymentTypes.CHAIN.GetPaymentMethodId(cryptoCode);
            var settings = zano.Where(method => method.PaymentMethodId == pmi).Select(m => m.Details).SingleOrDefault();
            _ZanoRpcProvider.Summaries.TryGetValue(cryptoCode, out var summary);
            _zanoLikeConfiguration.ZanoLikeConfigurationItems.TryGetValue(cryptoCode,
                out var configurationItem);

            var settlementThresholdChoice = ZanoLikeSettlementThresholdChoice.StoreSpeedPolicy;
            if (settings != null && settings.InvoiceSettledConfirmationThreshold is { } confirmations)
            {
                settlementThresholdChoice = confirmations switch
                {
                    0 => ZanoLikeSettlementThresholdChoice.ZeroConfirmation,
                    1 => ZanoLikeSettlementThresholdChoice.AtLeastOne,
                    10 => ZanoLikeSettlementThresholdChoice.AtLeastTen,
                    _ => ZanoLikeSettlementThresholdChoice.Custom
                };
            }

            return new ZanoLikePaymentMethodViewModel()
            {
                Enabled =
                    settings != null &&
                    !excludeFilters.Match(PaymentTypes.CHAIN.GetPaymentMethodId(cryptoCode)),
                Summary = summary,
                CryptoCode = cryptoCode,

                SettlementConfirmationThresholdChoice = settlementThresholdChoice,
                CustomSettlementConfirmationThreshold =
                    settings != null &&
                    settlementThresholdChoice is ZanoLikeSettlementThresholdChoice.Custom
                        ? settings.InvoiceSettledConfirmationThreshold
                        : null
            };
        }

        [HttpGet("{cryptoCode}")]
        public async Task<IActionResult> GetStoreZanoLikePaymentMethod(string cryptoCode)
        {
            cryptoCode = cryptoCode.ToUpperInvariant();
            if (!_zanoLikeConfiguration.ZanoLikeConfigurationItems.ContainsKey(cryptoCode))
            {
                return NotFound();
            }

            var vm = GetZanoLikePaymentMethodViewModel(StoreData, cryptoCode,
                StoreData.GetStoreBlob().GetExcludedPaymentMethods());
            return View("/Views/Zano/GetStoreZanoLikePaymentMethod.cshtml", vm);
        }

        [HttpPost("{cryptoCode}")]
        [DisableRequestSizeLimit]
        public async Task<IActionResult> GetStoreZanoLikePaymentMethod(ZanoLikePaymentMethodViewModel viewModel, string command, string cryptoCode)
        {
            cryptoCode = cryptoCode.ToUpperInvariant();
            if (!_zanoLikeConfiguration.ZanoLikeConfigurationItems.TryGetValue(cryptoCode,
                out var configurationItem))
            {
                return NotFound();
            }

            if (command == "add-account")
            {
                try
                {
                    var newAccount = await _ZanoRpcProvider.WalletRpcClients[cryptoCode].SendCommandAsync<CreateAccountRequest, CreateAccountResponse>("create_account", new CreateAccountRequest()
                    {
                        Label = viewModel.NewAccountLabel
                    });
                    viewModel.AccountIndex = newAccount.AccountIndex;
                }
                catch (Exception)
                {
                    ModelState.AddModelError(nameof(viewModel.AccountIndex), StringLocalizer["Could not create a new account."]);
                }

            }
            else if (command == "set-wallet-details")
            {
                var valid = true;
                if (viewModel.PrimaryAddress == null)
                {
                    ModelState.AddModelError(nameof(viewModel.PrimaryAddress), StringLocalizer["Please set your primary public address"]);
                    valid = false;
                }
                if (viewModel.PrivateViewKey == null)
                {
                    ModelState.AddModelError(nameof(viewModel.PrivateViewKey), StringLocalizer["Please set your private view key"]);
                    valid = false;
                }
                if (configurationItem.WalletDirectory == null)
                {
                    ModelState.AddModelError(nameof(viewModel.PrimaryAddress), StringLocalizer["This installation doesn't support wallet creation (BTCPAY_XMR_WALLET_DAEMON_WALLETDIR is not set)"]);
                    valid = false;
                }
                if (valid)
                {
                    if (_ZanoRpcProvider.Summaries.TryGetValue(cryptoCode, out var summary))
                    {
                        if (summary.WalletAvailable)
                        {
                            TempData.SetStatusMessageModel(new StatusMessageModel
                            {
                                Severity = StatusMessageModel.StatusSeverity.Error,
                                Message = StringLocalizer["There is already an active wallet configured for {0}. Replacing it would break any existing invoices!", cryptoCode].Value
                            });
                            return RedirectToAction(nameof(GetStoreZanoLikePaymentMethod),
                                new { cryptoCode });
                        }
                    }
                    try
                    {
                        var response = await _ZanoRpcProvider.WalletRpcClients[cryptoCode].SendCommandAsync<GenerateFromKeysRequest, GenerateFromKeysResponse>("generate_from_keys", new GenerateFromKeysRequest
                        {
                            PrimaryAddress = viewModel.PrimaryAddress,
                            PrivateViewKey = viewModel.PrivateViewKey,
                            WalletFileName = "view_wallet",
                            RestoreHeight = viewModel.RestoreHeight,
                            Password = viewModel.WalletPassword
                        });
                        if (response?.Error != null)
                        {
                            throw new GenerateFromKeysException(response.Error.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError(nameof(viewModel.AccountIndex), StringLocalizer["Could not generate view wallet from keys: {0}", ex.Message]);
                        return View("/Views/Zano/GetStoreZanoLikePaymentMethod.cshtml", viewModel);
                    }

                    TempData.SetStatusMessageModel(new StatusMessageModel
                    {
                        Severity = StatusMessageModel.StatusSeverity.Info,
                        Message = StringLocalizer["View-only wallet created. The wallet will soon become available."].Value
                    });
                    return RedirectToAction(nameof(GetStoreZanoLikePaymentMethod), new { cryptoCode });
                }
            }
            //else if (command == "upload-wallet")
            //{
            //    var valid = true;
            //    if (viewModel.WalletFile == null)
            //    {
            //        ModelState.AddModelError(nameof(viewModel.WalletFile), StringLocalizer["Please select the view-only wallet file"]);
            //        valid = false;
            //    }
            //    if (viewModel.WalletKeysFile == null)
            //    {
            //        ModelState.AddModelError(nameof(viewModel.WalletKeysFile), StringLocalizer["Please select the view-only wallet keys file"]);
            //        valid = false;
            //    }
            //    if (configurationItem.WalletDirectory == null)
            //    {
            //        ModelState.AddModelError(nameof(viewModel.WalletFile), StringLocalizer["This installation doesn't support wallet import (BTCPAY_XMR_WALLET_DAEMON_WALLETDIR is not set)"]);
            //        valid = false;
            //    }
            //    if (valid)
            //    {
            //        if (_ZanoRpcProvider.Summaries.TryGetValue(cryptoCode, out var summary))
            //        {
            //            if (summary.WalletAvailable)
            //            {
            //                TempData.SetStatusMessageModel(new StatusMessageModel
            //                {
            //                    Severity = StatusMessageModel.StatusSeverity.Error,
            //                    Message = StringLocalizer["There is already an active wallet configured for {0}. Replacing it would break any existing invoices!", cryptoCode].Value
            //                });
            //                return RedirectToAction(nameof(GetStoreZanoLikePaymentMethod),
            //                    new { cryptoCode });
            //            }
            //        }

            //        var fileAddress = Path.Combine(configurationItem.WalletDirectory, "wallet");
            //        using (var fileStream = new FileStream(fileAddress, FileMode.Create))
            //        {
            //            await viewModel.WalletFile.CopyToAsync(fileStream);
            //            try
            //            {
            //                Exec($"chmod 666 {fileAddress}");
            //            }
            //            catch
            //            {
            //                // ignored
            //            }
            //        }

            //        fileAddress = Path.Combine(configurationItem.WalletDirectory, "wallet.keys");
            //        using (var fileStream = new FileStream(fileAddress, FileMode.Create))
            //        {
            //            await viewModel.WalletKeysFile.CopyToAsync(fileStream);
            //            try
            //            {
            //                Exec($"chmod 666 {fileAddress}");
            //            }
            //            catch
            //            {
            //                // ignored
            //            }
            //        }

            //        fileAddress = Path.Combine(configurationItem.WalletDirectory, "password");
            //        using (var fileStream = new StreamWriter(fileAddress, false))
            //        {
            //            await fileStream.WriteAsync(viewModel.WalletPassword);
            //            try
            //            {
            //                Exec($"chmod 666 {fileAddress}");
            //            }
            //            catch
            //            {
            //                // ignored
            //            }
            //        }

            //        try
            //        {
            //            //var response = await _ZanoRpcProvider.WalletRpcClients[cryptoCode].SendCommandAsync<OpenWalletRequest, OpenWalletResponse>("open_wallet", new OpenWalletRequest
            //            //{
            //            //    Filename = "wallet",
            //            //    Password = viewModel.WalletPassword
            //            //});
            //            //if (response?.Error != null)
            //            //{
            //            //    throw new WalletOpenException(response.Error.Message);
            //            //}
            //        }
            //        catch (Exception ex)
            //        {
            //            ModelState.AddModelError(nameof(viewModel.AccountIndex), StringLocalizer["Could not open the wallet: {0}", ex.Message]);
            //            return View("/Views/Zano/GetStoreZanoLikePaymentMethod.cshtml", viewModel);
            //        }

            //        TempData.SetStatusMessageModel(new StatusMessageModel
            //        {
            //            Severity = StatusMessageModel.StatusSeverity.Info,
            //            Message = StringLocalizer["View-only wallet files uploaded. The wallet will soon become available."].Value
            //        });
            //        return RedirectToAction(nameof(GetStoreZanoLikePaymentMethod), new { cryptoCode });
            //    }
            //}

            if (!ModelState.IsValid)
            {

                var vm = GetZanoLikePaymentMethodViewModel(StoreData, cryptoCode,
                    StoreData.GetStoreBlob().GetExcludedPaymentMethods());

                vm.Enabled = viewModel.Enabled;
                vm.SettlementConfirmationThresholdChoice = viewModel.SettlementConfirmationThresholdChoice;
                vm.CustomSettlementConfirmationThreshold = viewModel.CustomSettlementConfirmationThreshold;
                //vm.SupportWalletExport = configurationItem.WalletDirectory is not null;
                return View("/Views/Zano/GetStoreZanoLikePaymentMethod.cshtml", vm);
            }

            var storeData = StoreData;
            var blob = storeData.GetStoreBlob();
            storeData.SetPaymentMethodConfig(_handlers[PaymentTypes.CHAIN.GetPaymentMethodId(cryptoCode)], new ZanoPaymentPromptDetails()
            {
                AccountAddress = viewModel.AccountIndex,
                InvoiceSettledConfirmationThreshold = viewModel.SettlementConfirmationThresholdChoice switch
                {
                    ZanoLikeSettlementThresholdChoice.ZeroConfirmation => 0,
                    ZanoLikeSettlementThresholdChoice.AtLeastOne => 1,
                    ZanoLikeSettlementThresholdChoice.AtLeastTen => 10,
                    ZanoLikeSettlementThresholdChoice.Custom when viewModel.CustomSettlementConfirmationThreshold is { } custom => custom,
                    _ => null
                }
            });

            blob.SetExcluded(PaymentTypes.CHAIN.GetPaymentMethodId(viewModel.CryptoCode), !viewModel.Enabled);
            storeData.SetStoreBlob(blob);
            await _StoreRepository.UpdateStore(storeData);
            return RedirectToAction("GetStoreZanoLikePaymentMethods",
                new { StatusMessage = $"{cryptoCode} settings updated successfully", storeId = StoreData.Id });
        }

        private void Exec(string cmd)
        {

            var escapedArgs = cmd.Replace("\"", "\\\"", StringComparison.InvariantCulture);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = "/bin/sh",
                    Arguments = $"-c \"{escapedArgs}\""
                }
            };

#pragma warning disable CA1416 // Validate platform compatibility
            process.Start();
#pragma warning restore CA1416 // Validate platform compatibility
            process.WaitForExit();
        }

        public class ZanoLikePaymentMethodListViewModel
        {
            public IEnumerable<ZanoLikePaymentMethodViewModel> Items { get; set; }
        }

        public class ZanoLikePaymentMethodViewModel : IValidatableObject
        {
            public ZanoRPCProvider.ZanoLikeSummary Summary { get; set; }
            public string CryptoCode { get; set; }
            public string NewAccountLabel { get; set; }
            public long AccountIndex { get; set; }
            public bool Enabled { get; set; }

            public IEnumerable<SelectListItem> Accounts { get; set; }
            public bool WalletFileFound { get; set; }
            [Display(Name = "Primary Public Address")]
            public string PrimaryAddress { get; set; }
            [Display(Name = "Private View Key")]
            public string PrivateViewKey { get; set; }
            [Display(Name = "Restore Height")]
            public int RestoreHeight { get; set; }
            [Display(Name = "Wallet Password")]
            public string WalletPassword { get; set; }
            [Display(Name = "Consider the invoice settled when the payment transaction …")]
            public ZanoLikeSettlementThresholdChoice SettlementConfirmationThresholdChoice { get; set; }
            [Display(Name = "Required Confirmations"), Range(0, 100)]
            public long? CustomSettlementConfirmationThreshold { get; set; }

            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                if (SettlementConfirmationThresholdChoice is ZanoLikeSettlementThresholdChoice.Custom
                    && CustomSettlementConfirmationThreshold is null)
                {
                    yield return new ValidationResult(
                        "You must specify the number of required confirmations when using a custom threshold.",
                        new[] { nameof(CustomSettlementConfirmationThreshold) });
                }
            }
        }

        public enum ZanoLikeSettlementThresholdChoice
        {
            [Display(Name = "Store Speed Policy", Description = "Use the store's speed policy")]
            StoreSpeedPolicy,
            [Display(Name = "Zero Confirmation", Description = "Is unconfirmed")]
            ZeroConfirmation,
            [Display(Name = "At Least One", Description = "Has at least 1 confirmation")]
            AtLeastOne,
            [Display(Name = "At Least Ten", Description = "Has at least 10 confirmations")]
            AtLeastTen,
            [Display(Name = "Custom", Description = "Custom")]
            Custom
        }
    }
}