using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace EXE.Services;

public class SePayTransactionLookupService : ISePayTransactionLookupService
{
    private readonly BankTransferSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly ILogger<SePayTransactionLookupService> _logger;

    public SePayTransactionLookupService(
        IOptions<BankTransferSettings> settings,
        HttpClient httpClient,
        ILogger<SePayTransactionLookupService> logger)
    {
        _settings = settings.Value;
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> HasIncomingTransactionAsync(string paymentCode, decimal amount, DateTime? fromDate = null)
    {
        if (string.IsNullOrWhiteSpace(_settings.SePayApiToken) ||
            string.IsNullOrWhiteSpace(paymentCode) ||
            amount <= 0)
        {
            return false;
        }

        var amountVnd = (long)Math.Round(amount, 0, MidpointRounding.AwayFromZero);
        var url = BuildTransactionsUrl(paymentCode.Trim(), amountVnd, fromDate);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.SePayApiToken.Trim());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("SePay API lookup failed. Status={StatusCode}, PaymentCode={PaymentCode}", (int)response.StatusCode, paymentCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<SePayTransactionsResponse>();
            return result?.Data?.Any(tx => IsMatchingTransaction(tx, paymentCode.Trim(), amountVnd)) == true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SePay API lookup error. PaymentCode={PaymentCode}", paymentCode);
            return false;
        }
    }

    private string BuildTransactionsUrl(string paymentCode, long amountVnd, DateTime? fromDate)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_settings.SePayTransactionsUrl)
            ? "https://userapi.sepay.vn/v2/transactions"
            : _settings.SePayTransactionsUrl.Trim();

        var query = new Dictionary<string, string>
        {
            ["q"] = paymentCode,
            ["transfer_type"] = "in",
            ["amount_in_min"] = amountVnd.ToString(),
            ["amount_in_max"] = amountVnd.ToString(),
            ["per_page"] = "20",
            ["timestamp_format"] = "iso8601"
        };

        if (fromDate.HasValue)
        {
            query["transaction_date_from"] = fromDate.Value.AddDays(-1).ToString("yyyy-MM-dd HH:mm:ss");
        }

        return baseUrl + "?" + string.Join("&", query.Select(kv =>
            Uri.EscapeDataString(kv.Key) + "=" + Uri.EscapeDataString(kv.Value)));
    }

    private static bool IsMatchingTransaction(SePayTransaction tx, string paymentCode, long amountVnd)
    {
        if (!string.Equals(tx.TransferType, "in", StringComparison.OrdinalIgnoreCase) &&
            tx.AmountIn <= 0)
        {
            return false;
        }

        if (tx.AmountIn != amountVnd)
        {
            return false;
        }

        return ContainsPaymentCode(tx.Code, paymentCode) ||
               ContainsPaymentCode(tx.TransactionContent, paymentCode) ||
               ContainsPaymentCode(tx.ReferenceNumber, paymentCode);
    }

    private static bool ContainsPaymentCode(string? value, string paymentCode)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Contains(paymentCode, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class SePayTransactionsResponse
    {
        [JsonPropertyName("data")]
        public List<SePayTransaction>? Data { get; set; }
    }

    private sealed class SePayTransaction
    {
        [JsonPropertyName("transfer_type")]
        public string? TransferType { get; set; }

        [JsonPropertyName("amount_in")]
        public long AmountIn { get; set; }

        [JsonPropertyName("transaction_content")]
        public string? TransactionContent { get; set; }

        [JsonPropertyName("reference_number")]
        public string? ReferenceNumber { get; set; }

        [JsonPropertyName("code")]
        public string? Code { get; set; }
    }
}
