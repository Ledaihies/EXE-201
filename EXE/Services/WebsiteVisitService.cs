using EXE.Models;
using EXE.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace EXE.Services;

public sealed class WebsiteVisitService : IWebsiteVisitService
{
    private const string VisitorCookieName = "nv_visitor_key";
    private static readonly string[] SkippedSegments =
    {
        "/admin",
        "/productadmin",
        "/orderadmin",
        "/revenueadmin",
        "/accountadmin",
        "/advertisingadmin",
        "/voucheradmin",
        "/seller",
        "/staff",
        "/api",
        "/webhooks"
    };

    private readonly ApplicationDbContext _context;

    private sealed record VisitDayStat(DateTime Date, int Count);

    public WebsiteVisitService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task RecordVisitAsync(HttpContext context)
    {
        try
        {
            if (!ShouldTrack(context))
            {
                return;
            }

            var visitorKey = await ResolveVisitorKeyAsync(context);
            if (string.IsNullOrWhiteSpace(visitorKey))
            {
                return;
            }

            var today = DateTime.Today;
            var exists = await _context.WebsiteVisits.AnyAsync(v =>
                v.VisitDate == today &&
                v.VisitorKey == visitorKey);

            if (exists)
            {
                return;
            }

            var userId = context.Session.GetInt32("UserId");
            _context.WebsiteVisits.Add(new WebsiteVisit
            {
                VisitorKey = visitorKey,
                UserId = userId,
                SessionId = context.Session.Id,
                IpAddress = context.Connection.RemoteIpAddress?.ToString(),
                UserAgent = context.Request.Headers.UserAgent.ToString(),
                Path = context.Request.Path.Value,
                VisitDate = today,
                VisitedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();
        }
        catch (Exception)
        {
            // Best-effort tracking only. Never block the real request path.
        }
    }

    public async Task<WebsiteVisitStatsViewModel> GetStatsAsync(DateTime? from = null, DateTime? to = null)
    {
        var today = DateTime.Today;
        var fromDate = (from ?? today.AddDays(-13)).Date;
        var toExclusive = (to ?? today).Date.AddDays(1);

        int totalVisits = 0;
        int todayVisits = 0;
        int uniqueVisitors = 0;
        int productViews = 0;
        List<VisitDayStat> dailyVisits = new();

        try
        {
            totalVisits = await _context.WebsiteVisits.CountAsync();
            todayVisits = await _context.WebsiteVisits.CountAsync(v => v.VisitDate == today);
            uniqueVisitors = await _context.WebsiteVisits
                .Select(v => v.VisitorKey)
                .Distinct()
                .CountAsync();
            productViews = await _context.Products
                .Select(p => p.ViewCount ?? 0)
                .SumAsync();

            var grouped = await _context.WebsiteVisits
                .Where(v => v.VisitDate >= fromDate && v.VisitDate < toExclusive)
                .GroupBy(v => v.VisitDate)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToListAsync();

            dailyVisits = grouped
                .Select(x => new VisitDayStat(x.Date, x.Count))
                .ToList();
        }
        catch
        {
            // If the table is not deployed yet, return zeroed stats instead of failing the admin page.
        }

        var days = Enumerable.Range(0, (toExclusive.Date - fromDate.Date).Days)
            .Select(i => fromDate.AddDays(i))
            .ToList();

        return new WebsiteVisitStatsViewModel
        {
            TotalVisits = totalVisits,
            TodayVisits = todayVisits,
            UniqueVisitors = uniqueVisitors,
            ProductViews = productViews,
            DailyVisits = days.Select(d =>
            {
                var match = dailyVisits.FirstOrDefault(x => x.Date == d);
                return match?.Count ?? 0;
            }).ToList()
        };
    }

    private static bool ShouldTrack(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            return false;
        }

        if (context.Request.Headers.TryGetValue("X-Requested-With", out var requestedWith) &&
            string.Equals(requestedWith.ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (Path.HasExtension(path))
        {
            return false;
        }

        var lower = path.ToLowerInvariant();
        return !SkippedSegments.Any(lower.StartsWith);
    }

    private async Task<string> ResolveVisitorKeyAsync(HttpContext context)
    {
        var userId = context.Session.GetInt32("UserId");
        if (userId.HasValue)
        {
            return $"user:{userId.Value}";
        }

        if (context.Request.Cookies.TryGetValue(VisitorCookieName, out var cookieValue) &&
            !string.IsNullOrWhiteSpace(cookieValue))
        {
            return cookieValue;
        }

        var visitorKey = Guid.NewGuid().ToString("N");
        context.Response.Cookies.Append(
            VisitorCookieName,
            visitorKey,
            new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                SameSite = SameSiteMode.Lax,
                Secure = context.Request.IsHttps
            });

        await Task.CompletedTask;
        return visitorKey;
    }
}
