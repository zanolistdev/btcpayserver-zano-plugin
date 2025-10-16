namespace BTCPayServer.Plugins.Zano;

public class ZanoLikeSpecificBtcPayNetwork : BTCPayNetworkBase
{
    public int MaxTrackedConfirmation = 10;
    public string UriScheme { get; set; }
}