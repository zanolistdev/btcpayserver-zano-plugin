namespace BTCPayServer.Plugins.Zano.Payments
{
    public class ZanoPaymentPromptDetails
    {
        public long AccountAddress { get; set; }
        public long? InvoiceSettledConfirmationThreshold { get; set; }
    }
}