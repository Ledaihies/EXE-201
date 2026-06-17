using EXE.Models;
using EXE.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EXE.Controllers.Admin;

[AdminOnly]
public class AccountAdminController : Controller
{
    private readonly ApplicationDbContext _context;

    public AccountAdminController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Sellers()
    {
        var rows = await BuildAccountRows("Seller");
        ViewData["Title"] = "Quản lý người bán";
        ViewBag.AccountType = "Seller";
        ViewBag.Heading = "Quản lý tài khoản người bán";
        ViewBag.Description = "Danh sách toàn bộ người bán, số sản phẩm, đơn hàng và thông tin ngân hàng.";
        return View("Accounts", rows);
    }

    public async Task<IActionResult> Buyers()
    {
        var rows = await BuildAccountRows("User");
        ViewData["Title"] = "Quản lý người mua";
        ViewBag.AccountType = "User";
        ViewBag.Heading = "Quản lý tài khoản người mua";
        ViewBag.Description = "Danh sách toàn bộ người mua, đơn hàng, đánh giá và thông tin liên hệ.";
        return View("Accounts", rows);
    }

    public async Task<IActionResult> Details(int id, string? returnTo)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null) return NotFound();

        var isSeller = string.Equals(user.Role?.RoleName, "Seller", StringComparison.OrdinalIgnoreCase);
        ViewBag.ProductCount = isSeller
            ? await _context.Products.CountAsync(p => p.SellerId == id)
            : 0;
        ViewBag.OrderCount = isSeller
            ? await _context.OrderItems
                .Where(oi => oi.Product != null && oi.Product.SellerId == id)
                .Select(oi => oi.OrderId)
                .Distinct()
                .CountAsync()
            : await _context.Orders.CountAsync(o => o.UserId == id);
        ViewBag.OrderTotal = isSeller
            ? await _context.OrderItems
                .Where(oi => oi.Product != null && oi.Product.SellerId == id)
                .SumAsync(oi => (decimal?)((oi.Price ?? 0) * (oi.Quantity ?? 0))) ?? 0m
            : await _context.Orders
                .Where(o => o.UserId == id)
                .SumAsync(o => (decimal?)(o.TotalAmount ?? 0)) ?? 0m;
        ViewBag.ReviewCount = await _context.Reviews.CountAsync(r => r.UserId == id);

        ViewData["Title"] = "Chi tiết tài khoản";
        ViewBag.ReturnTo = NormalizeReturnTo(returnTo, user.Role?.RoleName);
        return View(user);
    }

    public IActionResult Edit(int id, string? returnTo)
    {
        return RedirectToAction(nameof(Details), new { id, returnTo });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int userId, string? fullName, string? email, string? phone, string? address,
        string? bankName, string? bankAccountNumber, string? bankAccountName, string? bankBranch, string? returnTo)
    {
        await Task.CompletedTask;
        return Forbid();
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, string? returnTo)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == id);

        if (user == null) return NotFound();

        var target = NormalizeReturnTo(returnTo, user.Role?.RoleName);
        if (await HasBlockingData(id, user.Role?.RoleName))
        {
            TempData["AccountAdminError"] = "Tài khoản đã có dữ liệu giao dịch/sản phẩm nên không thể xóa cứng.";
            return Redirect(target);
        }

        var carts = await _context.Carts.Include(c => c.CartItems).Where(c => c.UserId == id).ToListAsync();
        _context.CartItems.RemoveRange(carts.SelectMany(c => c.CartItems));
        _context.Carts.RemoveRange(carts);

        var wishlists = await _context.Wishlists.Where(w => w.UserId == id).ToListAsync();
        var wallets = await _context.Wallets.Include(w => w.Transactions).Where(w => w.UserId == id).ToListAsync();
        var topUps = await _context.WalletTopUpRequests.Where(r => r.UserId == id).ToListAsync();
        var auditLogs = await _context.AuditLogs.Where(a => a.UserId == id).ToListAsync();

        _context.Wishlists.RemoveRange(wishlists);
        _context.WalletTransactions.RemoveRange(wallets.SelectMany(w => w.Transactions));
        _context.Wallets.RemoveRange(wallets);
        _context.WalletTopUpRequests.RemoveRange(topUps);
        _context.AuditLogs.RemoveRange(auditLogs);
        _context.Users.Remove(user);

        await _context.SaveChangesAsync();
        TempData["AccountAdminMessage"] = "Đã xóa tài khoản.";
        return Redirect(target);
    }

    private async Task<List<AdminAccountRow>> BuildAccountRows(string roleName)
    {
        var users = await _context.Users
            .Include(u => u.Role)
            .Where(u => u.Role != null && u.Role.RoleName == roleName)
            .OrderByDescending(u => u.CreatedDate)
            .ToListAsync();

        var ids = users.Select(u => u.UserId).ToList();
        var productCounts = await _context.Products
            .Where(p => p.SellerId.HasValue && ids.Contains(p.SellerId.Value))
            .GroupBy(p => p.SellerId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);

        var orderCounts = await _context.Orders
            .Where(o => o.UserId.HasValue && ids.Contains(o.UserId.Value))
            .GroupBy(o => o.UserId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count(), Total = g.Sum(o => o.TotalAmount ?? 0) })
            .ToDictionaryAsync(x => x.UserId, x => new AccountOrderStats(x.Count, x.Total));

        var sellerOrderCounts = await _context.OrderItems
            .Where(oi => oi.Product != null && oi.Product.SellerId.HasValue && ids.Contains(oi.Product.SellerId.Value))
            .GroupBy(oi => oi.Product!.SellerId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Select(x => x.OrderId).Distinct().Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);

        var reviewCounts = await _context.Reviews
            .Where(r => r.UserId.HasValue && ids.Contains(r.UserId.Value))
            .GroupBy(r => r.UserId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);

        return users.Select(u =>
        {
            var buyerOrders = orderCounts.GetValueOrDefault(u.UserId) ?? new AccountOrderStats(0, 0m);
            return new AdminAccountRow(
                u,
                productCounts.GetValueOrDefault(u.UserId),
                string.Equals(roleName, "Seller", StringComparison.OrdinalIgnoreCase)
                    ? sellerOrderCounts.GetValueOrDefault(u.UserId)
                    : buyerOrders.Count,
                buyerOrders.Total,
                reviewCounts.GetValueOrDefault(u.UserId));
        }).ToList();
    }

    private async Task<bool> HasBlockingData(int userId, string? roleName)
    {
        if (string.Equals(roleName, "Seller", StringComparison.OrdinalIgnoreCase))
        {
            return await _context.Products.AnyAsync(p => p.SellerId == userId)
                || await _context.AdvertisingPaymentRequests.AnyAsync(r => r.SellerId == userId)
                || await _context.OrderItems.AnyAsync(oi => oi.Product != null && oi.Product.SellerId == userId);
        }

        return await _context.Orders.AnyAsync(o => o.UserId == userId)
            || await _context.Reviews.AnyAsync(r => r.UserId == userId)
            || await _context.OrderReturnRequests.AnyAsync(r => r.UserId == userId || r.SellerConfirmedByUserId == userId);
    }

    private static string NormalizeReturnTo(string? returnTo, string? roleName)
    {
        if (returnTo == "/AccountAdmin/Sellers" || returnTo == "/AccountAdmin/Buyers")
        {
            return returnTo;
        }

        return string.Equals(roleName, "Seller", StringComparison.OrdinalIgnoreCase)
            ? "/AccountAdmin/Sellers"
            : "/AccountAdmin/Buyers";
    }

    public sealed record AdminAccountRow(User User, int ProductCount, int OrderCount, decimal OrderTotal, int ReviewCount);
    private sealed record AccountOrderStats(int Count, decimal Total);
}
