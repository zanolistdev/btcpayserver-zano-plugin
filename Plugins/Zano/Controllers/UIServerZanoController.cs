using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Client;
using BTCPayServer.Plugins.Zano.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Zano.Controllers
{
    [Route("stores/{storeId}/server")]
    [Authorize(AuthenticationSchemes = AuthenticationSchemes.Cookie)]
    [Authorize(Policy = Policies.CanModifyServerSettings, AuthenticationSchemes = AuthenticationSchemes.Cookie)]
    public class UIServerZanoController : Controller
    {
        private readonly ISettingsRepository _settingsRepository;
        private readonly ILogger<UIServerZanoController> _logger;
        private readonly ZanoRPCProvider _rpcProvider;

        public UIServerZanoController(ISettingsRepository settingsRepository,
            ILogger<UIServerZanoController> logger,
            ZanoRPCProvider rpcProvider)
        {
            _settingsRepository = settingsRepository;
            _logger = logger;
            _rpcProvider = rpcProvider;
        }

        [HttpGet("settings")]
        public async Task<IActionResult> Settings()
        {
            var vm = await _settingsRepository.GetSettingAsync<ZanoServerSettings>("ZanoServerSettings")
                             ?? new ZanoServerSettings();
            return View("/Views/Zano/ServerZanoSettings.cshtml", vm);
        }

        [HttpPost("settings")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(ZanoServerSettings vm, string command)
        {
            if (command == "test")
            {
                // CRITICAL CHANGE: Calling the integrated, corrected TestConnections method
                var (ok, msg) = await TestConnections(vm);
                TempData.SetStatusMessageModel(new StatusMessageModel
                {
                    Severity = ok ? StatusMessageModel.StatusSeverity.Success : StatusMessageModel.StatusSeverity.Error,
                    Message = msg
                });
                return View("/Views/Zano/ServerZanoSettings.cshtml", vm);
            }

            await _settingsRepository.UpdateSetting(vm, "ZanoServerSettings");
            TempData.SetStatusMessageModel(new StatusMessageModel
            {
                Severity = StatusMessageModel.StatusSeverity.Success,
                Message = "Zano settings saved. Restart may be required for changes to take effect."
            });
            return RedirectToAction(nameof(Settings));
        }

        // The standard path for Cryptonote JSON-RPC calls
        private const string RpcPath = "/json_rpc";

        // Generic RPC ping method with correct path and authentication logic
        private static async Task<(bool ok, string message)> PingRpc(
            Uri baseUri,
            string rpcMethod,
            string serviceName,
            string username = null,
            string password = null)
        {
            // 1. Construct the full RPC URI (e.g., http://host:port/json_rpc)
            var fullUri = new Uri(baseUri, RpcPath);

            try
            {
                using var client = new HttpClient();
                // Add basic authentication if credentials are provided
                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
                }

                using var req = new HttpRequestMessage(HttpMethod.Post, fullUri);

                // 2. Define the JSON-RPC payload for the specific method
                string payload = $"{{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"{rpcMethod}\"}}";
                req.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                var resp = await client.SendAsync(req);

                // 3. Check for successful HTTP status code (200-299)
                if (!resp.IsSuccessStatusCode)
                {
                    return (false, $"{serviceName} ({baseUri}) responded with HTTP error: {(int)resp.StatusCode}");
                }

                // Note: For full robustness, you would also parse the JSON response
                // here to ensure the RPC method didn't return a JSON error object, 
                // but checking the HTTP status is a strong first step.

                return (true, $"{serviceName} ({baseUri}) is OK (Method: {rpcMethod})");
            }
            catch (Exception ex)
            {
                return (false, $"{serviceName} ({baseUri}) failed to connect: {ex.Message}");
            }
        }

        // The main connection test method, now using the corrected PingRpc
        public static async Task<(bool ok, string message)> TestConnections(ZanoServerSettings vm)
        {
            var allOk = true;
            var msg = new StringBuilder();

            // ------------------------------------------
            // 1. ZANO DAEMON (NODE) TEST: Uses 'getinfo'
            // ------------------------------------------
            if (vm.ZANO_DAEMON_URI is not null)
            {
                var (ok, m) = await PingRpc(vm.ZANO_DAEMON_URI, "getinfo", "Zano Daemon", vm.ZANO_DAEMON_USERNAME, vm.ZANO_DAEMON_PASSWORD);
                allOk &= ok;
                msg.AppendLine(m);
            }

            // ------------------------------------------
            // 2. ZANO WALLET RPC TEST: Uses 'get_address'
            // ------------------------------------------
            if (vm.ZANO_WALLET_DAEMON_URI is not null)
            {
                var (ok, m) = await PingRpc(vm.ZANO_WALLET_DAEMON_URI, "getaddress", "Zano Wallet RPC", vm.ZANO_DAEMON_USERNAME, vm.ZANO_DAEMON_PASSWORD);
                allOk &= ok;
                msg.AppendLine(m);
            }

            // FUSD reuses ZANO endpoints; no separate test inputs needed.

            return (allOk, msg.ToString().Trim());
        }
    }

    public class ZanoServerSettings
    {
        public Uri ZANO_DAEMON_URI { get; set; }
        public Uri ZANO_WALLET_DAEMON_URI { get; set; }
        public string ZANO_DAEMON_USERNAME { get; set; }
        public string ZANO_DAEMON_PASSWORD { get; set; }

        // FUSD reuses ZANO endpoints; no separate fields required

        public string WALLET_DIR { get; set; }
    }
}
