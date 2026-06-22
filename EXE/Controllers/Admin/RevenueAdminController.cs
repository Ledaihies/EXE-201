using EXE.Models;
using EXE.Security;
using EXE.Services;
using EXE.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace EXE.Controllers.Admin;

[AdminOnly]
    public class RevenueAdminController : Controller
    {
        private const decimal AdminCommissionRate = 0.15m;
        private const decimal CodFeeRate = 0m;
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

    public RevenueAdminController(ApplicationDbContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
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
            .Include(oi => oi.Order)
                .ThenInclude(o => o!.Payments)
                    .ThenInclude(p => p.PaymentMethod)
            .Include(oi => oi.Order)
                .ThenInclude(o => o!.CODCollection)
            .Include(oi => oi.Order)
                .ThenInclude(o => o!.OrderSettlements)
            .Include(oi => oi.Product)
                .ThenInclude(p => p!.Seller)
            .Where(oi => oi.Order != null && oi.Product != null)
            .AsQueryable();

        itemsQuery = itemsQuery.Where(oi => oi.Order!.OrderDate >= fromDate &&
                                            oi.Order!.OrderDate < toExclusive);

        var soldItems = await itemsQuery.ToListAsync();
        soldItems = soldItems
            .Where(oi => IsRevenueEligibleOrder(oi.Order))
            .ToList();

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
        var onlineRevenue = soldItems
            .Where(oi => IsOnlinePaidOrder(oi.Order))
            .Sum(oi => (oi.Price ?? 0m) * (oi.Quantity ?? 0));
        var codRemittedRevenue = await _context.CODCollections
            .Where(c => c.Status == "RemittedToAdmin" &&
                        c.RemittedToAdminAt >= fromDate &&
                        c.RemittedToAdminAt < toExclusive)
            .SumAsync(c => (decimal?)c.Amount) ?? 0m;
        var codPendingCollection = await _context.CODCollections
            .Where(c => c.Status == "PendingCollection")
            .SumAsync(c => (decimal?)c.Amount) ?? 0m;
        var codCashInTransit = await _context.CODCollections
            .Where(c => c.Status == "CashInTransit")
            .SumAsync(c => (decimal?)c.Amount) ?? 0m;
        var codRemittedTotal = await _context.CODCollections
            .Where(c => c.Status == "RemittedToAdmin")
            .SumAsync(c => (decimal?)c.Amount) ?? 0m;
        var totalSellerPayable = await _context.OrderSettlements
            .Where(s => s.SettlementStatus == "ReadyToSettle" || s.SettlementStatus == "WaitingPayout")
            .SumAsync(s => (decimal?)s.SellerReceivable) ?? 0m;
        var totalSellerPaid = await _context.SellerPayouts
            .Where(p => p.Status == "Paid")
            .SumAsync(p => (decimal?)p.TotalAmount) ?? 0m;
        var codSettlements = await _context.OrderSettlements
            .Where(s => s.PaymentMethod == "COD" &&
                        s.CreatedAt >= fromDate &&
                        s.CreatedAt < toExclusive)
            .ToListAsync();
        var totalPlatformFee = totalAdminCommission + codSettlements.Sum(s => s.CodFee);
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
        ViewBag.TotalOnlineRevenue = onlineRevenue;
        ViewBag.TotalCODRemittedRevenue = codRemittedRevenue;
        ViewBag.CODPendingCollection = codPendingCollection;
        ViewBag.CODCashInTransit = codCashInTransit;
        ViewBag.CODRemittedTotal = codRemittedTotal;
        ViewBag.TotalPlatformFee = totalPlatformFee;
        ViewBag.TotalSellerPayable = totalSellerPayable;
        ViewBag.TotalSellerPaid = totalSellerPaid;
        ViewBag.TotalAdvertisingRevenue = advertisingRevenue;
        ViewBag.TotalWalletTopUpRevenue = walletTopUpRevenue;
        ViewBag.TotalShippingRefunds = shippingRefunds;
        ViewBag.TotalAdminRevenue = totalPlatformFee + advertisingRevenue + walletTopUpRevenue - shippingRefunds;
        ViewBag.TotalSellerPayout = dashboardItems.Sum(x => x.SellerPayout) + shippingRefunds;
        ViewBag.TotalQuantitySold = dashboardItems.Sum(x => x.QuantitySold);
        ViewBag.CommissionRate = AdminCommissionRate;
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        ViewBag.Mode = mode;
        ViewBag.Month = month ?? today.Month;
        ViewBag.Year = year ?? today.Year;
        ViewBag.SellerPayoutSummaries = sellerPayoutSummaries;
        ViewBag.CODCollections = await _context.CODCollections
            .Include(c => c.Order)
                .ThenInclude(o => o.Status)
            .Include(c => c.Order)
                .ThenInclude(o => o.User)
            .Where(c => (c.CreatedAt >= fromDate && c.CreatedAt < toExclusive) ||
                        (c.RemittedToAdminAt >= fromDate && c.RemittedToAdminAt < toExclusive))
            .OrderByDescending(c => c.CreatedAt)
            .Take(50)
            .ToListAsync();
        ViewBag.ReadySettlements = await _context.OrderSettlements
            .Include(s => s.Seller)
            .Include(s => s.Order)
            .Where(s => s.SettlementStatus == "ReadyToSettle" || s.SettlementStatus == "WaitingPayout")
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return View("~/Views/Admin/RevenueAdmin/Index.cshtml", dashboardItems);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmCODRemitted(int id, string? note)
    {
        var adminId = HttpContext.Session.GetInt32("UserId");
        var cod = await _context.CODCollections
            .Include(c => c.Order)
                .ThenInclude(o => o.Status)
            .Include(c => c.Order)
                .ThenInclude(o => o.Payments)
                    .ThenInclude(p => p.PaymentMethod)
            .Include(c => c.Order)
                .ThenInclude(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
            .Include(c => c.Order)
                .ThenInclude(o => o.OrderSettlements)
            .FirstOrDefaultAsync(c => c.CODCollectionId == id);

        if (cod == null) return NotFound();

        var orderStatus = cod.Order.Status?.StatusName ?? string.Empty;
        if (IsBlockedOrderStatus(orderStatus))
        {
            TempData["RevenueMessage"] = "Đơn hàng đã hủy/hoàn/trả nên không thể đối soát COD.";
            return RedirectToAction(nameof(Index));
        }

        if (!string.Equals(cod.Status, "CollectedFromCustomer", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(cod.Status, "RemittedToAdmin", StringComparison.OrdinalIgnoreCase))
        {
            TempData["RevenueMessage"] = "COD chưa thu từ khách nên không thể xác nhận tiền về admin.";
            return RedirectToAction(nameof(Index));
        }

        cod.Status = "RemittedToAdmin";
        cod.RemittedToAdminAt ??= DateTime.Now;
        cod.ConfirmedByAdminId = adminId;
        cod.Note = string.IsNullOrWhiteSpace(note) ? cod.Note : note.Trim();
        cod.Order.PaymentStatus = "CODRemittedToAdmin";

        var codPayment = cod.Order.Payments.FirstOrDefault(p => p.PaymentMethod?.MethodName == "COD");
        if (codPayment != null)
        {
            codPayment.Amount = cod.Amount;
            codPayment.PaymentDate ??= DateTime.Now;
        }

        if (!cod.Order.OrderSettlements.Any())
        {
            foreach (var sellerGroup in cod.Order.OrderItems
                .Where(oi => oi.Product != null && oi.Product.SellerId.HasValue)
                .GroupBy(oi => oi.Product!.SellerId!.Value))
            {
                var gross = sellerGroup.Sum(oi => (oi.Price ?? 0m) * (oi.Quantity ?? 0));
                if (gross <= 0) continue;

                var shippingFee = AllocateByGross(cod.Order.ShippingFee ?? 0m, gross, cod.Order.OrderItems.Sum(oi => (oi.Price ?? 0m) * (oi.Quantity ?? 0)));
                var platformFee = Math.Round(gross * AdminCommissionRate, 0, MidpointRounding.AwayFromZero);
                var codFee = Math.Round(gross * CodFeeRate, 0, MidpointRounding.AwayFromZero);
                _context.OrderSettlements.Add(new OrderSettlement
                {
                    OrderId = cod.OrderId,
                    SellerId = sellerGroup.Key,
                    GrossAmount = gross,
                    PlatformFee = platformFee,
                    ShippingFee = shippingFee,
                    CodFee = codFee,
                    SellerReceivable = gross - platformFee - shippingFee - codFee,
                    AdminRevenue = platformFee + codFee,
                    PaymentMethod = "COD",
                    SettlementStatus = "ReadyToSettle",
                    CreatedAt = DateTime.Now
                });
            }
        }

        await _context.SaveChangesAsync();

        var codSellerIds = cod.Order.OrderItems
            .Where(oi => oi.Product != null && oi.Product.SellerId.HasValue)
            .Select(oi => oi.Product.SellerId!.Value)
            .Distinct()
            .ToList();

        if (cod.Order.UserId.HasValue)
        {
            await _notificationService.CreateNotification(
                cod.Order.UserId.Value,
                "COD đã được xác nhận",
                $"Đơn hàng #{cod.OrderId} đã được Admin xác nhận tiền COD về.",
                "COD",
                "Order",
                cod.OrderId);
        }

        foreach (var sellerId in codSellerIds)
        {
            await _notificationService.CreateNotification(
                sellerId,
                "COD đã về Admin",
                $"Đơn hàng #{cod.OrderId} đã được xác nhận COD về Admin và đang chờ payout.",
                "COD",
                "Order",
                cod.OrderId);
        }

        await _notificationService.CreateNotificationForRole(
            RoleAccess.Admin,
            "COD đã về Admin",
            $"Đơn hàng #{cod.OrderId} COD đã được xác nhận về Admin.",
            "COD",
            "Order",
            cod.OrderId);
        TempData["RevenueMessage"] = "Đã xác nhận COD về admin và tạo đối soát cho đơn hàng.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSellerPayout(int sellerId, string? payoutMethod, string? note)
    {
        var adminId = HttpContext.Session.GetInt32("UserId");
        var settlements = await _context.OrderSettlements
            .Include(s => s.Order)
                .ThenInclude(o => o.Status)
            .Include(s => s.Order)
                .ThenInclude(o => o.CODCollection)
            .Where(s => s.SellerId == sellerId && (s.SettlementStatus == "ReadyToSettle" || s.SettlementStatus == "WaitingPayout"))
            .ToListAsync();

        settlements = settlements
            .Where(s => s.Order.CODCollection?.Status == "RemittedToAdmin" &&
                        !IsBlockedOrderStatus(s.Order.Status?.StatusName))
            .ToList();

        if (!settlements.Any())
        {
            TempData["RevenueMessage"] = "Không có đối soát hợp lệ để payout cho seller này.";
            return RedirectToAction(nameof(Index));
        }

        var payout = new SellerPayout
        {
            SellerId = sellerId,
            TotalAmount = settlements.Sum(s => s.SellerReceivable),
            Status = "Paid",
            PayoutMethod = string.IsNullOrWhiteSpace(payoutMethod) ? "BankTransfer" : payoutMethod.Trim(),
            PaidAt = DateTime.Now,
            CreatedByAdminId = adminId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedAt = DateTime.Now
        };
        _context.SellerPayouts.Add(payout);

        foreach (var settlement in settlements)
        {
            settlement.SettlementStatus = "Settled";
            settlement.SettledAt = DateTime.Now;
            _context.SellerPayoutItems.Add(new SellerPayoutItem
            {
                SellerPayout = payout,
                OrderSettlementId = settlement.OrderSettlementId,
                OrderId = settlement.OrderId,
                Amount = settlement.SellerReceivable
            });
        }

        await _context.SaveChangesAsync();

        await _notificationService.CreateNotification(
            sellerId,
            "Đã payout cho seller",
            $"Admin đã ghi nhận payout số tiền {payout.TotalAmount:N0}đ cho shop của bạn.",
            "Payout",
            "SellerPayout",
            payout.SellerPayoutId);

        await _notificationService.CreateNotificationForRole(
            RoleAccess.Admin,
            "Payout đã tạo",
            $"Đã ghi nhận payout cho seller #{sellerId} với tổng tiền {payout.TotalAmount:N0}đ.",
            "Payout",
            "SellerPayout",
            payout.SellerPayoutId);

        TempData["RevenueMessage"] = "Đã ghi nhận payout paid cho seller.";
        return RedirectToAction(nameof(Index));
    }

    private static decimal AllocateByGross(decimal total, decimal sellerGross, decimal orderGross)
    {
        if (total <= 0 || sellerGross <= 0 || orderGross <= 0) return 0m;
        return orderGross == sellerGross ? total : Math.Round(total * sellerGross / orderGross, 0, MidpointRounding.AwayFromZero);
    }

    private static bool IsRevenueEligibleOrder(Order? order)
    {
        if (order == null) return false;

        var status = NormalizeStatus(order.Status?.StatusName);
        if (IsBlockedOrderStatus(status))
        {
            return false;
        }

        return IsOnlinePaidOrder(order) || IsCodRemittedOrder(order);
    }

    private static bool IsOnlinePaidOrder(Order? order)
    {
        if (order == null) return false;

        var paymentStatus = NormalizeStatus(order.PaymentStatus);
        var status = NormalizeStatus(order.Status?.StatusName);
        if (IsBlockedOrderStatus(status))
        {
            return false;
        }

        var hasValidPayment = order.Payments.Any(p =>
            (p.Amount ?? 0m) > 0m &&
            !string.Equals(NormalizeStatus(p.PaymentMethod?.MethodName), "cod", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(NormalizeStatus(p.PaymentMethod?.MethodName), "refund", StringComparison.OrdinalIgnoreCase));

        var paymentOk = paymentStatus is "paid" or "paidonline" or "success";
        var statusOk = status is "hoanthanh" or "danhanhang" or "completed" or "delivered";
        return hasValidPayment && paymentOk && statusOk;
    }

    private static bool IsCodRemittedOrder(Order? order)
    {
        if (order == null) return false;

        var paymentStatus = NormalizeStatus(order.PaymentStatus);
        if (paymentStatus == "codremittedtoadmin")
        {
            return true;
        }

        return order.OrderSettlements.Any(s => IsReadyToSettleStatus(s.SettlementStatus));
    }

    private static bool IsBlockedOrderStatus(string? status)
    {
        var normalized = NormalizeStatus(status);
        return normalized is "pending" or "incart" or "waitingpayment" or "cancelled" or "returned" or "refunded" or "faileddelivery" or "dahuy" or "dahoantien" or "choxacnhanhoanhang";
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
