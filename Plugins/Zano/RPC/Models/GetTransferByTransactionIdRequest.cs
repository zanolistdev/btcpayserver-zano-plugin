using Newtonsoft.Json;

namespace BTCPayServer.Plugins.Zano.RPC.Models
{
    public class GetTransferByTransactionIdRequest
    {
        [JsonProperty("txid")]
        public string TransactionId { get; set; }

        [JsonProperty("account_index", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public long? AccountIndex { get; set; }
    }
}