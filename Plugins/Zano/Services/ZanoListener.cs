using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using BTCPayServer.Client.Models;
using BTCPayServer.Data;
using BTCPayServer.Events;
using BTCPayServer.HostedServices;
using BTCPayServer.Payments;
using BTCPayServer.Plugins.Zano.Configuration;
using BTCPayServer.Plugins.Zano.Payments;
using BTCPayServer.Plugins.Zano.RPC;
using BTCPayServer.Plugins.Zano.RPC.Models;
using BTCPayServer.Plugins.Zano.Utils;
using BTCPayServer.Services;
using BTCPayServer.Services.Invoices;

using Microsoft.Extensions.Logging;



using Newtonsoft.Json.Linq;

namespace BTCPayServer.Plugins.Zano.Services
{
    public class ZanoListener : EventHostedServiceBase
    {
        private readonly InvoiceRepository _invoiceRepository;
        private readonly EventAggregator _eventAggregator;
        private readonly ZanoRPCProvider _zanoRpcProvider;
        private readonly ZanoLikeConfiguration _ZanoLikeConfiguration;
        private readonly BTCPayNetworkProvider _networkProvider;
        private readonly ILogger<ZanoListener> _logger;
        private readonly PaymentMethodHandlerDictionary _handlers;
        private readonly InvoiceActivator _invoiceActivator;
        private readonly PaymentService _paymentService;

        // Polling mechanism for block detection
        private readonly Dictionary<string, long> _lastKnownBlockHeights = [];
        private Timer _blockPollingTimer;
        private const int BLOCK_POLLING_INTERVAL_SECONDS = 3; // Check every 5 seconds for testing

        public ZanoListener(InvoiceRepository invoiceRepository,
            EventAggregator eventAggregator,
            ZanoRPCProvider zanoRpcProvider,
            ZanoLikeConfiguration zanoLikeConfigurationItem,
            BTCPayNetworkProvider networkProvider,
            ILogger<ZanoListener> logger,
            PaymentMethodHandlerDictionary handlers,
            InvoiceActivator invoiceActivator,
            PaymentService paymentService) : base(eventAggregator, logger)
        {
            _invoiceRepository = invoiceRepository;
            _eventAggregator = eventAggregator;
            _zanoRpcProvider = zanoRpcProvider;
            _ZanoLikeConfiguration = zanoLikeConfigurationItem;
            _networkProvider = networkProvider;
            _logger = logger;
            _handlers = handlers;
            _invoiceActivator = invoiceActivator;
            _paymentService = paymentService;
        }

        protected override void SubscribeToEvents()
        {
            base.SubscribeToEvents();
            Subscribe<ZanoEvent>();
            Subscribe<ZanoRPCProvider.ZanoDaemonStateChange>();
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await base.StartAsync(cancellationToken);

            _logger.LogInformation($"Starting ZanoListener with block polling every {BLOCK_POLLING_INTERVAL_SECONDS} seconds");

            // Start block polling timer
            _blockPollingTimer = new Timer(async _ => await PollForNewBlocks(), null,
                TimeSpan.FromSeconds(BLOCK_POLLING_INTERVAL_SECONDS),
                TimeSpan.FromSeconds(BLOCK_POLLING_INTERVAL_SECONDS));

            _logger.LogInformation("ZanoListener started with block polling enabled");
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _blockPollingTimer?.Dispose();
            await base.StopAsync(cancellationToken);
        }

        private async Task PollForNewBlocks()
        {
            try
            {
                _logger.LogDebug("Starting block polling cycle");

                foreach (var cryptoCode in _zanoRpcProvider.DaemonRpcClients.Keys)
                {
                    _logger.LogDebug($"Checking {cryptoCode} daemon availability");

                    var isAvailable = _zanoRpcProvider.IsAvailable(cryptoCode);
                    _logger.LogDebug($"{cryptoCode} IsAvailable: {isAvailable}");

                    if (_zanoRpcProvider.Summaries.TryGetValue(cryptoCode, out var summary))
                    {
                        _logger.LogDebug($"{cryptoCode} Summary - Synced: {summary.Synced}, WalletAvailable: {summary.WalletAvailable}, DaemonAvailable: {summary.DaemonAvailable}, CurrentHeight: {summary.CurrentHeight}");
                    }
                    else
                    {
                        _logger.LogDebug($"{cryptoCode} No summary available");
                    }

                    if (!isAvailable)
                    {
                        _logger.LogDebug($"{cryptoCode} daemon is not available, skipping");
                        continue;
                    }

                    var currentHeight = await GetCurrentBlockHeight(cryptoCode);
                    if (currentHeight.HasValue)
                    {
                        _logger.LogDebug($"Current block height for {cryptoCode}: {currentHeight.Value}");

                        if (!_lastKnownBlockHeights.ContainsKey(cryptoCode))
                        {
                            _lastKnownBlockHeights[cryptoCode] = currentHeight.Value;
                            _logger.LogInformation($"Initialized block height for {cryptoCode}: {currentHeight.Value}");
                        }
                        else if (currentHeight.Value > _lastKnownBlockHeights[cryptoCode])
                        {
                            _logger.LogInformation($"New block detected for {cryptoCode}: {_lastKnownBlockHeights[cryptoCode]} -> {currentHeight.Value}");
                            _lastKnownBlockHeights[cryptoCode] = currentHeight.Value;
                            await OnNewBlock(cryptoCode);
                        }
                        else if (currentHeight.Value == _lastKnownBlockHeights[cryptoCode])
                        {
                            _logger.LogDebug($"No new blocks for {cryptoCode}, height remains: {currentHeight.Value}");
                        }
                        else
                        {
                            _logger.LogWarning($"Block height decreased for {cryptoCode}: {_lastKnownBlockHeights[cryptoCode]} -> {currentHeight.Value}");
                            _lastKnownBlockHeights[cryptoCode] = currentHeight.Value;
                        }
                    }
                    else
                    {
                        _logger.LogWarning($"Could not get block height for {cryptoCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during block polling");
            }
        }

        private async Task<long?> GetCurrentBlockHeight(string cryptoCode)
        {
            try
            {
                if (!_zanoRpcProvider.DaemonRpcClients.TryGetValue(cryptoCode, out var daemonClient))
                {
                    _logger.LogWarning($"No daemon client found for {cryptoCode}");
                    return null;
                }

                _logger.LogDebug($"Making RPC call to get block height for {cryptoCode}");
                var result = await daemonClient.SendCommandAsync<JsonRpcClient.NoRequestModel, GetInfoResponse>(
                    "getinfo", JsonRpcClient.NoRequestModel.Instance);

                _logger.LogDebug($"RPC response for {cryptoCode}: Height={result.Height}, TargetHeight={result.TargetHeight}");
                return result.Height;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to get block height for {cryptoCode}");
                return null;
            }
        }

        protected override async Task ProcessEvent(object evt, CancellationToken cancellationToken)
        {
            if (evt is ZanoRPCProvider.ZanoDaemonStateChange stateChange)
            {
                if (_zanoRpcProvider.IsAvailable(stateChange.CryptoCode))
                {
                    _logger.LogInformation($"{stateChange.CryptoCode} just became available");
                    _ = UpdateAnyPendingZanoLikePayment(stateChange.CryptoCode);
                }
                else
                {
                    _logger.LogInformation($"{stateChange.CryptoCode} just became unavailable");
                }
            }
            else if (evt is ZanoEvent zanoEvent)
            {
                if (!_zanoRpcProvider.IsAvailable(zanoEvent.CryptoCode))
                {
                    return;
                }

                if (!string.IsNullOrEmpty(zanoEvent.BlockHash))
                {
                    await OnNewBlock(zanoEvent.CryptoCode);
                }

                if (!string.IsNullOrEmpty(zanoEvent.TransactionHash))
                {
                    await OnTransactionUpdated(zanoEvent.CryptoCode, zanoEvent.TransactionHash);
                }
            }
        }

        private async Task ReceivedPayment(InvoiceEntity invoice, PaymentEntity payment)
        {
            _logger.LogInformation(
                $"Invoice {invoice.Id} received payment {payment.Value} {payment.Currency} {payment.Id}");

            var prompt = invoice.GetPaymentPrompt(payment.PaymentMethodId);

            if (prompt != null &&
                prompt.Activated &&
                prompt.Destination == payment.Destination &&
                prompt.Calculate().Due > 0.0m)
            {
                await _invoiceActivator.ActivateInvoicePaymentMethod(invoice.Id, payment.PaymentMethodId, true);
                invoice = await _invoiceRepository.GetInvoice(invoice.Id);
            }

            _eventAggregator.Publish(
                new InvoiceEvent(invoice, InvoiceEvent.ReceivedPayment) { Payment = payment });
        }

        private async Task UpdatePaymentStates(string cryptoCode, InvoiceEntity[] invoices)
        {
            if (!invoices.Any())
            {
                return;
            }

            var zanoWalletRpcClient = _zanoRpcProvider.WalletRpcClients[cryptoCode];
            var network = _networkProvider.GetNetwork(cryptoCode);
            var paymentId = PaymentTypes.CHAIN.GetPaymentMethodId(network.CryptoCode);
            var handler = (ZanoLikePaymentHandler)_handlers[paymentId];

            //get all the required data in one list (invoice, its existing payments and the current payment method details)
            var expandedInvoices = invoices.Select(entity => (Invoice: entity,
                    ExistingPayments: GetAllZanoLikePayments(entity, cryptoCode),
                    Prompt: entity.GetPaymentPrompt(paymentId),
                    PaymentMethodDetails: handler.ParsePaymentPromptDetails(entity.GetPaymentPrompt(paymentId)
                        .Details)))
                .Select(tuple => (
                    tuple.Invoice,
                    tuple.PaymentMethodDetails,
                    tuple.Prompt,
                    ExistingPayments: tuple.ExistingPayments.Select(entity =>
                        (Payment: entity, PaymentData: handler.ParsePaymentDetails(entity.Details),
                            tuple.Invoice))
                ));

            var existingPaymentData = expandedInvoices.SelectMany(tuple => tuple.ExistingPayments);



            var keyValuePair = await zanoWalletRpcClient.SendCommandAsync<GetTransfersRequest, GetTransfersResponse>(
                      "get_recent_txs_and_info2",
                      new GetTransfersRequest()
                      {
                          Count = 100,
                          ExcludeMiningTxs = false,
                          ExcludeUnconfirmed = true,
                          Offset = 0,
                          Order = "FROM_END_TO_BEGIN",
                          UpdateProvisionInfo = true
                      });




            var transferProcessingTasks = new List<Task>();

            var updatedPaymentEntities = new List<(PaymentEntity Payment, InvoiceEntity invoice)>();

            var transfers = keyValuePair.Transfers;

            if (transfers.Count > 0 && expandedInvoices.Count() > 0)
            {
                foreach (var transfer in transfers)
                {

                    InvoiceEntity invoice = null;
                    var existingMatch = existingPaymentData.SingleOrDefault(tuple =>
                        tuple.Payment.Destination == transfer.PaymentId &&
                        tuple.PaymentData.TransactionId == transfer.TxHash);

                    if (existingMatch.Invoice != null)
                    {
                        invoice = existingMatch.Invoice;
                    }
                    else if (transfer.PaymentId != null)
                    {
                        var newMatch = expandedInvoices.SingleOrDefault(tuple =>
                                    tuple.Invoice.Addresses.Any(x => x.Address == transfer.PaymentId));


                        if (newMatch.Invoice == null)
                        {
                            continue;
                        }

                        invoice = newMatch.Invoice;
                    }
                    if (invoice != null)
                    {
                        var currentHeight = keyValuePair.Pi.CurrentHeight;
                        var confirmations = currentHeight - transfer.Height;
                        if (confirmations > 2)
                        {

                            transferProcessingTasks.Add(HandlePaymentData(cryptoCode, transfer.Subtransfers[0].Amount, transfer.TxHash, confirmations, currentHeight,
                               transfer.UnlockTime, invoice, updatedPaymentEntities));
                        }
                    }
                };
            }

            transferProcessingTasks.Add(
                _paymentService.UpdatePayments(updatedPaymentEntities.Select(tuple => tuple.Item1).ToList()));
            await Task.WhenAll(transferProcessingTasks);
            foreach (var valueTuples in updatedPaymentEntities.GroupBy(entity => entity.Item2))
            {
                if (valueTuples.Any())
                {
                    _eventAggregator.Publish(new InvoiceNeedUpdateEvent(valueTuples.Key.Id));
                }
            }

        }

        private async Task OnNewBlock(string cryptoCode)
        {
            _logger.LogInformation($"Processing new block for {cryptoCode}");
            await UpdateAnyPendingZanoLikePayment(cryptoCode);
            _eventAggregator.Publish(new NewBlockEvent()
            { PaymentMethodId = PaymentTypes.CHAIN.GetPaymentMethodId(cryptoCode) });
            _logger.LogInformation($"NewBlockEvent published for {cryptoCode}");
        }

        private async Task OnTransactionUpdated(string cryptoCode, string transactionHash)
        {
            var paymentMethodId = PaymentTypes.CHAIN.GetPaymentMethodId(cryptoCode);
            var transfer = await GetTransferByTxId(cryptoCode, transactionHash, this.CancellationToken);
            if (transfer is null)
            {
                return;
            }
            var paymentsToUpdate = new List<(PaymentEntity Payment, InvoiceEntity invoice)>();

            //group all destinations of the tx together and loop through the sets
            foreach (var destination in transfer.Transfers.GroupBy(destination => destination.Address))
            {
                //find the invoice corresponding to this address, else skip
                var invoice = await _invoiceRepository.GetInvoiceFromAddress(paymentMethodId, destination.Key);
                if (invoice == null)
                {
                    continue;
                }

                var index = destination.First().SubaddrIndex;

                //await HandlePaymentData(cryptoCode,
                //    destination.Sum(destination1 => destination1.Amount),
                //    index.Major,
                //    index.Minor,
                //    transfer.Transfer.Txid,
                //    transfer.Transfer.Confirmations,
                //    transfer.Transfer.Height
                //    , transfer.Transfer.UnlockTime, invoice, paymentsToUpdate);
            }

            if (paymentsToUpdate.Any())
            {
                await _paymentService.UpdatePayments(paymentsToUpdate.Select(tuple => tuple.Payment).ToList());
                foreach (var valueTuples in paymentsToUpdate.GroupBy(entity => entity.invoice))
                {
                    if (valueTuples.Any())
                    {
                        _eventAggregator.Publish(new InvoiceNeedUpdateEvent(valueTuples.Key.Id));
                    }
                }
            }
        }

        private async Task<GetTransferByTransactionIdResponse> GetTransferByTxId(string cryptoCode,
            string transactionHash, CancellationToken cancellationToken)
        {
            var accounts = await _zanoRpcProvider.WalletRpcClients[cryptoCode].SendCommandAsync<GetAccountsRequest, GetAccountsResponse>("get_accounts", new GetAccountsRequest(), cancellationToken);
            var accountIndexes = accounts
                .SubaddressAccounts
                .Select(a => new long?(a.AccountIndex))
                .ToList();
            if (accountIndexes.Count is 0)
            {
                accountIndexes.Add(null);
            }
            var req = accountIndexes
                .Select(i => GetTransferByTxId(cryptoCode, transactionHash, i))
                .ToArray();
            foreach (var task in req)
            {
                var result = await task;
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private async Task<GetTransferByTransactionIdResponse> GetTransferByTxId(string cryptoCode, string transactionHash, long? accountIndex)
        {
            try
            {
                var result = await _zanoRpcProvider.WalletRpcClients[cryptoCode]
                    .SendCommandAsync<GetTransferByTransactionIdRequest, GetTransferByTransactionIdResponse>(
                        "get_transfer_by_txid",
                        new GetTransferByTransactionIdRequest()
                        {
                            TransactionId = transactionHash,
                            AccountIndex = accountIndex
                        });
                return result;
            }
            catch (JsonRpcClient.JsonRpcApiException)
            {
                return null;
            }
        }

        private async Task HandlePaymentData(string cryptoCode, long totalAmount, string txId, long confirmations, long blockHeight, long locktime, InvoiceEntity invoice,
            List<(PaymentEntity Payment, InvoiceEntity invoice)> paymentsToUpdate)
        {
            var network = _networkProvider.GetNetwork(cryptoCode);
            var pmi = PaymentTypes.CHAIN.GetPaymentMethodId(network.CryptoCode);
            var handler = (ZanoLikePaymentHandler)_handlers[pmi];
            var promptDetails = handler.ParsePaymentPromptDetails(invoice.GetPaymentPrompt(pmi).Details);
            var details = new ZanoLikePaymentData()
            {
                TransactionId = txId,
                ConfirmationCount = confirmations,
                BlockHeight = blockHeight,
                LockTime = locktime,
                InvoiceSettledConfirmationThreshold = promptDetails.InvoiceSettledConfirmationThreshold
            };
            var status = GetStatus(details, invoice.SpeedPolicy) ? PaymentStatus.Settled : PaymentStatus.Processing;
            var paymentData = new PaymentData()
            {
                Status = status,
                Amount = ZanoMoney.Convert(totalAmount),
                Created = DateTimeOffset.UtcNow,
                Id = $"{txId}{new Guid().ToString()}",
                Currency = network.CryptoCode,
                InvoiceDataId = invoice.Id,
            }.Set(invoice, handler, details);


            //check if this tx exists as a payment to this invoice already
            var alreadyExistingPaymentThatMatches = GetAllZanoLikePayments(invoice, cryptoCode)
                .SingleOrDefault(c => c.Id == paymentData.Id && c.PaymentMethodId == pmi);

            //if it doesnt, add it and assign a new zanolike address to the system if a balance is still due
            if (alreadyExistingPaymentThatMatches == null)
            {
                var payment = await _paymentService.AddPayment(paymentData, [txId]);
                if (payment != null)
                {
                    await ReceivedPayment(invoice, payment);
                }
            }
            else
            {
                //else update it with the new data
                alreadyExistingPaymentThatMatches.Status = status;
                alreadyExistingPaymentThatMatches.Details = JToken.FromObject(details, handler.Serializer);
                paymentsToUpdate.Add((alreadyExistingPaymentThatMatches, invoice));
            }
        }

        private bool GetStatus(ZanoLikePaymentData details, SpeedPolicy speedPolicy)
            => ConfirmationsRequired(details, speedPolicy) <= details.ConfirmationCount;

        public static long ConfirmationsRequired(ZanoLikePaymentData details, SpeedPolicy speedPolicy)
            => (details, speedPolicy) switch
            {
                (_, _) when details.ConfirmationCount < details.LockTime =>
                    details.LockTime - details.ConfirmationCount,
                ({ InvoiceSettledConfirmationThreshold: long v }, _) => v,
                (_, SpeedPolicy.HighSpeed) => 0,
                (_, SpeedPolicy.MediumSpeed) => 1,
                (_, SpeedPolicy.LowMediumSpeed) => 2,
                (_, SpeedPolicy.LowSpeed) => 6,
                _ => 6,
            };


        private async Task UpdateAnyPendingZanoLikePayment(string cryptoCode)
        {
            var paymentMethodId = PaymentTypes.CHAIN.GetPaymentMethodId(cryptoCode);
            var invoices = await _invoiceRepository.GetMonitoredInvoices(paymentMethodId);
            if (!invoices.Any())
            {
                return;
            }
            invoices = invoices.Where(entity => entity.GetPaymentPrompt(paymentMethodId)?.Activated is true).ToArray();
            await UpdatePaymentStates(cryptoCode, invoices);
        }

        private IEnumerable<PaymentEntity> GetAllZanoLikePayments(InvoiceEntity invoice, string cryptoCode)
        {
            return invoice.GetPayments(false)
                .Where(p => p.PaymentMethodId == PaymentTypes.CHAIN.GetPaymentMethodId(cryptoCode));
        }
    }
}