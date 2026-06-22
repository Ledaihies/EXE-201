using EXE.Models;
using EXE.Security;
using EXE.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace EXE.Controllers.Admin;

[AdminOnly]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebsiteVisitService _websiteVisitService;

    public AdminController(ApplicationDbContext context, IWebsiteVisitService websiteVisitService)
    {
        _context = context;
        _websiteVisitService = websiteVisitService;
    }

    public async Task<IActionResult> Index()
    {
        var productCount = await _context.Products.CountAsync();
        var categoryCount = await _context.Categories.CountAsync();
        var orderCount = await _context.Orders.CountAsync();
        var userCount = await _context.Users.CountAsync();

        ViewBag.ProductCount = productCount;
        ViewBag.CategoryCount = categoryCount;
        ViewBag.OrderCount = orderCount;
        ViewBag.UserCount = userCount;

        var visitStats = await _websiteVisitService.GetStatsAsync(DateTime.Today.AddDays(-13), DateTime.Today);
        ViewBag.TotalVisits = visitStats.TotalVisits;
        ViewBag.TodayVisits = visitStats.TodayVisits;
        ViewBag.UniqueVisitors = visitStats.UniqueVisitors;
        ViewBag.ProductViews = visitStats.ProductViews;

        var today = DateTime.Today;
        var fromDate = today.AddDays(-13);
        var allDays = Enumerable.Range(0, 14)
            .Select(i => fromDate.AddDays(i))
            .ToList();

        var labels = allDays.Select(d => d.ToString("dd/MM")).ToList();
        var trafficData = visitStats.DailyVisits;
        var revenueData = await BuildDailyRevenueAsync(fromDate, today.AddDays(1), allDays);

        ViewBag.ChartLabelsJson = JsonSerializer.Serialize(labels);
        ViewBag.TrafficDataJson = JsonSerializer.Serialize(trafficData);
        ViewBag.RevenueDataJson = JsonSerializer.Serialize(revenueData);

        return View();
    }

    private async Task<List<decimal>> BuildDailyRevenueAsync(DateTime fromDate, DateTime toExclusive, List<DateTime> allDays)
    {
        var orders = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Status)
            .Include(o => o.Payments)
                .ThenInclude(p => p.PaymentMethod)
            .Include(o => o.CODCollection)
            .Include(o => o.OrderSettlements)
            .Where(o => o.OrderDate != null &&
                        o.OrderDate >= fromDate &&
                        o.OrderDate < toExclusive)
            .ToListAsync();

        var eligibleByDate = orders
            .Where(IsRevenueEligibleOrder)
            .GroupBy(o => o.OrderDate!.Value.Date)
            .ToDictionary(g => g.Key, g => g.Sum(o => o.TotalAmount ?? 0m));

        return allDays.Select(d => eligibleByDate.TryGetValue(d, out var value) ? value : 0m).ToList();
    }

    private static bool IsRevenueEligibleOrder(Order order)
    {
        var status = NormalizeStatus(order.Status?.StatusName);
        if (IsBlockedStatus(status))
        {
            return false;
        }

        var paymentStatus = NormalizeStatus(order.PaymentStatus);
        var isOnlinePaid = paymentStatus is "paid" or "paidonline" or "success" &&
                           status is "hoanthanh" or "danhanhang" or "completed" or "delivered";
        var isCodPaid = paymentStatus == "codremittedtoadmin" ||
                        order.OrderSettlements.Any(s => IsReadyToSettleStatus(s.SettlementStatus));

        return isOnlinePaid || isCodPaid;
    }

    private static bool IsBlockedStatus(string status)
    {
        return status is "pending" or "incart" or "waitingpayment" or "cancelled" or "returned" or "refunded" or "faileddelivery" or "dahuy" or "dahoantien" or "choxacnhanhoanhang";
    }

    private static bool IsReadyToSettleStatus(string? status)
    {
        var normalized = NormalizeStatus(status);
        return normalized is "readytosettle" or "waitingpayout" or "settled";
    }

    private static string NormalizeStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        normalized = normalized.Replace('đ', 'd');
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (!char.IsWhiteSpace(ch) && ch != '-' && ch != '_')
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }
}
