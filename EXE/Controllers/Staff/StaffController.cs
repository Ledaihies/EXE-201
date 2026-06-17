using EXE.Models;
using EXE.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EXE.Controllers.Staff;

[StaffOnly]
public class StaffController : Controller
{
    private readonly ApplicationDbContext _context;

    public StaffController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var totalOrders = await _context.Orders.CountAsync();
        var pendingOrders = await _context.Orders.CountAsync(o => o.Status != null && o.Status.StatusName == "Chờ xử lý");
        var today = DateTime.Today;
        var todayOrders = await _context.Orders.CountAsync(o => o.OrderDate >= today);

        ViewBag.TotalOrders = totalOrders;
        ViewBag.PendingOrders = pendingOrders;
        ViewBag.TodayOrders = todayOrders;

        return View();
    }
}

