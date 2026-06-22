using EXE.Models;
using EXE.Security;
using EXE.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EXE.Controllers.Admin;

[AdminOnly]
public class OrderAdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notificationService;

    public OrderAdminController(ApplicationDbContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<IActionResult> Index()
    {
        var orders = await _context.Orders
            .Include(o => o.User)
            .Include(o => o.Status)
            .Include(o => o.Payments)
                .ThenInclude(p => p.PaymentMethod)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();
        return View(orders);
    }

    public async Task<IActionResult> Details(int id)
    {
        var order = await _context.Orders
            .Include(o => o.User)
            .Include(o => o.Status)
            .Include(o => o.Payments)
                .ThenInclude(p => p.PaymentMethod)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.OrderId == id);

        if (order == null) return NotFound();
        ViewBag.Statuses = await _context.OrderStatuses.OrderBy(s => s.StatusId).ToListAsync();
        return View(order);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int id, int statusId)
    {
        var order = await _context.Orders
            .Include(o => o.CODCollection)
            .Include(o => o.OrderSettlements)
            .FirstOrDefaultAsync(o => o.OrderId == id);
        if (order == null) return NotFound();

        order.StatusId = statusId;
        var statusName = await _context.OrderStatuses
            .Where(s => s.StatusId == statusId)
            .Select(s => s.StatusName)
            .FirstOrDefaultAsync();

        if (order.CODCollection != null)
        {
            if (string.Equals(statusName, "Cho giao hang", StringComparison.OrdinalIgnoreCase))
            {
                order.CODCollection.Status = "CashInTransit";
            }
            else if (string.Equals(statusName, "Da nhan hang", StringComparison.OrdinalIgnoreCase))
            {
                order.CODCollection.Status = "CollectedFromCustomer";
                order.CODCollection.CollectedAt ??= DateTime.Now;
            }
            else if (string.Equals(statusName, "Da huy", StringComparison.OrdinalIgnoreCase))
            {
                order.CODCollection.Status = "Cancelled";
                order.PaymentStatus = "Cancelled";
                foreach (var settlement in order.OrderSettlements)
                {
                    settlement.SettlementStatus = "Cancelled";
                }
            }
            else if (string.Equals(statusName, "Da hoan tien", StringComparison.OrdinalIgnoreCase))
            {
                order.PaymentStatus = "Refunded";
                foreach (var settlement in order.OrderSettlements)
                {
                    settlement.SettlementStatus = "Refunded";
                }
            }
        }
        await _context.SaveChangesAsync();

        if (order.UserId.HasValue)
        {
            var title = $"Đơn hàng #{order.OrderId} cập nhật trạng thái";
            await _notificationService.CreateNotification(
                order.UserId.Value,
                title,
                $"Đơn hàng của bạn đã chuyển sang trạng thái: {statusName}.",
                "Order",
                "Order",
                order.OrderId);
        }

        var sellerIds = await _context.OrderItems
            .Where(oi => oi.OrderId == order.OrderId && oi.Product != null && oi.Product.SellerId.HasValue)
            .Select(oi => oi.Product.SellerId!.Value)
            .Distinct()
            .ToListAsync();

        foreach (var sellerId in sellerIds)
        {
            await _notificationService.CreateNotification(
                sellerId,
                $"Đơn hàng #{order.OrderId} đổi trạng thái",
                $"Đơn hàng #{order.OrderId} đã được cập nhật sang trạng thái: {statusName}.",
                "Order",
                "Order",
                order.OrderId);
        }

        await _notificationService.CreateNotificationForRole(
            RoleAccess.Admin,
            $"Đơn hàng #{order.OrderId} đổi trạng thái",
            $"Đơn hàng #{order.OrderId} đã được cập nhật sang trạng thái: {statusName}.",
            "Order",
            "Order",
            order.OrderId);
        return RedirectToAction(nameof(Details), new { id });
    }
}

