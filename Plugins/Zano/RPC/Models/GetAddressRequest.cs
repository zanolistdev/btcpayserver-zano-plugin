﻿using Newtonsoft.Json;

namespace BTCPayServer.Plugins.Zano.RPC.Models
{
    public class GetAddressRequest
    {
        [JsonProperty("account_index")]
        public int AccountIndex { get; set; }
    }

    public class GetAddressResponse
    {
        [JsonProperty("address")]
        public string Address { get; set; }
    }
}