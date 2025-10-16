using Newtonsoft.Json;

namespace BTCPayServer.Plugins.Zano.RPC.Models
{
    public partial class MakeUriResponse
    {
        [JsonProperty("uri")]
        public string Uri { get; set; }
    }
}