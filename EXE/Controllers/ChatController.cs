using System;
using EXE.Models;
using EXE.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.RegularExpressions;

namespace EXE.Controllers;

public class ChatController : Controller
{
    private readonly ApplicationDbContext _context;

    public ChatController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> Ask([FromForm] string message)
    {
        var raw = (message ?? "").Trim();
        var m = raw.ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(m))
            return Json(new { reply = "Bạn hãy nhập câu hỏi giúp mình nhé." });

        // 0) Nhận diện ý định: chào, cảm ơn, tạm biệt, cần giúp
        var greetingWords = new[] { "alo", "alô", "chào", "hello", "hi", "hey", "xin chào", "chào bạn", "chào shop", "chào ad", "ơi" };
        if (greetingWords.Any(w => m == w || m.StartsWith(w + " ") || m.EndsWith(" " + w) || m == "bạn ơi" || m == "ad ơi"))
            return Json(new { reply = "Chào bạn! Mình có thể giúp bạn tìm đặc sản theo vùng miền, giá, hoặc gợi ý giao hàng, thanh toán. Bạn muốn hỏi gì ạ?" });

        var thanksWords = new[] { "cảm ơn", "thanks", "thank you", "cám ơn" };
        if (thanksWords.Any(w => m.Contains(w, StringComparison.OrdinalIgnoreCase)))
            return Json(new { reply = "Không có gì ạ! Bạn cần thêm gì cứ hỏi mình nhé." });

        var byeWords = new[] { "tạm biệt", "bye", "bai", "hẹn gặp lại" };
        if (byeWords.Any(w => m.Contains(w, StringComparison.OrdinalIgnoreCase)))
            return Json(new { reply = "Tạm biệt bạn! Chúc bạn mua sắm vui vẻ. Hẹn gặp lại ạ." });

        var helpWords = new[] { "giúp", "help", "hỗ trợ gì", "làm gì", "có thể hỏi" };
        if (helpWords.Any(w => m.Contains(w, StringComparison.OrdinalIgnoreCase)) && m.Length < 30)
            return Json(new { reply = "Mình hỗ trợ bạn: (1) Tìm đặc sản theo tên, vùng (vd: Hà Nội, Cần Thơ), giá (vd: dưới 100k). (2) Giao hàng, thanh toán, đổi trả. Bạn hỏi cụ thể nhé!" });

        // 1) Knowledge-base style answers (fast path)
        if (m.Contains("ship", StringComparison.OrdinalIgnoreCase) || m.Contains("vận chuyển", StringComparison.OrdinalIgnoreCase) || m.Contains("giao hàng", StringComparison.OrdinalIgnoreCase))
            return Json(new { reply = "Bên mình có hỗ trợ giao hàng toàn quốc. Phí vận chuyển sẽ hiển thị ở bước thanh toán (tạm tính hiện đang là 25.000đ)." });

        if (m.Contains("thanh toán", StringComparison.OrdinalIgnoreCase) || m.Contains("payment", StringComparison.OrdinalIgnoreCase) || m.Contains("cod", StringComparison.OrdinalIgnoreCase))
            return Json(new { reply = "Bạn có thể thanh toán khi nhận hàng (COD) hoặc chuyển khoản (tuỳ cấu hình). Hiện trang Checkout đã hỗ trợ đặt hàng cơ bản." });

        if (m.Contains("đổi trả", StringComparison.OrdinalIgnoreCase) || m.Contains("trả hàng", StringComparison.OrdinalIgnoreCase) || m.Contains("hoàn tiền", StringComparison.OrdinalIgnoreCase))
            return Json(new { reply = "Chính sách đổi trả: nếu sản phẩm lỗi do vận chuyển/đóng gói, bạn liên hệ ngay để được hỗ trợ đổi trả." });

        if (m.Contains("liên hệ", StringComparison.OrdinalIgnoreCase) || m.Contains("support", StringComparison.OrdinalIgnoreCase) || m.Contains("hotline", StringComparison.OrdinalIgnoreCase) || m.Contains("email", StringComparison.OrdinalIgnoreCase))
            return Json(new { reply = "Bạn vào trang Liên hệ để gửi tin nhắn hoặc dùng email: hotro@nguonviet.vn." });

        if (m.Contains("đặt hàng", StringComparison.OrdinalIgnoreCase) || m.Contains("mua hàng", StringComparison.OrdinalIgnoreCase) || m.Contains("order", StringComparison.OrdinalIgnoreCase))
            return Json(new { reply = "Bạn chọn sản phẩm muốn mua, bấm 'Thêm giỏ', sau đó vào trang Giỏ hàng / Thanh toán để nhập địa chỉ và hoàn tất đơn. Nếu cần, bạn có thể ghi chú thêm yêu cầu trong phần địa chỉ." });

        if (m.Contains("tài khoản", StringComparison.OrdinalIgnoreCase) || m.Contains("đăng ký", StringComparison.OrdinalIgnoreCase) || m.Contains("đăng nhập", StringComparison.OrdinalIgnoreCase))
            return Json(new { reply = "Bạn bấm vào biểu tượng tài khoản (hoặc mục Đăng nhập/Đăng ký) ở góc trên, nhập email và mật khẩu để đăng ký. Sau khi đăng nhập, bạn xem lịch sử đơn ở mục 'Tài khoản > Hóa đơn của tôi'." });

        if (m.Contains("khuyến mãi", StringComparison.OrdinalIgnoreCase) || m.Contains("voucher", StringComparison.OrdinalIgnoreCase) || m.Contains("mã giảm giá", StringComparison.OrdinalIgnoreCase) || m.Contains("sale", StringComparison.OrdinalIgnoreCase))
            return Json(new { reply = "Các mã giảm giá hiện có sẽ được hiển thị trên banner hoặc trong phần Thanh toán. Khi có mã, bạn nhập ở ô 'Mã giảm giá' trên trang Giỏ hàng/Thanh toán để áp dụng." });

        if (m.Contains("theo dõi đơn", StringComparison.OrdinalIgnoreCase) || m.Contains("trạng thái đơn", StringComparison.OrdinalIgnoreCase) || m.Contains("kiểm tra đơn", StringComparison.OrdinalIgnoreCase))
            return Json(new { reply = "Bạn đăng nhập, vào 'Tài khoản > Hóa đơn của tôi' để xem danh sách đơn và trạng thái từng đơn (mới, đang xử lý, đang giao, hoàn thành ...)." });

        if (m.Contains("giờ mở cửa", StringComparison.OrdinalIgnoreCase) || m.Contains("thời gian làm việc", StringComparison.OrdinalIgnoreCase))
            return Json(new { reply = "Website hoạt động 24/7 để bạn đặt hàng bất cứ lúc nào. Thời gian xử lý & giao hàng sẽ phụ thuộc khung giờ làm việc của đơn vị vận chuyển." });

        // 1.b) Tư vấn theo ngân sách: "dưới 200k", "tầm 300k", "khoảng 500k"
        decimal? maxBudget = TryParseMaxBudget(m);
        if (maxBudget.HasValue)
        {
            var budget = maxBudget.Value;
            var budgetProducts = await _context.Products
                .Include(p => p.Region)
                .OrderBy(p => p.Price ?? 0)
                .Where(p => p.ApprovalStatus == "Approved" && (p.Price ?? 0) > 0 && (p.Price ?? 0) <= budget)
                .Take(5)
                .ToListAsync();

            if (budgetProducts.Any())
            {
                var sbBudget = new StringBuilder();
                sbBudget.AppendLine($"Một số đặc sản dưới khoảng {budget:N0}đ bạn có thể tham khảo:");
                foreach (var p in budgetProducts)
                {
                    var price = p.Price ?? 0;
                    var regionName = p.Region?.RegionName ?? p.Region?.Province ?? "Đặc sản";
                    sbBudget.AppendLine($"- {p.ProductName} ({regionName}) – {price:N0}đ – Mã: #{p.ProductId}");
                }
                sbBudget.AppendLine();
                sbBudget.AppendLine("Bạn có thể gõ thêm tên vùng (vd: Hà Nội, Miền Trung) hoặc loại sản phẩm (vd: trà, bánh, mứt) để mình lọc kỹ hơn.");

                return Json(new { reply = sbBudget.ToString().Trim() });
            }
        }

        // 2) DB-powered: search products/regions/categories by keyword
        var keyword = raw.Trim();
        if (keyword.Length >= 2)
        {
            var keywordLower = keyword.ToLowerInvariant();

            var products = await _context.Products
                .Include(p => p.Region)
                .Include(p => p.Category)
                .OrderByDescending(p => p.CreatedDate)
                .Where(p => p.ApprovalStatus == "Approved" && (
                    (p.ProductName != null && p.ProductName.ToLower().Contains(keywordLower)) ||
                    (p.Description != null && p.Description.ToLower().Contains(keywordLower)) ||
                    (p.Region != null && p.Region.RegionName != null && p.Region.RegionName.ToLower().Contains(keywordLower)) ||
                    (p.Region != null && p.Region.Province != null && p.Region.Province.ToLower().Contains(keywordLower)) ||
                    (p.Category != null && p.Category.CategoryName != null && p.Category.CategoryName.ToLower().Contains(keywordLower))))
                .Take(5)
                .ToListAsync();

            var regions = await _context.Regions
                .Where(r =>
                    (r.RegionName != null && r.RegionName.ToLower().Contains(keywordLower)) ||
                    (r.Province != null && r.Province.ToLower().Contains(keywordLower)))
                .Take(3)
                .ToListAsync();

            var categories = await _context.Categories
                .Where(c => c.CategoryName != null && c.CategoryName.ToLower().Contains(keywordLower))
                .Take(3)
                .ToListAsync();

            // Accent-insensitive fallback if nothing found with plain Contains.
            if (!products.Any() && !regions.Any() && !categories.Any())
            {
                var normKey = TextSearch.Normalize(keyword);

                var allRegions = await _context.Regions.AsNoTracking().ToListAsync();
                regions = allRegions
                    .Where(r =>
                        TextSearch.ContainsNormalized(normKey, r.RegionName) ||
                        TextSearch.ContainsNormalized(normKey, r.Province))
                    .Take(3)
                    .ToList();

                var allCategories = await _context.Categories.AsNoTracking().ToListAsync();
                categories = allCategories
                    .Where(c => TextSearch.ContainsNormalized(normKey, c.CategoryName))
                    .Take(3)
                    .ToList();

                var allProducts = await _context.Products
                    .Include(p => p.Region)
                    .Include(p => p.Category)
                    .Where(p => p.ApprovalStatus == "Approved")
                    .OrderByDescending(p => p.CreatedDate)
                    .ToListAsync();

                products = allProducts
                    .Where(p =>
                        TextSearch.ContainsNormalized(normKey, p.ProductName) ||
                        TextSearch.ContainsNormalized(normKey, p.Description) ||
                        TextSearch.ContainsNormalized(normKey, p.Region?.RegionName) ||
                        TextSearch.ContainsNormalized(normKey, p.Region?.Province) ||
                        TextSearch.ContainsNormalized(normKey, p.Category?.CategoryName))
                    .Take(5)
                    .ToList();
            }

            if (products.Any() || regions.Any() || categories.Any())
            {
                var sb = new StringBuilder();

                if (regions.Any())
                {
                    sb.AppendLine("Mình tìm thấy khu vực phù hợp:");
                    foreach (var r in regions)
                        sb.AppendLine($"- {r.RegionName} ({r.Province})");
                    sb.AppendLine();
                }

                if (categories.Any())
                {
                    sb.AppendLine("Danh mục liên quan:");
                    foreach (var c in categories)
                        sb.AppendLine($"- {c.CategoryName}");
                    sb.AppendLine();
                }

                if (products.Any())
                {
                    sb.AppendLine("Gợi ý đặc sản cho bạn:");
                    foreach (var p in products)
                    {
                        var price = p.Price ?? 0;
                        var regionName = p.Region?.RegionName;
                        sb.AppendLine($"- {p.ProductName} ({(regionName ?? "Đặc sản")}) – {price:N0}đ – Mã: #{p.ProductId}");
                    }
                    sb.AppendLine();
                    sb.AppendLine("Bạn muốn mình lọc theo vùng hay theo khoảng giá? (Ví dụ: \"Hà Nội\" hoặc \"dưới 200k\")");
                }

                return Json(new { reply = sb.ToString().Trim() });
            }
        }

        // 3) Fallback thông minh: gợi ý vùng + danh mục thay vì chỉ báo "không tìm thấy"
        var someRegions = await _context.Regions.AsNoTracking().Take(5).Select(r => r.RegionName ?? r.Province).Where(x => x != null).ToListAsync();
        var someCategories = await _context.Categories.AsNoTracking().Take(4).Select(c => c.CategoryName).Where(x => x != null).ToListAsync();
        var regionList = someRegions.Any() ? string.Join(", ", someRegions.Take(4)) : "Hà Nội, Cần Thơ, Đà Nẵng";
        var categoryList = someCategories.Any() ? string.Join(", ", someCategories) : "đặc sản vùng miền";
        var fallback = "Mình chưa tìm được đặc sản khớp với \"" + raw + "\". Bạn thử: (1) Tên đặc sản hoặc vùng như " + regionList + ". (2) Khoảng giá ví dụ \"dưới 100k\". (3) Danh mục: " + categoryList + ".";
        return Json(new { reply = fallback });
    }

    private static decimal? TryParseMaxBudget(string messageLower)
    {
        if (string.IsNullOrWhiteSpace(messageLower))
            return null;

        // Chỉ xử lý khi có ý nói "dưới", "tầm", "khoảng", "<"
        if (!messageLower.Contains("dưới") &&
            !messageLower.Contains("duoi") &&
            !messageLower.Contains("tầm") &&
            !messageLower.Contains("tam") &&
            !messageLower.Contains("khoảng") &&
            !messageLower.Contains("khoang") &&
            !messageLower.Contains("<"))
        {
            return null;
        }

        var match = Regex.Match(messageLower, @"(\d+)\s*(k|nghìn|ngan|ngàn|000|tr|triệu|trieu)?");
        if (!match.Success)
            return null;

        if (!int.TryParse(match.Groups[1].Value, out var number))
            return null;

        var unit = match.Groups[2].Value;
        decimal amount;

        if (string.IsNullOrEmpty(unit))
        {
            amount = number;
        }
        else if (unit.Contains("tr") || unit.Contains("triệu") || unit.Contains("trieu"))
        {
            amount = number * 1_000_000m;
        }
        else
        {
            // mặc định "k", "nghìn", "000", ...
            amount = number * 1_000m;
        }

        if (amount <= 0)
            return null;

        return amount;
    }
}

