using System;
using System.Threading.Tasks;

using BTCPayServer.Data;
using BTCPayServer.Payments;
using BTCPayServer.Plugins.Zano.RPC.Models;
using BTCPayServer.Plugins.Zano.Services;
using BTCPayServer.Plugins.Zano.Utils;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BTCPayServer.Plugins.Zano.Payments
{
    public class ZanoLikePaymentHandler : IPaymentMethodHandler
    {
        private readonly ZanoLikeSpecificBtcPayNetwork _network;
        public ZanoLikeSpecificBtcPayNetwork Network => _network;
        public JsonSerializer Serializer { get; }
        private readonly ZanoRPCProvider _zanoRpcProvider;

        public PaymentMethodId PaymentMethodId { get; }

        public ZanoLikePaymentHandler(ZanoLikeSpecificBtcPayNetwork network, ZanoRPCProvider zanoRpcProvider)
        {
            PaymentMethodId = PaymentTypes.CHAIN.GetPaymentMethodId(network.CryptoCode);
            _network = network;
            Serializer = BlobSerializer.CreateSerializer().Serializer;
            _zanoRpcProvider = zanoRpcProvider;
        }
        bool IsReady() => _zanoRpcProvider.IsConfigured(_network.CryptoCode) && _zanoRpcProvider.IsAvailable(_network.CryptoCode);

        public Task BeforeFetchingRates(PaymentMethodContext context)
        {
            context.Prompt.Currency = _network.CryptoCode;
            context.Prompt.Divisibility = _network.Divisibility;
            if (context.Prompt.Activated && IsReady())
            {
                var supportedPaymentMethod = ParsePaymentMethodConfig(context.PaymentMethodConfig);
                var walletClient = _zanoRpcProvider.WalletRpcClients[_network.CryptoCode];
                var daemonClient = _zanoRpcProvider.DaemonRpcClients[_network.CryptoCode];
                try
                {
                    context.State = new Prepare()
                    {
                        GetFeeRate = daemonClient.SendCommandAsync<GetFeeEstimateRequest, GetFeeEstimateResponse>("getinfo", new GetFeeEstimateRequest()),
                        ReserveAddress = s => walletClient.SendCommandAsync<CreateAddressRequest, CreateAddressResponse>("make_integrated_address", new() { Label = $"btcpay invoice #{s}", AccountIndex = supportedPaymentMethod.AccountAddress }),
                        AccountAddress = supportedPaymentMethod.AccountAddress
                    };
                }
                catch (Exception ex)
                {
                    context.Logs.Write($"Error in BeforeFetchingRates: {ex.Message}", InvoiceEventData.EventSeverity.Error);
                }
            }
            return Task.CompletedTask;
        }

        public async Task ConfigurePrompt(PaymentMethodContext context)
        {
            if (!_zanoRpcProvider.IsConfigured(_network.CryptoCode))
            {
                throw new PaymentMethodUnavailableException($"BTCPAY_ZANO_DAEMON_URI or BTCPAY_ZANO_WALLET_DAEMON_URI isn't configured");
            }

            if (!_zanoRpcProvider.IsAvailable(_network.CryptoCode) || context.State is not Prepare zanoPrepare)
            {
                throw new PaymentMethodUnavailableException($"Node or wallet not available");
            }

            var invoice = context.InvoiceEntity;
            var feeRatePerKb = await zanoPrepare.GetFeeRate;
            var address = await zanoPrepare.ReserveAddress(invoice.Id);

            var feeRatePerByte = feeRatePerKb.DefaultFee / 1024;
            var details = new ZanoLikeOnChainPaymentMethodDetails()
            {
                AccountAddres = zanoPrepare.AccountAddress,
               // AccountAddres = address.Address,
                InvoiceSettledConfirmationThreshold = ParsePaymentMethodConfig(context.PaymentMethodConfig).InvoiceSettledConfirmationThreshold
            };
            context.Prompt.Destination = address.Address;
            context.Prompt.PaymentMethodFee = ZanoMoney.Convert(feeRatePerByte * 100);
            context.Prompt.Details = JObject.FromObject(details, Serializer);
            context.TrackedDestinations.Add(address.PaymentId);
        }
        private ZanoPaymentPromptDetails ParsePaymentMethodConfig(JToken config)
        {
            return config.ToObject<ZanoPaymentPromptDetails>(Serializer) ?? throw new FormatException($"Invalid {nameof(ZanoLikePaymentHandler)}");
        }
        object IPaymentMethodHandler.ParsePaymentMethodConfig(JToken config)
        {
            return ParsePaymentMethodConfig(config);
        }

        class Prepare
        {
            public Task<GetFeeEstimateResponse> GetFeeRate;
            public Func<string, Task<CreateAddressResponse>> ReserveAddress;

            public long AccountAddress { get; internal set; }
        }

        public ZanoLikeOnChainPaymentMethodDetails ParsePaymentPromptDetails(JToken details)
        {
            return details.ToObject<ZanoLikeOnChainPaymentMethodDetails>(Serializer);
        }
        object IPaymentMethodHandler.ParsePaymentPromptDetails(JToken details)
        {
            return ParsePaymentPromptDetails(details);
        }

        public ZanoLikePaymentData ParsePaymentDetails(JToken details)
        {
            return details.ToObject<ZanoLikePaymentData>(Serializer) ?? throw new FormatException($"Invalid {nameof(ZanoLikePaymentHandler)}");
        }
        object IPaymentMethodHandler.ParsePaymentDetails(JToken details)
        {
            return ParsePaymentDetails(details);
        }
    }
}