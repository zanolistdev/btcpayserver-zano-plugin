using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Services;
using System.Net.Http;
using System.Net;
using BTCPayServer.Hosting;
using BTCPayServer.Payments;
using BTCPayServer.Payments.Bitcoin;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using BTCPayServer.Configuration;
using System.Linq;
using System;
using System.Globalization;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Plugins.Altcoins;
using BTCPayServer.Plugins.Monero.Configuration;
using BTCPayServer.Plugins.Monero.Payments;
using BTCPayServer.Plugins.Monero.Services;
using BTCPayServer.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NBXplorer;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Monero;

public class MoneroPlugin : BaseBTCPayServerPlugin
{
    public override IBTCPayServerPlugin.PluginDependency[] Dependencies { get; } =
    {
        new IBTCPayServerPlugin.PluginDependency { Identifier = nameof(BTCPayServer), Condition = ">=2.0.5" }
    };
    public ChainName ChainName { get; private set; }
    public NBXplorerNetworkProvider NBXplorerNetworkProvider { get; private set; }
    public override void Execute(IServiceCollection services)
    {
        var network = new MoneroLikeSpecificBtcPayNetwork()
        {
            CryptoCode = "XMR",
            DisplayName = "Monero",
            Divisibility = 12,
            DefaultRateRules = new[]
            {
                    "XMR_X = XMR_BTC * BTC_X",
                    "XMR_BTC = kraken(XMR_BTC)"
                },
            CryptoImagePath = "/imlegacy/monero.svg",
            UriScheme = "monero"
        };
        var blockExplorerLink = ChainName == ChainName.Mainnet
                    ? "https://www.exploremonero.com/transaction/{0}"
                    : "https://testnet.xmrchain.net/tx/{0}";
        var pmi = PaymentTypes.CHAIN.GetPaymentMethodId("XMR");
        services.AddDefaultPrettyName(pmi, network.DisplayName);
        services.AddBTCPayNetwork(network)
                .AddTransactionLinkProvider(pmi, new SimpleTransactionLinkProvider(blockExplorerLink));


        services.AddSingleton(provider =>
                ConfigureMoneroLikeConfiguration(provider));
        services.AddHttpClient("XMRclient")
            .ConfigurePrimaryHttpMessageHandler(provider =>
            {
                var configuration = provider.GetRequiredService<MoneroLikeConfiguration>();
                if (!configuration.MoneroLikeConfigurationItems.TryGetValue("XMR", out var xmrConfig) || xmrConfig.Username is null || xmrConfig.Password is null)
                {
                    return new HttpClientHandler();
                }
                return new HttpClientHandler
                {
                    Credentials = new NetworkCredential(xmrConfig.Username, xmrConfig.Password),
                    PreAuthenticate = true
                };
            });
        services.AddSingleton<MoneroRPCProvider>();
        services.AddHostedService<MoneroLikeSummaryUpdaterHostedService>();
        services.AddHostedService<MoneroListener>();
        services.AddSingleton<IPaymentMethodHandler>(provider =>
                (IPaymentMethodHandler)ActivatorUtilities.CreateInstance(provider, typeof(MoneroLikePaymentMethodHandler), new object[] { network }));
        services.AddSingleton<IPaymentLinkExtension>(provider =>
(IPaymentLinkExtension)ActivatorUtilities.CreateInstance(provider, typeof(MoneroPaymentLinkExtension), new object[] { network, pmi }));
        services.AddSingleton<ICheckoutModelExtension>(provider =>
(ICheckoutModelExtension)ActivatorUtilities.CreateInstance(provider, typeof(MoneroCheckoutModelExtension), new object[] { network, pmi }));
        
        services.AddSingleton<ICheckoutCheatModeExtension>(provider =>
            (ICheckoutCheatModeExtension)ActivatorUtilities.CreateInstance(provider, typeof(MoneroCheckoutCheatModeExtension), new object[] { network, pmi }));

        services.AddUIExtension("store-nav", "/Views/Monero/StoreNavMoneroExtension.cshtml");
        services.AddUIExtension("store-wallets-nav", "/Views/Monero/StoreWalletsNavMoneroExtension.cshtml");
        services.AddUIExtension("store-invoices-payments", "/Views/Monero/ViewMoneroLikePaymentData.cshtml");
        services.AddSingleton<ISyncSummaryProvider, MoneroSyncSummaryProvider>();
    }
    class SimpleTransactionLinkProvider : DefaultTransactionLinkProvider
    {
        public SimpleTransactionLinkProvider(string blockExplorerLink) : base(blockExplorerLink)
        {
        }

        public override string GetTransactionLink(string paymentId)
        {
            if (string.IsNullOrEmpty(BlockExplorerLink))
                return null;
            return string.Format(CultureInfo.InvariantCulture, BlockExplorerLink, paymentId);
        }
    }
    
    private static MoneroLikeConfiguration ConfigureMoneroLikeConfiguration(IServiceProvider serviceProvider)
    {
        var configuration = serviceProvider.GetService<IConfiguration>();
        var btcPayNetworkProvider = serviceProvider.GetService<BTCPayNetworkProvider>();
        var result = new MoneroLikeConfiguration();

        var supportedNetworks = btcPayNetworkProvider.GetAll()
            .OfType<MoneroLikeSpecificBtcPayNetwork>();

        foreach (var moneroLikeSpecificBtcPayNetwork in supportedNetworks)
        {
            var daemonUri =
                configuration.GetOrDefault<Uri>($"{moneroLikeSpecificBtcPayNetwork.CryptoCode}_daemon_uri",
                    null);
            var walletDaemonUri =
                configuration.GetOrDefault<Uri>(
                    $"{moneroLikeSpecificBtcPayNetwork.CryptoCode}_wallet_daemon_uri", null);
            var cashCowWalletDaemonUri =
                configuration.GetOrDefault<Uri>(
                    $"{moneroLikeSpecificBtcPayNetwork.CryptoCode}_cashcow_wallet_daemon_uri", null);
            var walletDaemonWalletDirectory =
                configuration.GetOrDefault<string>(
                    $"{moneroLikeSpecificBtcPayNetwork.CryptoCode}_wallet_daemon_walletdir", null);
            // Only for regtest
            var walletCashCowDaemonWalletDirectory =
                configuration.GetOrDefault<string>(
                    $"{moneroLikeSpecificBtcPayNetwork.CryptoCode}_cashcow_wallet_daemon_walletdir", null);
            var daemonUsername =
                configuration.GetOrDefault<string>(
                    $"{moneroLikeSpecificBtcPayNetwork.CryptoCode}_daemon_username", null);
            var daemonPassword =
                configuration.GetOrDefault<string>(
                    $"{moneroLikeSpecificBtcPayNetwork.CryptoCode}_daemon_password", null);
            if (daemonUri == null || walletDaemonUri == null || walletDaemonWalletDirectory == null)
            {
                var logger = serviceProvider.GetRequiredService<ILogger<MoneroPlugin>>();
                var cryptoCode = moneroLikeSpecificBtcPayNetwork.CryptoCode.ToUpperInvariant();
                if (daemonUri is null)
                {
					logger.LogWarning($"BTCPAY_{cryptoCode}_DAEMON_URI is not configured");
				}
                if (walletDaemonUri is null)
                {
                    logger.LogWarning($"BTCPAY_{cryptoCode}_WALLET_DAEMON_URI is not configured");
                }
                if (walletDaemonWalletDirectory is null)
				{
					logger.LogWarning($"BTCPAY_{cryptoCode}_WALLET_DAEMON_WALLETDIR is not configured");
				}
				logger.LogWarning($"{cryptoCode} got disabled as it is not fully configured.");
			}
            else
            {
                result.MoneroLikeConfigurationItems.Add(moneroLikeSpecificBtcPayNetwork.CryptoCode, new MoneroLikeConfigurationItem()
                {
                    DaemonRpcUri = daemonUri,
                    Username = daemonUsername,
                    Password = daemonPassword,
                    InternalWalletRpcUri = walletDaemonUri,
                    WalletDirectory = walletDaemonWalletDirectory,
                    CashCowWalletDirectory = walletCashCowDaemonWalletDirectory,
                    CashCowWalletRpcUri = cashCowWalletDaemonUri,
                });
            }
        }
        return result;
    }
}
