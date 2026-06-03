using EXE.Models;
using Microsoft.EntityFrameworkCore;

namespace EXE.Services;

public class WalletService : IWalletService
{
    private readonly ApplicationDbContext _context;

    public WalletService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Wallet> GetOrCreateWalletAsync(int userId)
    {
        var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
        if (wallet != null) return wallet;

        wallet = new Wallet
        {
            UserId = userId,
            Balance = 0,
            CreatedDate = DateTime.Now,
            UpdatedDate = DateTime.Now
        };
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();
        return wallet;
    }

    public async Task CreditAsync(int userId, decimal amount, string type, string description, int? orderId = null)
    {
        if (amount <= 0) return;

        var wallet = await GetOrCreateWalletAsync(userId);
        wallet.Balance += amount;
        wallet.UpdatedDate = DateTime.Now;
        _context.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = wallet.WalletId,
            OrderId = orderId,
            Type = type,
            Amount = amount,
            BalanceAfter = wallet.Balance,
            Description = description,
            CreatedDate = DateTime.Now
        });
        await _context.SaveChangesAsync();
    }
}
