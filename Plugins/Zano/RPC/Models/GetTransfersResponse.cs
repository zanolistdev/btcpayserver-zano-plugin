using System.Collections.Generic;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.Zano.RPC.Models
{
    public class EmployedEntries
    {
        [JsonProperty("receive")]
        public List<Receive> Receive { get; set; }
    }

    public class Pi
    {
        [JsonProperty("balance")]
        public long Balance { get; set; }
        
        [JsonProperty("curent_height")]
        public int CurrentHeight { get; set; }
        
        [JsonProperty("transfer_entries_count")]
        public int TransferEntriesCount { get; set; }
        
        [JsonProperty("transfers_count")]
        public int TransfersCount { get; set; }
        
        [JsonProperty("unlocked_balance")]
        public long UnlockedBalance { get; set; }
    }

    public class Receive
    {
        [JsonProperty("amount")]
        public long Amount { get; set; }
        
        [JsonProperty("asset_id")]
        public string AssetId { get; set; }
        
        [JsonProperty("index")]
        public int Index { get; set; }
    }

    public class GetTransfersResponse
    {
        [JsonProperty("last_item_index")]
        public int LastItemIndex { get; set; }
        
        [JsonProperty("pi")]
        public Pi Pi { get; set; }
        
        [JsonProperty("total_transfers")]
        public long TotalTransfers { get; set; }
        
        [JsonProperty("transfers")]
        public List<Transfer> Transfers { get; set; }
    }

    public class ServiceEntry
    {
        [JsonProperty("body")]
        public string Body { get; set; }
        
        [JsonProperty("flags")]
        public int Flags { get; set; }
        
        [JsonProperty("instruction")]
        public string Instruction { get; set; }
        
        [JsonProperty("security")]
        public string Security { get; set; }
        
        [JsonProperty("service_id")]
        public string ServiceId { get; set; }
    }

    public class Subtransfer
    {
        [JsonProperty("amount")]
        public long Amount { get; set; }
        
        [JsonProperty("asset_id")]
        public string AssetId { get; set; }
        
        [JsonProperty("is_income")]
        public bool IsIncome { get; set; }
    }

    public class Transfer
    {
        [JsonProperty("comment")]
        public string Comment { get; set; }
        
        [JsonProperty("employed_entries")]
        public EmployedEntries EmployedEntries { get; set; }
        
        [JsonProperty("fee")]
        public object Fee { get; set; }
        
        [JsonProperty("height")]
        public int Height { get; set; }
        
        [JsonProperty("is_mining")]
        public bool IsMining { get; set; }
        
        [JsonProperty("is_mixing")]
        public bool IsMixing { get; set; }
        
        [JsonProperty("is_service")]
        public bool IsService { get; set; }
        
        [JsonProperty("payment_id")]
        public string PaymentId { get; set; }
        
        [JsonProperty("remote_addresses")]
        public List<string> RemoteAddresses { get; set; }
        
        [JsonProperty("show_sender")]
        public bool ShowSender { get; set; }
        
        [JsonProperty("subtransfers")]
        public List<Subtransfer> Subtransfers { get; set; }
        
        [JsonProperty("timestamp")]
        public int Timestamp { get; set; }
        
        [JsonProperty("transfer_internal_index")]
        public int TransferInternalIndex { get; set; }
        
        [JsonProperty("tx_blob_size")]
        public int TxBlobSize { get; set; }
        
        [JsonProperty("tx_hash")]
        public string TxHash { get; set; }
        
        [JsonProperty("tx_type")]
        public int TxType { get; set; }
        
        [JsonProperty("unlock_time")]
        public int UnlockTime { get; set; }
        
        [JsonProperty("service_entries")]
        public List<ServiceEntry> ServiceEntries { get; set; }
    }
}