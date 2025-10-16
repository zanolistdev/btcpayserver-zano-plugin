﻿using Newtonsoft.Json;

namespace BTCPayServer.Plugins.Zano.RPC.Models
{
    public class GenerateBlocks
    {
        [JsonProperty("wallet_address")]
        public string WalletAddress { get; set; }
        
        [JsonProperty("amount_of_blocks")]
        public int AmountOfBlocks { get; set; }
    }
}