using System;
using System.Collections.Generic;

namespace BTCPayServer.Plugins.Zano.Configuration
{
    public class ZanoLikeConfiguration
    {
        public Dictionary<string, ZanoLikeConfigurationItem> ZanoLikeConfigurationItems { get; set; } = [];
    }

    public class ZanoLikeConfigurationItem
    {
        public Uri DaemonRpcUri { get; set; }
        public Uri InternalWalletRpcUri { get; set; }
        public string WalletDirectory { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public Uri CashCowWalletRpcUri { get; set; }
    }
}