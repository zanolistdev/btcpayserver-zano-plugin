using BTCPayServer.Plugins.Zano.Configuration;

using Xunit;

namespace BTCPayServer.Plugins.UnitTests.Zano.Configuration
{
    public class ZanoLikeConfigurationTests
    {
        [Trait("Category", "Unit")]
        [Fact]
        public void ZanoLikeConfiguration_ShouldInitializeWithEmptyDictionary()
        {
            var config = new ZanoLikeConfiguration();

            Assert.NotNull(config.ZanoLikeConfigurationItems);
            Assert.Empty(config.ZanoLikeConfigurationItems);
        }

        [Trait("Category", "Unit")]
        [Fact]
        public void ZanoLikeConfigurationItem_ShouldSetAndGetProperties()
        {
            var configItem = new ZanoLikeConfigurationItem
            {
                DaemonRpcUri = new Uri("http://localhost:18081"),
                InternalWalletRpcUri = new Uri("http://localhost:18082"),
                WalletDirectory = "/wallets",
                Username = "user",
                Password = "password"
            };

            Assert.Equal("http://localhost:18081/", configItem.DaemonRpcUri.ToString());
            Assert.Equal("http://localhost:18082/", configItem.InternalWalletRpcUri.ToString());
            Assert.Equal("/wallets", configItem.WalletDirectory);
            Assert.Equal("user", configItem.Username);
            Assert.Equal("password", configItem.Password);
        }

        [Trait("Category", "Unit")]
        [Fact]
        public void ZanoLikeConfiguration_ShouldAddAndRetrieveItems()
        {
            var config = new ZanoLikeConfiguration();
            var configItem = new ZanoLikeConfigurationItem
            {
                DaemonRpcUri = new Uri("http://localhost:18081"),
                InternalWalletRpcUri = new Uri("http://localhost:18082"),
                WalletDirectory = "/wallets",
                Username = "user",
                Password = "password"
            };

            config.ZanoLikeConfigurationItems.Add("XMR", configItem);

            Assert.Single(config.ZanoLikeConfigurationItems);
            Assert.True(config.ZanoLikeConfigurationItems.ContainsKey("XMR"));
            Assert.Equal(configItem, config.ZanoLikeConfigurationItems["XMR"]);
        }

        [Trait("Category", "Unit")]
        [Fact]
        public void ZanoLikeConfiguration_ShouldHandleDuplicateKeys()
        {
            var config = new ZanoLikeConfiguration();
            var configItem1 = new ZanoLikeConfigurationItem
            {
                DaemonRpcUri = new Uri("http://localhost:18081")
            };
            var configItem2 = new ZanoLikeConfigurationItem
            {
                DaemonRpcUri = new Uri("http://localhost:18082")
            };

            config.ZanoLikeConfigurationItems.Add("XMR", configItem1);

            Assert.Throws<ArgumentException>(() =>
                config.ZanoLikeConfigurationItems.Add("XMR", configItem2));
        }
    }
}