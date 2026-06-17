using EXE.Models;
using EXE.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EXE.Controllers.Admin;

[AdminOnly]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
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

            // Build daily traffic & revenue data for the last 14 days
            var today = DateTime.Today;
            var fromDate = today.AddDays(-13); // 14 days including today

            var cancelledStatus = await _context.OrderStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StatusName == "Hoàn thành");

            var ordersForChart = _context.Orders.AsQueryable();

            if (false && cancelledStatus != null)
            {
                ordersForChart = ordersForChart.Where(o => o.StatusId != cancelledStatus.StatusId);
            }

            var cancelledStatusId = await _context.OrderStatuses
                .AsNoTracking()
                .Where(s => s.StatusName == "Da huy")
                .Select(s => (int?)s.StatusId)
                .FirstOrDefaultAsync();
            if (cancelledStatusId.HasValue)
            {
                ordersForChart = ordersForChart.Where(o => o.StatusId != cancelledStatusId.Value);
            }

            ordersForChart = ordersForChart
                .Where(o => o.OrderDate != null &&
                            o.OrderDate >= fromDate &&
                            o.OrderDate < today.AddDays(1));

            var grouped = await ordersForChart
                .GroupBy(o => o.OrderDate!.Value.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    OrderCount = g.Count(),
                    Revenue = g.Sum(o => o.TotalAmount ?? 0)
                })
                .ToListAsync();

            var allDays = Enumerable.Range(0, 14)
                .Select(i => fromDate.AddDays(i))
                .ToList();

            var labels = allDays
                .Select(d => d.ToString("dd/MM"))
                .ToList();

            var trafficData = allDays
                .Select(d => grouped.FirstOrDefault(x => x.Date == d)?.OrderCount ?? 0)
                .ToList();

            var revenueData = allDays
                .Select(d => grouped.FirstOrDefault(x => x.Date == d)?.Revenue ?? 0m)
                .ToList();

            ViewBag.ChartLabelsJson = JsonSerializer.Serialize(labels);
            ViewBag.TrafficDataJson = JsonSerializer.Serialize(trafficData);
            ViewBag.RevenueDataJson = JsonSerializer.Serialize(revenueData);

            return View();
        }
    }

