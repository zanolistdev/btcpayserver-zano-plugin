using BTCPayServer.Plugins.Zano.RPC;

using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.Zano.Controllers
{
    [Route("[controller]")]
    public class ZanoLikeDaemonCallbackController : Controller
    {
        private readonly EventAggregator _eventAggregator;

        public ZanoLikeDaemonCallbackController(EventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
        }
        [HttpGet("block")]
        public IActionResult OnBlockNotify(string hash, string cryptoCode)
        {
            _eventAggregator.Publish(new ZanoEvent()
            {
                BlockHash = hash,
                CryptoCode = cryptoCode.ToUpperInvariant()
            });
            return Ok();
        }
        [HttpGet("tx")]
        public IActionResult OnTransactionNotify(string hash, string cryptoCode)
        {
            _eventAggregator.Publish(new ZanoEvent()
            {
                TransactionHash = hash,
                CryptoCode = cryptoCode.ToUpperInvariant()
            });
            return Ok();
        }
    }
}