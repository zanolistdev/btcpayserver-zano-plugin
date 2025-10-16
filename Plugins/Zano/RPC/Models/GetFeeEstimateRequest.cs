using Newtonsoft.Json;

namespace BTCPayServer.Plugins.Zano.RPC.Models
{
    public class GetFeeEstimateRequest
    {
        [JsonProperty("grace_blocks")]
        public int? GraceBlocks { get; set; }
    }
}