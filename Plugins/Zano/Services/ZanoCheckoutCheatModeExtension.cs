using System.Threading.Tasks;

using BTCPayServer.Payments;
using BTCPayServer.Plugins.Zano.RPC;
using BTCPayServer.Plugins.Zano.RPC.Models;

namespace BTCPayServer.Plugins.Zano.Services;

public class ZanoCheckoutCheatModeExtension : ICheckoutCheatModeExtension
{
    private readonly ZanoRPCProvider _rpcProvider;
    private readonly ZanoLikeSpecificBtcPayNetwork _network;
    private readonly PaymentMethodId _paymentMethodId;

    public ZanoCheckoutCheatModeExtension(
        ZanoRPCProvider rpcProvider,
        ZanoLikeSpecificBtcPayNetwork network,
        PaymentMethodId paymentMethodId)
    {
        _rpcProvider = rpcProvider;
        _network = network;
        _paymentMethodId = paymentMethodId;
    }

    public bool Handle(PaymentMethodId paymentMethodId) => _paymentMethodId == paymentMethodId;

    public async Task<ICheckoutCheatModeExtension.PayInvoiceResult> PayInvoice(ICheckoutCheatModeExtension.PayInvoiceContext payInvoiceContext)
    {
        var amount = payInvoiceContext.Amount;
        for (int i = 0; i < _network.Divisibility; i++)
        {
            amount *= 10;
        }

        var cashcow = _rpcProvider.CashCowWalletRpcClients[_network.CryptoCode];
        var result = await cashcow.SendCommandAsync<TransferRequest, TransferResponse>("transfer",
            new TransferRequest()
            {
                Destinations = new[] { new TransferDestination()
                {
                    Amount = (long)amount,
                    Address = payInvoiceContext.PaymentPrompt.Destination
                }
            }
            });
        return new ICheckoutCheatModeExtension.PayInvoiceResult(result.TransactionHash);
    }

    public async Task<ICheckoutCheatModeExtension.MineBlockResult> MineBlock(ICheckoutCheatModeExtension.MineBlockContext mineBlockContext)
    {
        var cashcow = _rpcProvider.CashCowWalletRpcClients[_network.CryptoCode];
        var deamon = _rpcProvider.DaemonRpcClients[_network.CryptoCode];
        var address = (await cashcow.SendCommandAsync<GetAddressRequest, GetAddressResponse>("get_address", new()
        {
            AccountIndex = 0
        })).Address;
        await deamon.SendCommandAsync<GenerateBlocks, JsonRpcClient.NoRequestModel>("generateblocks", new GenerateBlocks()
        {
            WalletAddress = address,
            AmountOfBlocks = mineBlockContext.BlockCount
        });
        return new ICheckoutCheatModeExtension.MineBlockResult();
    }
}