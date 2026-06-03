using EXE.Models;
using EXE.Security;
using EXE.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EXE.Controllers.Admin;

[AdminOnly]
public class RevenueAdminController : Controller
{
    private const decimal AdminCommissionRate = 0.15m;
    private readonly ApplicationDbContext _context;

    public RevenueAdminController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(DateTime? from, DateTime? to, string? mode, int? month, int? year)
    {
        mode = string.IsNullOrWhiteSpace(mode) ? "day" : mode.Trim().ToLowerInvariant();
        var today = DateTime.Today;
        DateTime fromDate;
        DateTime toExclusive;

        if (mode == "month")
        {
            var selectedYear = year.GetValueOrDefault(today.Year);
            var selectedMonth = Math.Clamp(month.GetValueOrDefault(today.Month), 1, 12);
            fromDate = new DateTime(selectedYear, selectedMonth, 1);
            toExclusive = fromDate.AddMonths(1);
            from = fromDate;
            to = toExclusive.AddDays(-1);
            month = selectedMonth;
            year = selectedYear;
        }
        else if (mode == "range")
        {
            fromDate = (from ?? today).Date;
            toExclusive = (to ?? fromDate).Date.AddDays(1);
        }
        else
        {
            fromDate = (from ?? today).Date;
            toExclusive = fromDate.AddDays(1);
            from = fromDate;
            to = fromDate;
            mode = "day";
        }

        var itemsQuery = _context.OrderItems
            .Include(oi => oi.Order)
                .ThenInclude(o => o!.Status)
            .Include(oi => oi.Product)
                .ThenInclude(p => p!.Seller)
            .Where(oi => oi.Order != null &&
                         (oi.Order.Status == null ||
                          oi.Order.Status.StatusName != "Da huy") &&
                         oi.Product != null)
            .AsQueryable();

        itemsQuery = itemsQuery.Where(oi => oi.Order!.OrderDate >= fromDate &&
                                            oi.Order!.OrderDate < toExclusive);

        var soldItems = await itemsQuery.ToListAsync();

        var dashboardItems = soldItems
            .GroupBy(oi => new
            {
                ProductId = oi.ProductId ?? 0,
                ProductName = oi.Product!.ProductName ?? $"SP #{oi.ProductId}",
                SellerId = oi.Product.SellerId,
                SellerName = oi.Product.Seller != null
                    ? (oi.Product.Seller.FullName ?? oi.Product.Seller.Email ?? $"Seller #{oi.Product.SellerId}")
                    : "Chưa có người bán"
            })
            .Select(g =>
            {
                var gross = g.Sum(oi => (oi.Price ?? 0m) * (oi.Quantity ?? 0));
                var commission = Math.Round(gross * AdminCommissionRate, 0, MidpointRounding.AwayFromZero);
                return new CommissionDashboardItem
                {
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.ProductName,
                    SellerId = g.Key.SellerId,
                    SellerName = g.Key.SellerName,
                    SellerBankName = g.FirstOrDefault()?.Product?.Seller?.BankName,
                    SellerBankAccountNumber = g.FirstOrDefault()?.Product?.Seller?.BankAccountNumber,
                    SellerBankAccountName = g.FirstOrDefault()?.Product?.Seller?.BankAccountName,
                    SellerBankBranch = g.FirstOrDefault()?.Product?.Seller?.BankBranch,
                    QuantitySold = g.Sum(oi => oi.Quantity ?? 0),
                    PaidOrderCount = g.Select(oi => oi.OrderId).Distinct().Count(),
                    GrossRevenue = gross,
                    AdminCommission = commission,
                    SellerPayout = gross - commission
                };
            })
            .OrderByDescending(x => x.GrossRevenue)
            .ToList();

        var totalGrossRevenue = dashboardItems.Sum(x => x.GrossRevenue);
        var totalAdminCommission = dashboardItems.Sum(x => x.AdminCommission);
        var advertisingRevenue = await _context.AdvertisingPaymentRequests
            .Where(r => r.Status == "Paid" &&
                        r.ConfirmedDate >= fromDate &&
                        r.ConfirmedDate < toExclusive)
            .SumAsync(r => (decimal?)r.Amount) ?? 0m;
        var walletTopUpRevenue = await _context.WalletTopUpRequests
            .Where(r => r.Status == "Paid" &&
                        r.ConfirmedDate >= fromDate &&
                        r.ConfirmedDate < toExclusive)
            .SumAsync(r => (decimal?)r.Amount) ?? 0m;
        var shippingRefunds = await _context.WalletTransactions
            .Where(t => t.Type == "ShippingRefund" &&
                        t.CreatedDate >= fromDate &&
                        t.CreatedDate < toExclusive)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;

        var shippingRefundTransactions = await _context.WalletTransactions
            .Include(t => t.Wallet)
                .ThenInclude(w => w!.User)
            .Where(t => t.Type == "ShippingRefund" &&
                        t.CreatedDate >= fromDate &&
                        t.CreatedDate < toExclusive)
            .ToListAsync();

        var productPayoutBySeller = dashboardItems
            .Where(x => x.SellerId.HasValue)
            .GroupBy(x => x.SellerId!.Value)
            .Select(g =>
            {
                var first = g.First();
                return new CommissionDashboardItem
                {
                    SellerId = first.SellerId,
                    SellerName = first.SellerName,
                    SellerBankName = first.SellerBankName,
                    SellerBankAccountNumber = first.SellerBankAccountNumber,
                    SellerBankAccountName = first.SellerBankAccountName,
                    SellerBankBranch = first.SellerBankBranch,
                    QuantitySold = g.Sum(x => x.QuantitySold),
                    PaidOrderCount = g.Sum(x => x.PaidOrderCount),
                    GrossRevenue = g.Sum(x => x.GrossRevenue),
                    AdminCommission = g.Sum(x => x.AdminCommission),
                    SellerPayout = g.Sum(x => x.SellerPayout)
                };
            });

        var shippingRefundBySeller = shippingRefundTransactions
            .Where(t => t.Wallet != null)
            .GroupBy(t => t.Wallet!.UserId)
            .Select(g =>
            {
                var user = g.First().Wallet?.User;
                return new CommissionDashboardItem
                {
                    SellerId = g.Key,
                    SellerName = user?.FullName ?? user?.Email ?? $"Seller #{g.Key}",
                    SellerBankName = user?.BankName,
                    SellerBankAccountNumber = user?.BankAccountNumber,
                    SellerBankAccountName = user?.BankAccountName,
                    SellerBankBranch = user?.BankBranch,
                    ShippingRefundAmount = g.Sum(t => t.Amount),
                    SellerPayout = g.Sum(t => t.Amount)
                };
            });

        var sellerPayoutSummaries = productPayoutBySeller
            .Concat(shippingRefundBySeller)
            .GroupBy(x => x.SellerId ?? 0)
            .Select(g =>
            {
                var first = g.FirstOrDefault(x =>
                    !string.IsNullOrWhiteSpace(x.SellerBankName) ||
                    !string.IsNullOrWhiteSpace(x.SellerBankAccountNumber) ||
                    !string.IsNullOrWhiteSpace(x.SellerBankAccountName)) ?? g.First();

                return new CommissionDashboardItem
                {
                    SellerId = first.SellerId,
                    SellerName = first.SellerName,
                    SellerBankName = first.SellerBankName,
                    SellerBankAccountNumber = first.SellerBankAccountNumber,
                    SellerBankAccountName = first.SellerBankAccountName,
                    SellerBankBranch = first.SellerBankBranch,
                    QuantitySold = g.Sum(x => x.QuantitySold),
                    PaidOrderCount = g.Sum(x => x.PaidOrderCount),
                    GrossRevenue = g.Sum(x => x.GrossRevenue),
                    AdminCommission = g.Sum(x => x.AdminCommission),
                    ShippingRefundAmount = g.Sum(x => x.ShippingRefundAmount),
                    SellerPayout = g.Sum(x => x.SellerPayout)
                };
            })
            .OrderByDescending(x => x.SellerPayout)
            .ToList();

        ViewBag.TotalGrossRevenue = totalGrossRevenue;
        ViewBag.TotalAdminCommission = totalAdminCommission;
        ViewBag.TotalAdvertisingRevenue = advertisingRevenue;
        ViewBag.TotalWalletTopUpRevenue = walletTopUpRevenue;
        ViewBag.TotalShippingRefunds = shippingRefunds;
        ViewBag.TotalAdminRevenue = totalAdminCommission + advertisingRevenue + walletTopUpRevenue - shippingRefunds;
        ViewBag.TotalSellerPayout = dashboardItems.Sum(x => x.SellerPayout) + shippingRefunds;
        ViewBag.TotalQuantitySold = dashboardItems.Sum(x => x.QuantitySold);
        ViewBag.CommissionRate = AdminCommissionRate;
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        ViewBag.Mode = mode;
        ViewBag.Month = month ?? today.Month;
        ViewBag.Year = year ?? today.Year;
        ViewBag.SellerPayoutSummaries = sellerPayoutSummaries;

        return View("~/Views/Admin/RevenueAdmin/Index.cshtml", dashboardItems);
    }
}
