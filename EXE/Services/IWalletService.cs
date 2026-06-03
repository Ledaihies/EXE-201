using EXE.Models;

namespace EXE.Services;

public interface IWalletService
{
    Task<Wallet> GetOrCreateWalletAsync(int userId);
    Task CreditAsync(int userId, decimal amount, string type, string description, int? orderId = null);
}
