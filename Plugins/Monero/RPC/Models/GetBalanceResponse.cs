using Newtonsoft.Json;

namespace BTCPayServer.Plugins.Monero.RPC.Models;

public class GetBalanceResponse
{
    [JsonProperty("unlocked_balance")] public long UnlockedBalance { get; set; }
}