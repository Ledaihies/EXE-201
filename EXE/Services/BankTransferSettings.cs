namespace EXE.Services;

public class BankTransferSettings
{
    public string BankId { get; set; } = "";
    public string AccountNo { get; set; } = "";
    public string AccountName { get; set; } = "";
    public string Template { get; set; } = "compact2";
    public string PaymentCodePrefix { get; set; } = "DH";
    public string SePayApiKey { get; set; } = "";
    public string SePayWebhookSecret { get; set; } = "";
    public string SePayApiToken { get; set; } = "";
    public string SePayTransactionsUrl { get; set; } = "https://userapi.sepay.vn/v2/transactions";
}
