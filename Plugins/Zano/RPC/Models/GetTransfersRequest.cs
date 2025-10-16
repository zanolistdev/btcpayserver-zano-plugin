using Newtonsoft.Json;

namespace BTCPayServer.Plugins.Zano.RPC.Models
{
    public partial class GetTransfersRequest
    {
        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("exclude_mining_txs")]
        public bool ExcludeMiningTxs { get; set; }

        [JsonProperty("exclude_unconfirmed")]
        public bool ExcludeUnconfirmed { get; set; }

        [JsonProperty("offset")]
        public int Offset { get; set; }

        [JsonProperty("order")]
        public string Order { get; set; }

        [JsonProperty("update_provision_info")]
        public bool UpdateProvisionInfo { get; set; }
    }
}