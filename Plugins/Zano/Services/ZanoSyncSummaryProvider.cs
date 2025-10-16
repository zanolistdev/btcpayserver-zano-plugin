using System.Collections.Generic;
using System.Linq;

using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Client.Models;
using BTCPayServer.Payments;

namespace BTCPayServer.Plugins.Zano.Services
{
    public class ZanoSyncSummaryProvider : ISyncSummaryProvider
    {
        private readonly ZanoRPCProvider _zanoRpcProvider;

        public ZanoSyncSummaryProvider(ZanoRPCProvider zanoRpcProvider)
        {
            _zanoRpcProvider = zanoRpcProvider;
        }

        public bool AllAvailable()
        {
            return _zanoRpcProvider.Summaries.All(pair => pair.Value.WalletAvailable);
        }

        public string Partial { get; } = "/Views/Zano/ZanoSyncSummary.cshtml";
        public IEnumerable<ISyncStatus> GetStatuses()
        {
            return _zanoRpcProvider.Summaries.Select(pair => new ZanoSyncStatus()
            {
                Summary = pair.Value,
                PaymentMethodId = PaymentMethodId.Parse(pair.Key).ToString()
            });
        }
    }

    public class ZanoSyncStatus : SyncStatus, ISyncStatus
    {
        public override bool Available
        {
            get
            {
                return Summary?.WalletAvailable ?? false;
            }
        }

        public ZanoRPCProvider.ZanoLikeSummary Summary { get; set; }
    }
}