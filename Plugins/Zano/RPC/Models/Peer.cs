using Newtonsoft.Json;

namespace BTCPayServer.Plugins.Zano.RPC.Models
{
    public partial class Peer
    {
        [JsonProperty("info")]
        public Info Info { get; set; }
    }
}