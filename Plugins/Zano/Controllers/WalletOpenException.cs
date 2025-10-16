using System;

namespace BTCPayServer.Plugins.Zano.Controllers;

public class WalletOpenException(string message) : Exception(message);