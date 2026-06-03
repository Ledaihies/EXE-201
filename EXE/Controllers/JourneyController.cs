using EXE.Models;
using EXE.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EXE.Controllers;

public class JourneyController : Controller
{
    private readonly ApplicationDbContext _context;

    public JourneyController(ApplicationDbContext context)
    {
        _context = context;
    }

    private int? GetCurrentUserId()
    {
        return HttpContext.Session.GetInt32("UserId");
    }

    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return RedirectToAction("Login", "Auth");
        }

        // Lấy tất cả đơn hàng có sản phẩm gắn vùng cho user
        var orderItems = await _context.OrderItems
            .Include(oi => oi.Order)
            .Include(oi => oi.Product)!.ThenInclude(p => p.Region)
            .Include(oi => oi.Product)!.ThenInclude(p => p.ProductImages)
            .Where(oi => oi.Order != null && oi.Order.UserId == userId.Value && oi.Product != null && oi.Product.RegionId != null)
            .ToListAsync();

        if (!orderItems.Any())
        {
            // Vẫn gợi ý vùng để bắt đầu hành trình
            var allRegionsForNew = await _context.Regions
                .Where(r => _context.Products.Any(p => p.RegionId == r.RegionId))
                .OrderBy(r => r.RegionName)
                .Take(6)
                .Select(r => new NextRegionSuggestionViewModel
                {
                    RegionId = r.RegionId,
                    RegionName = r.RegionName ?? "Vùng miền",
                    Province = r.Province,
                    Reason = "Bắt đầu hành trình từ đây"
                })
                .ToListAsync();
            return View(new CustomerJourneyViewModel
            {
                NextRegionSuggestions = allRegionsForNew
            });
        }

        var grouped = orderItems
            .Where(oi => oi.Product!.Region != null)
            .GroupBy(oi => oi.Product!.Region!)
            .Select(g =>
            {
                var region = g.Key;
                var orders = g.Select(oi => oi.Order!).DistinctBy(o => o.OrderId).ToList();
                var firstVisited = orders
                    .Where(o => o.OrderDate.HasValue)
                    .OrderBy(o => o.OrderDate)
                    .FirstOrDefault()?.OrderDate;

                var totalAmount = orders.Sum(o => o.TotalAmount ?? 0);
                var sampleProducts = g.Select(oi => oi.Product!)
                    .DistinctBy(p => p.ProductId)
                    .Take(4)
                    .ToList();

                return new JourneyStopViewModel
                {
                    RegionId = region.RegionId,
                    RegionName = region.RegionName ?? "Vùng miền",
                    Province = region.Province,
                    FirstVisitedAt = firstVisited,
                    OrdersCount = orders.Count,
                    TotalAmount = totalAmount,
                    SampleProducts = sampleProducts
                };
            })
            .OrderBy(s => s.FirstVisitedAt ?? DateTime.MaxValue)
            .ToList();

        // Gamification / thống kê
        var regionsCount = grouped.Count;
        var totalOrders = grouped.Sum(s => s.OrdersCount);
        var totalAmount = grouped.Sum(s => s.TotalAmount);

        var badges = new List<string>();
        var suggestions = new List<string>();

        // Huy hiệu "Tín đồ Hà Nội": đã mua >= 3 đơn từ vùng có tên chứa "Hà Nội"
        var haNoiStop = grouped.FirstOrDefault(s =>
            (!string.IsNullOrWhiteSpace(s.RegionName) && s.RegionName.Contains("Hà Nội", StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(s.Province) && s.Province.Contains("Hà Nội", StringComparison.OrdinalIgnoreCase)));
        if (haNoiStop != null && haNoiStop.OrdersCount >= 3)
        {
            badges.Add("Tín đồ Hà Nội – bạn đã có ít nhất 3 đơn từ Hà Nội.");
        }

        // Huy hiệu "Phượt thủ miền Trung": đã mua ở >= 3 vùng thuộc miền Trung (xấp xỉ theo tên tỉnh/thành)
        string[] centralKeywords =
        {
            "Huế", "Đà Nẵng", "Quảng Trị", "Quảng Bình", "Quảng Nam",
            "Quảng Ngãi", "Bình Định", "Phú Yên", "Khánh Hòa",
            "Nghệ An", "Hà Tĩnh", "Thanh Hóa"
        };

        int centralVisited = grouped.Count(s =>
        {
            var name = (s.RegionName ?? string.Empty) + " " + (s.Province ?? string.Empty);
            return centralKeywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase));
        });

        if (centralVisited >= 3)
        {
            badges.Add("Phượt thủ miền Trung – bạn đã khám phá ít nhất 3 vùng miền Trung.");
        }

        // Gợi ý "Miền Tây" nếu đã có Hà Nội & Huế nhưng chưa có vùng gợi ý thuộc miền Tây
        bool hasHanoi = grouped.Any(s =>
            (!string.IsNullOrWhiteSpace(s.RegionName) && s.RegionName.Contains("Hà Nội", StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(s.Province) && s.Province.Contains("Hà Nội", StringComparison.OrdinalIgnoreCase)));

        bool hasHue = grouped.Any(s =>
            (!string.IsNullOrWhiteSpace(s.RegionName) && s.RegionName.Contains("Huế", StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(s.Province) && s.Province.Contains("Huế", StringComparison.OrdinalIgnoreCase)));

        string[] mekongKeywords =
        {
            "Cần Thơ", "An Giang", "Đồng Tháp", "Vĩnh Long", "Tiền Giang",
            "Bến Tre", "Trà Vinh", "Sóc Trăng", "Bạc Liêu", "Cà Mau", "Hậu Giang", "Kiên Giang"
        };

        bool hasMekong = grouped.Any(s =>
        {
            var name = (s.RegionName ?? string.Empty) + " " + (s.Province ?? string.Empty);
            return mekongKeywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase));
        });

        if (hasHanoi && hasHue && !hasMekong)
        {
            suggestions.Add("Bạn đã ghé Hà Nội và Huế, thử thêm đặc sản Miền Tây (Cần Thơ, An Giang, Cà Mau...) nhé!");
        }

        // Tiến độ 3 miền (Bắc / Trung / Nam)
        bool hasNorth = grouped.Any(s =>
        {
            var name = (s.RegionName ?? string.Empty) + " " + (s.Province ?? string.Empty);
            return name.Contains("Hà Nội", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("Miền Bắc", StringComparison.OrdinalIgnoreCase);
        });
        bool hasCentral = grouped.Any(s =>
        {
            var name = (s.RegionName ?? string.Empty) + " " + (s.Province ?? string.Empty);
            return centralKeywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase)) ||
                   (s.RegionName ?? string.Empty).Contains("Miền Trung", StringComparison.OrdinalIgnoreCase);
        });
        bool hasSouth = grouped.Any(s =>
        {
            var name = (s.RegionName ?? string.Empty) + " " + (s.Province ?? string.Empty);
            return name.Contains("Hồ Chí Minh", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("Sài Gòn", StringComparison.OrdinalIgnoreCase) ||
                   (s.RegionName ?? string.Empty).Contains("Miền Nam", StringComparison.OrdinalIgnoreCase) ||
                   mekongKeywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase));
        });
        if (regionsCount >= 5) badges.Add("Nhà thám hiểm – bạn đã ghé ít nhất 5 vùng miền.");
        if (hasNorth && hasCentral && hasSouth) badges.Add("Trọn vẹn 3 miền – Bắc, Trung, Nam đều đã có trong hành trình của bạn!");

        // Gợi ý vùng tiếp theo: các vùng có sản phẩm nhưng user chưa ghé
        var visitedRegionIds = grouped.Select(s => s.RegionId).ToHashSet();
        var allRegionsWithProducts = await _context.Regions
            .Where(r => _context.Products.Any(p => p.RegionId == r.RegionId))
            .OrderBy(r => r.RegionName)
            .ToListAsync();

        var nextRegionSuggestions = new List<NextRegionSuggestionViewModel>();
        foreach (var r in allRegionsWithProducts)
        {
            if (visitedRegionIds.Contains(r.RegionId)) continue;
            var reason = "";
            var name = (r.RegionName ?? "") + " " + (r.Province ?? "");
            bool isNorth = name.Contains("Hà Nội", StringComparison.OrdinalIgnoreCase) || (r.RegionName ?? "").Contains("Miền Bắc", StringComparison.OrdinalIgnoreCase);
            bool isCentral = centralKeywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase)) || (r.RegionName ?? "").Contains("Miền Trung", StringComparison.OrdinalIgnoreCase);
            bool isSouth = name.Contains("Hồ Chí Minh", StringComparison.OrdinalIgnoreCase) || (r.RegionName ?? "").Contains("Miền Nam", StringComparison.OrdinalIgnoreCase) || mekongKeywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase));
            if (!hasNorth && isNorth) reason = "Khám phá đặc sản miền Bắc";
            else if (!hasCentral && isCentral) reason = "Trải nghiệm hương vị miền Trung";
            else if (!hasSouth && isSouth) reason = "Nếm thử đặc sản miền Nam";
            else if (r.RegionName?.Contains("Tây Nguyên", StringComparison.OrdinalIgnoreCase) == true) reason = "Đặc sản Tây Nguyên độc đáo";
            else reason = "Vùng bạn chưa ghé – đáng thử!";
            nextRegionSuggestions.Add(new NextRegionSuggestionViewModel
            {
                RegionId = r.RegionId,
                RegionName = r.RegionName ?? "Vùng miền",
                Province = r.Province,
                Reason = reason
            });
        }
        // Giới hạn tối đa 6 gợi ý, ưu tiên theo thứ tự Bắc/Trung/Nam nếu chưa đủ
        nextRegionSuggestions = nextRegionSuggestions
            .OrderBy(x => x.Reason.StartsWith("Khám phá") ? 0 : x.Reason.StartsWith("Trải nghiệm") ? 1 : x.Reason.StartsWith("Nếm thử") ? 2 : 3)
            .Take(6)
            .ToList();

        // Cấp độ khách: Khám phá (1-2), Du khách (3-4), Nhà thám hiểm (5+)
        string? journeyLevelName = null;
        int journeyLevelOrder = 0;
        if (regionsCount >= 5) { journeyLevelName = "Nhà thám hiểm"; journeyLevelOrder = 3; }
        else if (regionsCount >= 3) { journeyLevelName = "Du khách"; journeyLevelOrder = 2; }
        else if (regionsCount >= 1) { journeyLevelName = "Khám phá"; journeyLevelOrder = 1; }

        // Mục tiêu nhỏ tiếp theo
        var nextGoals = new List<string>();
        if (regionsCount < 3)
        {
            var need = 3 - regionsCount;
            nextGoals.Add($"Mua thêm {need} vùng nữa để lên cấp Du khách.");
        }
        else if (regionsCount < 5)
        {
            var need = 5 - regionsCount;
            nextGoals.Add($"Mua thêm {need} vùng nữa để nhận huy hiệu Nhà thám hiểm.");
        }
        if (!hasNorth || !hasCentral || !hasSouth)
        {
            var missing = new List<string>();
            if (!hasNorth) missing.Add("Bắc");
            if (!hasCentral) missing.Add("Trung");
            if (!hasSouth) missing.Add("Nam");
            nextGoals.Add($"Khám phá thêm miền {string.Join(", ", missing)} để nhận huy hiệu Trọn vẹn 3 miền.");
        }
        if (centralVisited > 0 && centralVisited < 3)
        {
            var need = 3 - centralVisited;
            nextGoals.Add($"Mua thêm đặc sản {need} vùng miền Trung nữa để nhận huy hiệu Phượt thủ miền Trung.");
        }
        if (haNoiStop == null || haNoiStop.OrdersCount < 3)
        {
            var need = haNoiStop == null ? 3 : 3 - haNoiStop.OrdersCount;
            nextGoals.Add($"Đặt thêm {need} đơn từ Hà Nội để nhận huy hiệu Tín đồ Hà Nội.");
        }
        nextGoals = nextGoals.Take(3).ToList(); // tối đa 3 mục tiêu

        var vm = new CustomerJourneyViewModel
        {
            Stops = grouped,
            TotalOrders = totalOrders,
            TotalAmount = totalAmount,
            RegionsCount = regionsCount,
            Badges = badges,
            Suggestions = suggestions,
            NextRegionSuggestions = nextRegionSuggestions,
            HasNorth = hasNorth,
            HasCentral = hasCentral,
            HasSouth = hasSouth,
            JourneyLevelName = journeyLevelName,
            JourneyLevelOrder = journeyLevelOrder,
            NextGoals = nextGoals
        };

        return View(vm);
    }
}

