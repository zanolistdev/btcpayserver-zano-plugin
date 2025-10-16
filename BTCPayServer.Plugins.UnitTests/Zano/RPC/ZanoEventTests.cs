using BTCPayServer.Plugins.Zano.RPC;

using Xunit;

namespace BTCPayServer.Plugins.UnitTests.Zano.RPC
{
    public class ZanoEventTest
    {
        [Fact]
        public void DefaultInitialization_ShouldHaveNullProperties()
        {
            var zanoEvent = new ZanoEvent();

            Assert.Null(zanoEvent.BlockHash);
            Assert.Null(zanoEvent.TransactionHash);
            Assert.Null(zanoEvent.CryptoCode);
        }

        [Fact]
        public void PropertyAssignment_ShouldSetAndRetrieveValues()
        {
            var zanoEvent = new ZanoEvent
            {
                BlockHash = "block123",
                TransactionHash = "tx456",
                CryptoCode = "XMR"
            };

            Assert.Equal("block123", zanoEvent.BlockHash);
            Assert.Equal("tx456", zanoEvent.TransactionHash);
            Assert.Equal("XMR", zanoEvent.CryptoCode);
        }

        [Theory]
        [InlineData("block123", "tx456", "XMR", "XMR: Tx Update New Block (tx456block123)")]
        public void ToString_ShouldReturnCorrectString(string blockHash, string transactionHash, string cryptoCode, string expected)
        {
            var zanoEvent = new ZanoEvent
            {
                BlockHash = blockHash,
                TransactionHash = transactionHash,
                CryptoCode = cryptoCode
            };

            var result = zanoEvent.ToString();

            Assert.Equal(expected, result);
        }


    }
}