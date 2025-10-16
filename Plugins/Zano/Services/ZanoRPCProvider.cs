using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

using BTCPayServer.Plugins.Zano.Configuration;
using BTCPayServer.Plugins.Zano.RPC;
using BTCPayServer.Plugins.Zano.RPC.Models;
using BTCPayServer.Services;

using Microsoft.Extensions.Logging;

using NBitcoin;

namespace BTCPayServer.Plugins.Zano.Services
{
    public class ZanoRPCProvider
    {
        private readonly ZanoLikeConfiguration _zanoLikeConfiguration;
        private readonly ILogger<ZanoRPCProvider> _logger;
        private readonly EventAggregator _eventAggregator;
        private readonly BTCPayServerEnvironment environment;
        public ImmutableDictionary<string, JsonRpcClient> DaemonRpcClients;
        public ImmutableDictionary<string, JsonRpcClient> WalletRpcClients;

        private readonly ConcurrentDictionary<string, ZanoLikeSummary> _summaries = new();

        public ConcurrentDictionary<string, ZanoLikeSummary> Summaries => _summaries;

        public ZanoRPCProvider(ZanoLikeConfiguration zanoLikeConfigurationItem,
            ILogger<ZanoRPCProvider> logger,
            EventAggregator eventAggregator,
            IHttpClientFactory httpClientFactory, BTCPayServerEnvironment environment)
        {
            _zanoLikeConfiguration = zanoLikeConfigurationItem;
            _logger = logger;
            _eventAggregator = eventAggregator;
            this.environment = environment;
            DaemonRpcClients =
                _zanoLikeConfiguration.ZanoLikeConfigurationItems.ToImmutableDictionary(pair => pair.Key,
                    pair => new JsonRpcClient(pair.Value.DaemonRpcUri, pair.Value.Username, pair.Value.Password,
                        httpClientFactory.CreateClient($"{pair.Key}client")));
            WalletRpcClients =
                _zanoLikeConfiguration.ZanoLikeConfigurationItems.ToImmutableDictionary(pair => pair.Key,
                    pair => new JsonRpcClient(pair.Value.InternalWalletRpcUri, "", "",
                        httpClientFactory.CreateClient($"{pair.Key}client")));
            if (environment.CheatMode)
            {
                CashCowWalletRpcClients =
                    _zanoLikeConfiguration.ZanoLikeConfigurationItems
                        .Where(i => i.Value.CashCowWalletRpcUri is not null).ToImmutableDictionary(pair => pair.Key,
                            pair => new JsonRpcClient(pair.Value.CashCowWalletRpcUri, "", "",
                                httpClientFactory.CreateClient($"{pair.Key}cashcow-client")));
            }
        }

        public ImmutableDictionary<string, JsonRpcClient> CashCowWalletRpcClients { get; set; }

        public bool IsConfigured(string cryptoCode) => WalletRpcClients.ContainsKey(cryptoCode) && DaemonRpcClients.ContainsKey(cryptoCode);
        public bool IsAvailable(string cryptoCode)
        {
            cryptoCode = cryptoCode.ToUpperInvariant();
            return _summaries.ContainsKey(cryptoCode) && IsAvailable(_summaries[cryptoCode]);
        }

        private bool IsAvailable(ZanoLikeSummary summary)
        {
            return summary.Synced &&
                   summary.WalletAvailable;
        }

        public async Task<ZanoLikeSummary> UpdateSummary(string cryptoCode)
        {
            _logger.LogDebug($"UpdateSummary called for {cryptoCode}");
            
            if (!DaemonRpcClients.TryGetValue(cryptoCode.ToUpperInvariant(), out var daemonRpcClient) ||
                !WalletRpcClients.TryGetValue(cryptoCode.ToUpperInvariant(), out var walletRpcClient))
            {
                _logger.LogWarning($"No RPC clients found for {cryptoCode}");
                return null;
            }

            var summary = new ZanoLikeSummary();
            try
            {
                _logger.LogDebug($"Getting daemon info for {cryptoCode}");
                var daemonResult =
                    await daemonRpcClient.SendCommandAsync<JsonRpcClient.NoRequestModel, GetInfoResponse>("getinfo",
                        JsonRpcClient.NoRequestModel.Instance);
                summary.TargetHeight = daemonResult.TargetHeight.GetValueOrDefault(0);
                summary.CurrentHeight = daemonResult.Height;
                summary.TargetHeight = summary.TargetHeight == 0 ? summary.CurrentHeight : summary.TargetHeight;
                summary.Synced = !daemonResult.BusySyncing;
                summary.UpdatedAt = DateTime.UtcNow;
                summary.DaemonAvailable = true;
                _logger.LogDebug($"{cryptoCode} Daemon info - Height: {summary.CurrentHeight}, Synced: {summary.Synced}, BusySyncing: {daemonResult.BusySyncing}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to get daemon info for {cryptoCode}");
                summary.DaemonAvailable = false;
            }

            bool walletCreated = false;
            try
            {
                _logger.LogDebug($"Getting wallet height for {cryptoCode}");

                var client = new HttpClient();

                var path = _zanoLikeConfiguration.ZanoLikeConfigurationItems.ToImmutableDictionary(pair => pair.Key,
                        pair => pair.Value.DaemonRpcUri).FirstOrDefault().Value;
                var request = new HttpRequestMessage(HttpMethod.Get,
                    new Uri(path, "getheight"));

                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var heightInfo = JsonSerializer.Deserialize<GetHeightResponse>(json, options);

                _logger.LogDebug($"{cryptoCode} Wallet height: {heightInfo.Height}");

                summary.WalletHeight = heightInfo.Height;
                summary.WalletAvailable = true;
            }
            catch when (environment.CheatMode && !walletCreated)
            {
                //await CreateTestWallet(walletRpcClient);
                //walletCreated = true;
                //goto retry;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to get wallet height for {cryptoCode}");
                summary.WalletAvailable = false;
            }

            //if (environment.CheatMode &&
            //   CashCowWalletRpcClients.TryGetValue(cryptoCode.ToUpperInvariant(), out var cashCow))
            //{
            //    await MakeCashCowFat(cashCow, daemonRpcClient);
            //} 

            var changed = !_summaries.ContainsKey(cryptoCode) || IsAvailable(cryptoCode) != IsAvailable(summary);

            _summaries.AddOrReplace(cryptoCode, summary);
            if (changed)
            {
                _logger.LogInformation($"State changed for {cryptoCode}, publishing ZanoDaemonStateChange event");
                _eventAggregator.Publish(new ZanoDaemonStateChange() { Summary = summary, CryptoCode = cryptoCode });
            }
            else
            {
                _logger.LogDebug($"No state change for {cryptoCode}");
            }

            return summary;
        }

        //private async Task MakeCashCowFat(JsonRpcClient cashcow, JsonRpcClient deamon)
        //{
        //    try
        //    {
        //        var walletResult =
        //            await cashcow.SendCommandAsync<JsonRpcClient.NoRequestModel, GetHeightResponse>(
        //                "get_height", JsonRpcClient.NoRequestModel.Instance);
        //    }
        //    catch
        //    {
        //        _logger.LogInformation("Creating XMR cashcow wallet...");
        //        //  await CreateTestWallet(cashcow);
        //    }

        //    var balance =
        //        (await cashcow.SendCommandAsync<JsonRpcClient.NoRequestModel, GetBalanceResponse>("getbalance",
        //            JsonRpcClient.NoRequestModel.Instance));
        //    if (balance.UnlockedBalance != 0)
        //    {
        //        return;
        //    }
        //    _logger.LogInformation("Mining blocks for the cashcow...");
        //    var address = (await cashcow.SendCommandAsync<GetAddressRequest, GetAddressResponse>("getaddress", new()
        //    {
        //        AccountIndex = 0
        //    })).Address;
        //    await deamon.SendCommandAsync<GenerateBlocks, JsonRpcClient.NoRequestModel>("generateblocks", new GenerateBlocks()
        //    {
        //        WalletAddress = address,
        //        AmountOfBlocks = 100
        //    });
        //    _logger.LogInformation("Mining succeed!");
        //}

        //private static async Task CreateTestWallet(JsonRpcClient walletRpcClient)
        //{
        //    try
        //    {
        //        await walletRpcClient.SendCommandAsync<OpenWalletRequest, JsonRpcClient.NoRequestModel>(
        //            "open_wallet",





        //            new OpenWalletRequest()
        //            {
        //                Filename = "wallet",
        //                Password = "password"
        //            });
        //        return;
        //    }
        //    catch
        //    {
        //        // ignored
        //    }

        //    await walletRpcClient.SendCommandAsync<CreateWalletRequest, JsonRpcClient.NoRequestModel>("create_wallet",
        //        new()
        //        {
        //            Filename = "wallet",
        //            Password = "password",
        //            Language = "English"
        //        });
        //}


        public class ZanoDaemonStateChange
        {
            public string CryptoCode { get; set; }
            public ZanoLikeSummary Summary { get; set; }
        }

        public class ZanoLikeSummary
        {
            public bool Synced { get; set; }
            public long CurrentHeight { get; set; }
            public long WalletHeight { get; set; }
            public long TargetHeight { get; set; }
            public DateTime UpdatedAt { get; set; }
            public bool DaemonAvailable { get; set; }
            public bool WalletAvailable { get; set; }
        }
    }
}