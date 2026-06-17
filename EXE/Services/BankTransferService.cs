using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using EXE.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace EXE.Services;

public class BankTransferService : IBankTransferService
{
    private readonly BankTransferSettings _settings;

    public BankTransferService(IOptions<BankTransferSettings> settings)
    {
        _settings = settings.Value;
    }

    public string BuildPaymentCode(int orderId)
    {
        var prefix = string.IsNullOrWhiteSpace(_settings.PaymentCodePrefix) ? "DH" : _settings.PaymentCodePrefix.Trim();
        return $"{prefix}{orderId}";
    }

    public string BuildVietQrUrl(Order order)
    {
        var amount = (long)Math.Round(order.TotalAmount ?? 0, 0, MidpointRounding.AwayFromZero);
        return BuildVietQrUrl(amount, BuildPaymentCode(order.OrderId));
    }

    public string BuildVietQrUrl(decimal amount, string paymentCode)
    {
        var amountVnd = (long)Math.Round(amount, 0, MidpointRounding.AwayFromZero);
        var bankId = Uri.EscapeDataString(_settings.BankId.Trim());
        var accountNo = Uri.EscapeDataString(_settings.AccountNo.Trim());
        var template = Uri.EscapeDataString(string.IsNullOrWhiteSpace(_settings.Template) ? "compact2" : _settings.Template.Trim());
        var addInfo = Uri.EscapeDataString(paymentCode);
        var accountName = Uri.EscapeDataString(_settings.AccountName.Trim());

        return $"https://img.vietqr.io/image/{bankId}-{accountNo}-{template}.png?amount={amountVnd}&addInfo={addInfo}&accountName={accountName}";
    }

    public int? ExtractOrderId(string? paymentCode, string? content)
    {
        var prefix = Regex.Escape(string.IsNullOrWhiteSpace(_settings.PaymentCodePrefix) ? "DH" : _settings.PaymentCodePrefix.Trim());
        var pattern = $@"\b{prefix}(?<id>\d+)\b";

        foreach (var value in new[] { paymentCode, content })
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var match = Regex.Match(value, pattern, RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups["id"].Value, out var orderId))
            {
                return orderId;
            }
        }

        return null;
    }

    public int? ExtractAdvertisingRequestId(string? paymentCode, string? content)
    {
        return ExtractId(@"QC(?<id>\d+)", paymentCode, content);
    }

    public int? ExtractWalletTopUpRequestId(string? paymentCode, string? content)
    {
        return ExtractId(@"VI(?<id>\d+)", paymentCode, content);
    }

    private static int? ExtractId(string pattern, params string?[] values)
    {
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var match = Regex.Match(value, $@"\b{pattern}\b", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups["id"].Value, out var id))
            {
                return id;
            }
        }

        return null;
    }

    public async Task<bool> ValidateSePayRequestAsync(HttpRequest request, string rawBody)
    {
        if (!string.IsNullOrWhiteSpace(_settings.SePayApiKey))
        {
            var authorization = request.Headers.Authorization.ToString();
            var expected = "Apikey " + _settings.SePayApiKey;
            if (!FixedTimeEquals(authorization, expected))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(_settings.SePayWebhookSecret))
        {
            var signature = request.Headers["X-SePay-Signature"].ToString();
            var timestamp = request.Headers["X-SePay-Timestamp"].ToString();
            if (!long.TryParse(timestamp, out var unixSeconds))
            {
                return false;
            }

            var requestTime = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            if (DateTimeOffset.UtcNow - requestTime > TimeSpan.FromMinutes(5) ||
                requestTime - DateTimeOffset.UtcNow > TimeSpan.FromMinutes(5))
            {
                return false;
            }

            var expected = "sha256=" + HmacSha256(_settings.SePayWebhookSecret, $"{timestamp}.{rawBody}");
            if (!FixedTimeEquals(signature, expected))
            {
                return false;
            }
        }

        await Task.CompletedTask;
        return true;
    }

    private static bool FixedTimeEquals(string value, string expected)
    {
        var valueBytes = Encoding.UTF8.GetBytes(value);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return valueBytes.Length == expectedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(valueBytes, expectedBytes);
    }

    private static string HmacSha256(string key, string inputData)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var inputBytes = Encoding.UTF8.GetBytes(inputData);
        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(inputBytes);
        var sb = new StringBuilder(hashBytes.Length * 2);
        foreach (var b in hashBytes)
        {
            sb.AppendFormat("{0:x2}", b);
        }

        return sb.ToString();
    }
}
