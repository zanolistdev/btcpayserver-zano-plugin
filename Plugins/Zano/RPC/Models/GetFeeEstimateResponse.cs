using Newtonsoft.Json;

namespace BTCPayServer.Plugins.Zano.RPC.Models
{
    public class GetFeeEstimateResponse
    {
        [JsonProperty("default_fee")]
        public long DefaultFee { get; set; }
    }
}