using Newtonsoft.Json;

namespace BTCPayServer.Plugins.Zano.RPC.Models
{
    public partial class CreateAccountRequest
    {
        [JsonProperty("label")]
        public string Label { get; set; }
    }
}