using EXE.Models;
using Microsoft.AspNetCore.Http;

namespace EXE.Services;

public interface IBankTransferService
{
    string BuildPaymentCode(int orderId);
    string BuildVietQrUrl(decimal amount, string paymentCode);
    string BuildVietQrUrl(Order order);
    int? ExtractOrderId(string? paymentCode, string? content);
    int? ExtractAdvertisingRequestId(string? paymentCode, string? content);
    int? ExtractWalletTopUpRequestId(string? paymentCode, string? content);
    Task<bool> ValidateSePayRequestAsync(HttpRequest request, string rawBody);
}
