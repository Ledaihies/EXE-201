namespace EXE.Services;

public interface ISePayTransactionLookupService
{
    Task<bool> HasIncomingTransactionAsync(string paymentCode, decimal amount, DateTime? fromDate = null);
}
