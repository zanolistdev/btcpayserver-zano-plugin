using BTCPayServer.Abstractions.Contracts;
using System.Net.Http;
using System.Net;
using BTCPayServer.Hosting;
using BTCPayServer.Payments;
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
using NBXplorer;
using Microsoft.Extensions.Logging;

namespace BTCPayServer.Plugins.Monero;

public class MoneroPlugin : BaseBTCPayServerPlugin
{
    public override IBTCPayServerPlugin.PluginDependency[] Dependencies { get; } =
    {
        new IBTCPayServerPlugin.PluginDependency { Identifier = nameof(BTCPayServer), Condition = ">=2.0.5" }
    };
    string logo = "data:image/svg+xml,%3C%3Fxml%20version%3D%221.0%22%20encoding%3D%22iso-8859-1%22%3F%3E%3C!--%20Generator%3A%20Adobe%20Illustrator%2019.0.0%2C%20SVG%20Export%20Plug-In%20.%20SVG%20Version%3A%206.00%20Build%200)%20%20--%3E%3Csvg%20version%3D%221.1%22%20id%3D%22Capa_1%22%20xmlns%3D%22http%3A%2F%2Fwww.w3.org%2F2000%2Fsvg%22%20xmlns%3Axlink%3D%22http%3A%2F%2Fwww.w3.org%2F1999%2Fxlink%22%20x%3D%220px%22%20y%3D%220px%22%20viewBox%3D%220%200%20512%20512%22%20style%3D%22enable-background%3Anew%200%200%20512%20512%3B%22%20xml%3Aspace%3D%22preserve%22%3E%3Ccircle%20style%3D%22fill%3A%23F0EFEB%3B%22%20cx%3D%22256%22%20cy%3D%22256%22%20r%3D%22256%22%2F%3E%3Cpath%20style%3D%22fill%3A%234C4C4C%3B%22%20d%3D%22M364.2%2C393.163h107.979c-45.411%2C71.439-125.262%2C118.836-216.178%2C118.836S85.235%2C464.603%2C39.824%2C393.163h107.969V257.328l108.209%2C108.146L364.2%2C257.328V393.163z%22%2F%3E%3Cpath%20style%3D%22fill%3A%23FF6600%3B%22%20d%3D%22M512%2C256.001c0%2C28.599-4.692%2C56.1-13.343%2C81.784H421.21V122.537L256.002%2C286.062L90.794%2C122.537v215.248H13.346C4.694%2C312.102%2C0.003%2C284.6%2C0.003%2C256.001c0-141.384%2C114.614-255.998%2C255.998-255.998S512%2C114.616%2C512%2C256.001z%22%2F%3E%3Cg%3E%3C%2Fg%3E%3Cg%3E%3C%2Fg%3E%3Cg%3E%3C%2Fg%3E%3Cg%3E%3C%2Fg%3E%3Cg%3E%3C%2Fg%3E%3Cg%3E%3C%2Fg%3E%3Cg%3E%3C%2Fg%3E%3Cg%3E%3C%2Fg%3E%3Cg%3E%3C%2Fg%3E%3Cg%3E%3C%2Fg%3E%3Cg%3E%3C%2Fg%3E%3Cg%3E%3C%2Fg%3E%3Cg%3E%3C%2Fg%3E%3Cg%3E%3C%2Fg%3E%3Cg%3E%3C%2Fg%3E%3C%2Fsvg%3E";
    public override void Execute(IServiceCollection services)
    {
        var pluginServices = (PluginServiceCollection)services;
        var prov = pluginServices.BootstrapServices.GetRequiredService<NBXplorerNetworkProvider>();
        var chainName = prov.NetworkType;

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
            CryptoImagePath = logo,
            UriScheme = "monero"
        };
        var blockExplorerLink = chainName == ChainName.Mainnet
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
            var daemonUsername =
                configuration.GetOrDefault<string>(
                    $"{moneroLikeSpecificBtcPayNetwork.CryptoCode}_daemon_username", null);
            var daemonPassword =
                configuration.GetOrDefault<string>(
                    $"{moneroLikeSpecificBtcPayNetwork.CryptoCode}_daemon_password", null);
            if (daemonUri == null || walletDaemonUri == null)
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
                    CashCowWalletRpcUri = cashCowWalletDaemonUri,
                });
            }
        }
        return result;
    }
}
