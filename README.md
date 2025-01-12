# Monero support plugin

This plugin extends BTCPay Server to enable users to receive payments via Monero.

> [!WARNING]
> This plugin shares a single Monero wallet across all the stores in the BTCPay Server instance. Use this plugin only if you are not sharing your instance.

![Checkout](./img/Checkout.png)

## Configuration

Configure this plugin using the following environment variables:

| Environment variable | Description                                                                                                                                                                                                                                   | Example |
| --- |-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------| --- |
**BTCPAY_XMR_DAEMON_URI** | **Required**. The URI of the [monerod](https://github.com/monero-project/monero) RPC interface.                                                                                                                                               | http://127.0.0.1:18081 |
**BTCPAY_XMR_DAEMON_USERNAME** | **Optional**.  The username for authenticating with the daemon.                                                                                                                                                                               | john |
**BTCPAY_XMR_DAEMON_PASSWORD** | **Optional**. The password for authenticating with the daemon.                                                                                                                                                                                | secret |
**BTCPAY_XMR_WALLET_DAEMON_URI** | **Required**.  The URI of the [monero-wallet-rpc](https://getmonero.dev/interacting/monero-wallet-rpc.html) RPC interface.                                                                                                                    | http://127.0.0.1:18082 |
**BTCPAY_XMR_WALLET_DAEMON_WALLETDIR** | **Optional**. The directory where BTCPay Server saves wallet files uploaded via the UI ([See this blog post for more details](https://sethforprivacy.com/guides/accepting-monero-via-btcpay-server/#configure-the-bitcoin-wallet-of-choice)). | /home/cypherpunk/Monero/wallets/ |

BTCPay Server's Docker deployment simplifies the setup by automatically configuring these variables. For further details, refer to this [blog post](https://sethforprivacy.com/guides/accepting-monero-via-btcpay-server).

# For maintainers

If you are a developer maintaining this plugin, in order to maintain this plugin, you need to clone this repository with `--recurse-submodules`:
```bash
git clone --recurse-submodules https://github.com/btcpayserver/btcpayserver-monero-plugin
```
Then run the tests dependencies
```bash
docker-compose up -d dev
```

Then create the `appsettings.dev.json` file in `btcpayserver/BTCPayServer`, with the following content:

```json
{
  "DEBUG_PLUGINS": "C:\\Sources\\btcpayserver-monero-plugin\\Plugins\\Monero\\bin\\Debug\\net8.0\\BTCPayServer.Plugins.Monero.dll",
  "XMR_DAEMON_URI": "http://127.0.0.1:18081",
  "XMR_WALLET_DAEMON_URI": "http://127.0.0.1:18082",
  "XMR_CASHCOW_WALLET_DAEMON_URI": "http://127.0.0.1:18092",
}
```

Please replace `C:\\Sources\\btcpayserver-monero-plugin` with the absolute path of your repository.

This will ensure that BTCPay Server loads the plugin when it starts.

Finally, set up BTCPay Server as the startup project in [Rider](https://www.jetbrains.com/rider/) or Visual Studio.

Note: Running or compiling the BTCPay Server project will not automatically recompile the plugin project. Therefore, if you make any changes to the project, do not forget to build it before running BTCPay Server in debug mode.

We recommend using [Rider](https://www.jetbrains.com/rider/) for plugin development, as it supports hot reload with plugins. You can edit `.cshtml` files, save, and refresh the page to see the changes.

Visual Studio does not support this feature.

When debugging in regtest, BTCPay Server will automatically create an configure two wallets. (cashcow and merchant)
You can trigger payments or mine blocks on the invoice's checkout page.

# Licence

[MIT](LICENSE.md)