using EXE.Models;
using EXE.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EXE.Controllers.Admin;

[AdminOnly]
public class OrderAdminController : Controller
{
    private readonly ApplicationDbContext _context;

    public OrderAdminController(ApplicationDbContext context)
    {
        _context = context;
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
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound();

        order.StatusId = statusId;
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id });
    }
}

