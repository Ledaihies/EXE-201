using EXE.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EXE.ViewComponents;

public class JourneyLevelViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _context;

    public JourneyLevelViewComponent(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
            return View("Default", (LevelName: (string?)null, RegionsCount: 0));

        var roleName = HttpContext.Session.GetString("RoleName") ?? "";
        if (string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(roleName, "Staff", StringComparison.OrdinalIgnoreCase))
            return View("Default", (LevelName: (string?)null, RegionsCount: 0));

        var regionsCount = await _context.OrderItems
            .Where(oi => oi.Order != null && oi.Order.UserId == userId.Value && oi.Product != null && oi.Product.RegionId != null)
            .Select(oi => oi.Product!.RegionId)
            .Distinct()
            .CountAsync();

        string? levelName = null;
        if (regionsCount >= 5) levelName = "Nhà thám hiểm";
        else if (regionsCount >= 3) levelName = "Du khách";
        else if (regionsCount >= 1) levelName = "Khám phá";

        return View("Default", (LevelName: levelName, RegionsCount: regionsCount));
    }
}
