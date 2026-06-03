using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EXE.Models;
using EXE.Utils;
using System.Text.Json;
using EXE.ViewModels;
using System.Globalization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Diagnostics;

namespace EXE.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly IMemoryCache _cache;

        public HomeController(ApplicationDbContext context, IConfiguration config, IMemoryCache cache)
        {
            _context = context;
            _config = config;
            _cache = cache;
        }

        public async Task<IActionResult> Index()
        {
            var featured = await GetFeaturedProducts(20);
            var advertised = await GetAdvertisedProducts(20);
            var (defaultProducts, defaultLabel) = await GetProductsForLocationOrFeatured("Hà Nội", take: 8);

            // Demo: dùng danh sách voucher mẫu trong mã, không phụ thuộc cấu trúc bảng Voucher trong DB
            var demoVouchers = new List<Voucher>
            {
                new Voucher
                {
                    Code = "DEMO10",
                    Description = "Giảm 10% cho mọi đơn hàng demo trên Nguồn Việt.",
                    DiscountPercent = 10,
                    MinOrderAmount = 100000,
                    OccasionTag = "Demo quanh năm"
                },
                new Voucher
                {
                    Code = "TET2026",
                    Description = "Ưu đãi mừng năm mới – giảm 50.000đ cho đơn từ 400.000đ.",
                    DiscountFixed = 50000,
                    MinOrderAmount = 400000,
                    OccasionTag = "Tết"
                },
                new Voucher
                {
                    Code = "QTBANBE",
                    Description = "Giảm 15% cho đơn tặng bạn bè, áp dụng cho mọi vùng miền.",
                    DiscountPercent = 15,
                    MinOrderAmount = 300000,
                    OccasionTag = "Quà tặng"
                }
            };

            var now = DateTime.Now;
            var activeVouchers = await _context.Vouchers
                .Where(v => v.IsActive &&
                            (!v.StartDate.HasValue || v.StartDate <= now) &&
                            (!v.ExpiryDate.HasValue || v.ExpiryDate >= now))
                .OrderByDescending(v => v.IsFreeShipping)
                .ThenByDescending(v => v.DiscountPercent ?? 0)
                .ThenByDescending(v => v.DiscountFixed ?? 0)
                .Take(12)
                .ToListAsync();

            var journeySummary = await GetHomeJourneySummary();

            var vm = new HomeIndexViewModel
            {
                FeaturedProducts = featured,
                AdvertisedProducts = advertised,
                DefaultRegionProducts = defaultProducts,
                DefaultRegionLabel = defaultLabel ?? "Hà Nội",
                ActiveVouchers = activeVouchers,
                JourneySummary = journeySummary
            };

            return View(vm);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            if (exceptionFeature?.Error != null)
            {
                Console.Error.WriteLine(exceptionFeature.Error);
            }
            var message = "Yêu cầu của bạn chưa thể xử lý lúc này. Vui lòng kiểm tra lại thông tin hoặc thử lại sau.";

            if (exceptionFeature?.Path?.Contains("/Seller", StringComparison.OrdinalIgnoreCase) == true)
            {
                message = "Thao tác ở kênh người bán chưa hợp lệ. Vui lòng kiểm tra lại dữ liệu sản phẩm, ảnh tải lên hoặc quyền truy cập.";
            }

            Response.StatusCode = StatusCodes.Status200OK;
            return View("~/Views/Shared/Error.cshtml", new ErrorViewModel
            {
                RequestId = HttpContext.TraceIdentifier,
                Title = "Không thể thực hiện thao tác",
                Message = message
            });
        }

        [Route("Home/StatusCode/{code:int}")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult StatusCodePage(int code)
        {
            var title = code switch
            {
                403 => "Bạn chưa có quyền truy cập",
                404 => "Không tìm thấy nội dung",
                _ => "Không thể truy cập trang"
            };

            var message = code switch
            {
                403 => "Tài khoản hiện tại chưa có quyền xem trang này. Vui lòng đăng nhập đúng tài khoản hoặc quay lại trang phù hợp.",
                404 => "Trang hoặc dữ liệu bạn đang mở không tồn tại, đã bị xóa hoặc chưa được duyệt.",
                _ => "Trang bạn yêu cầu chưa thể hiển thị. Vui lòng kiểm tra lại đường dẫn hoặc thử lại sau."
            };

            Response.StatusCode = StatusCodes.Status200OK;
            return View("~/Views/Shared/Error.cshtml", new ErrorViewModel
            {
                RequestId = HttpContext.TraceIdentifier,
                StatusCode = code,
                Title = title,
                Message = message
            });
        }

        private async Task<HomeJourneySummaryViewModel?> GetHomeJourneySummary()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                return null;
            }

            var roleName = HttpContext.Session.GetString("RoleName") ?? string.Empty;
            if (string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(roleName, "Staff", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var orderItems = await _context.OrderItems
                .Include(oi => oi.Order)
                .Include(oi => oi.Product)!.ThenInclude(p => p.Region)
                .Where(oi => oi.Order != null && oi.Order.UserId == userId.Value && oi.Product != null && oi.Product.RegionId != null)
                .ToListAsync();

            if (!orderItems.Any())
            {
                return null;
            }

            var grouped = orderItems
                .Where(oi => oi.Product!.Region != null)
                .GroupBy(oi => oi.Product!.Region!)
                .Select(g => new
                {
                    Region = g.Key,
                    OrdersCount = g.Select(oi => oi.Order!).DistinctBy(o => o.OrderId).Count()
                })
                .ToList();

            var regionsCount = grouped.Count;

            string? journeyLevelName = null;
            if (regionsCount >= 5) journeyLevelName = "Nhà thám hiểm";
            else if (regionsCount >= 3) journeyLevelName = "Du khách";
            else if (regionsCount >= 1) journeyLevelName = "Khám phá";

            string[] centralKeywords =
            {
                "Huế", "Đà Nẵng", "Quảng Trị", "Quảng Bình", "Quảng Nam",
                "Quảng Ngãi", "Bình Định", "Phú Yên", "Khánh Hòa",
                "Nghệ An", "Hà Tĩnh", "Thanh Hóa"
            };

            string[] mekongKeywords =
            {
                "Cần Thơ", "An Giang", "Đồng Tháp", "Vĩnh Long", "Tiền Giang",
                "Bến Tre", "Trà Vinh", "Sóc Trăng", "Bạc Liêu", "Cà Mau", "Hậu Giang", "Kiên Giang"
            };

            bool hasNorth = grouped.Any(s =>
            {
                var name = (s.Region.RegionName ?? string.Empty) + " " + (s.Region.Province ?? string.Empty);
                return name.Contains("Hà Nội", StringComparison.OrdinalIgnoreCase) ||
                       name.Contains("Miền Bắc", StringComparison.OrdinalIgnoreCase);
            });

            bool hasCentral = grouped.Any(s =>
            {
                var name = (s.Region.RegionName ?? string.Empty) + " " + (s.Region.Province ?? string.Empty);
                return centralKeywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase)) ||
                       (s.Region.RegionName ?? string.Empty).Contains("Miền Trung", StringComparison.OrdinalIgnoreCase);
            });

            bool hasSouth = grouped.Any(s =>
            {
                var name = (s.Region.RegionName ?? string.Empty) + " " + (s.Region.Province ?? string.Empty);
                return name.Contains("Hồ Chí Minh", StringComparison.OrdinalIgnoreCase) ||
                       name.Contains("Sài Gòn", StringComparison.OrdinalIgnoreCase) ||
                       (s.Region.RegionName ?? string.Empty).Contains("Miền Nam", StringComparison.OrdinalIgnoreCase) ||
                       mekongKeywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase));
            });

            var nextGoals = new List<string>();
            if (regionsCount < 3)
            {
                var need = 3 - regionsCount;
                nextGoals.Add($"Khám phá thêm {need} vùng nữa để lên cấp Du khách.");
            }
            else if (regionsCount < 5)
            {
                var need = 5 - regionsCount;
                nextGoals.Add($"Mua thêm {need} vùng nữa để đạt huy hiệu Nhà thám hiểm.");
            }

            var visitedMien = (hasNorth ? 1 : 0) + (hasCentral ? 1 : 0) + (hasSouth ? 1 : 0);
            if (visitedMien < 3)
            {
                var missing = new List<string>();
                if (!hasNorth) missing.Add("Bắc");
                if (!hasCentral) missing.Add("Trung");
                if (!hasSouth) missing.Add("Nam");
                nextGoals.Add($"Hoàn thành đủ 3 miền: {string.Join(", ", missing)}.");
            }

            return new HomeJourneySummaryViewModel
            {
                JourneyLevelName = journeyLevelName,
                RegionsCount = regionsCount,
                HasNorth = hasNorth,
                HasCentral = hasCentral,
                HasSouth = hasSouth,
                NextGoals = nextGoals.Take(2).ToList()
            };
        }

        private async Task<List<Product>> GetFeaturedProducts(int take = 20)
        {
            return await _context.Products
                .Include(p => p.ProductImages)
                .Include(p => p.Region)
                .Where(p => p.ApprovalStatus == "Approved")
                .OrderByDescending(p => p.CreatedDate)
                .Take(take)
                .ToListAsync();
        }

        private async Task<List<Product>> GetAdvertisedProducts(int take = 20)
        {
            var now = DateTime.Now;
            return await _context.Products
                .Include(p => p.ProductImages)
                .Include(p => p.Region)
                .Where(p => p.ApprovalStatus == "Approved" &&
                            (p.AdvertisingBudget ?? 0) > 0 &&
                            (!p.AdvertisingEndDate.HasValue || p.AdvertisingEndDate >= now))
                .OrderByDescending(p => p.AdvertisingBudget ?? 0)
                .ThenByDescending(p => p.AdvertisingPaidDate ?? p.CreatedDate)
                .Take(take)
                .ToListAsync();
        }

        private async Task<(List<Product> Products, string? LocationLabel)> GetProductsForLocationOrFeatured(string? location, int take = 8)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return (await GetFeaturedProducts(take), null);
            }

            var key = location.Trim();

            // Lấy tất cả Regions rồi lọc in-memory bằng StringComparison (EF không dịch được Contains với StringComparison).
            var allRegions = await _context.Regions.AsNoTracking().ToListAsync();

            var region = allRegions
                .OrderByDescending(r => r.Province != null && r.Province.Contains(key, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(r => r.RegionName != null && r.RegionName.Contains(key, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(r =>
                    (r.Province != null && r.Province.Contains(key, StringComparison.OrdinalIgnoreCase)) ||
                    (r.RegionName != null && r.RegionName.Contains(key, StringComparison.OrdinalIgnoreCase)));

            if (region == null)
            {
                // Fallback: strip common prefixes then try again (DB-side).
                var cleaned = key
                    .Replace("Thành phố", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("TP.", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("Tỉnh", "", StringComparison.OrdinalIgnoreCase)
                    .Trim();

                if (!string.Equals(cleaned, key, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(cleaned))
                {
                    region = allRegions.FirstOrDefault(r =>
                        (r.Province != null && r.Province.Contains(cleaned, StringComparison.OrdinalIgnoreCase)) ||
                        (r.RegionName != null && r.RegionName.Contains(cleaned, StringComparison.OrdinalIgnoreCase)));
                }
            }

            if (region == null)
            {
                // Final fallback: accent-insensitive match in-memory (handles "Ha Noi" vs "Hà Nội").
                var normKey = TextSearch.Normalize(key);

                region = allRegions.FirstOrDefault(r =>
                    TextSearch.ContainsNormalized(normKey, r.Province) ||
                    TextSearch.ContainsNormalized(normKey, r.RegionName));
            }

            if (region == null)
            {
                return (await GetFeaturedProducts(take), null);
            }

            var products = await _context.Products
                .Include(p => p.ProductImages)
                .Include(p => p.Region)
                .Where(p => p.RegionId == region.RegionId && p.ApprovalStatus == "Approved")
                .OrderByDescending(p => p.CreatedDate)
                .Take(take)
                .ToListAsync();

            // If vùng có nhưng chưa có sản phẩm thì fallback sang search toàn cục theo từ khoá.
            if (!products.Any())
            {
                var normKey = TextSearch.Normalize(key);
                var allProducts = await _context.Products
                    .Include(p => p.ProductImages)
                    .Include(p => p.Region)
                    .Where(p => p.ApprovalStatus == "Approved")
                    .OrderByDescending(p => p.CreatedDate)
                    .ToListAsync();

                products = allProducts
                    .Where(p =>
                        TextSearch.ContainsNormalized(normKey, p.ProductName) ||
                        TextSearch.ContainsNormalized(normKey, p.Description) ||
                        TextSearch.ContainsNormalized(normKey, p.Region?.RegionName) ||
                        TextSearch.ContainsNormalized(normKey, p.Region?.Province))
                    .Take(take)
                    .ToList();
            }

            var label = region.Province ?? region.RegionName ?? key;
            return (products, label);
        }

        [HttpGet]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> RegionSuggestions(string location)
        {
            var (products, label) = await GetProductsForLocationOrFeatured(location, take: 8);
            ViewBag.Location = string.IsNullOrWhiteSpace(label) ? "Gợi ý nổi bật" : label;
            return PartialView("_RegionSuggestions", products);
        }

        [HttpGet]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> ReverseGeocode(double lat, double lng)
        {
            try
            {
                var (location, countryCode) = await ReverseGeocodeLocation(lat, lng);
                return Json(new { location, countryCode });
            }
            catch
            {
                return Json(new { location = (string?)null, countryCode = (string?)null });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GeoIp()
        {
            // Fallback when browser geolocation is blocked (non-HTTPS, permission denied, etc.).
            // Uses GeoIP providers server-side to avoid browser tracking prevention / CORS.
            // Adds cache + provider fallback to reduce rate limits (429).
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var cacheKey = $"geoip:{ip}";
            if (_cache.TryGetValue<(double? lat, double? lng, string? location)>(cacheKey, out var cached))
            {
                return Json(new { lat = cached.lat, lng = cached.lng, location = cached.location });
            }

            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("EXE101-NguonViet/1.0 (contact: support@nguonviet.vn)");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            http.Timeout = TimeSpan.FromSeconds(6);

            static bool TryGetDouble(JsonElement el, out double value)
            {
                value = default;
                try
                {
                    if (el.ValueKind == JsonValueKind.Number) { value = el.GetDouble(); return true; }
                    if (el.ValueKind == JsonValueKind.String && double.TryParse(el.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return true;
                }
                catch { }
                return false;
            }

            async Task<(double? lat, double? lng, string? location, string? countryCode)> FetchIpApiCo()
            {
                var json = await http.GetStringAsync("https://ipapi.co/json/");
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                double? lat = root.TryGetProperty("latitude", out var latEl) && TryGetDouble(latEl, out var latV) ? latV : (double?)null;
                double? lng = root.TryGetProperty("longitude", out var lngEl) && TryGetDouble(lngEl, out var lngV) ? lngV : (double?)null;
                var region = root.TryGetProperty("region", out var regEl) ? regEl.GetString() : null;
                var city = root.TryGetProperty("city", out var cityEl) ? cityEl.GetString() : null;
                var country = root.TryGetProperty("country_code", out var ccEl) ? ccEl.GetString() : null;
                return (lat, lng, region ?? city, country?.ToUpperInvariant());
            }

            async Task<(double? lat, double? lng, string? location, string? countryCode)> FetchIpWhoIs()
            {
                var json = await http.GetStringAsync("https://ipwho.is/");
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("success", out var okEl) && okEl.ValueKind == JsonValueKind.False) return (null, null, null, null);
                double? lat = root.TryGetProperty("latitude", out var latEl) && TryGetDouble(latEl, out var latV) ? latV : (double?)null;
                double? lng = root.TryGetProperty("longitude", out var lngEl) && TryGetDouble(lngEl, out var lngV) ? lngV : (double?)null;
                var region = root.TryGetProperty("region", out var regEl) ? regEl.GetString() : null;
                var city = root.TryGetProperty("city", out var cityEl) ? cityEl.GetString() : null;
                var country = root.TryGetProperty("country_code", out var ccEl) ? ccEl.GetString() : null;
                return (lat, lng, region ?? city, country?.ToUpperInvariant());
            }

            async Task<(double? lat, double? lng, string? location, string? countryCode)> FetchIpApiCom()
            {
                var json = await http.GetStringAsync("https://ip-api.com/json/?fields=status,country,countryCode,regionName,city,lat,lon");
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var status = root.TryGetProperty("status", out var stEl) ? stEl.GetString() : null;
                if (!string.Equals(status, "success", StringComparison.OrdinalIgnoreCase)) return (null, null, null, null);
                double? lat = root.TryGetProperty("lat", out var latEl) && TryGetDouble(latEl, out var latV) ? latV : (double?)null;
                double? lng = root.TryGetProperty("lon", out var lngEl) && TryGetDouble(lngEl, out var lngV) ? lngV : (double?)null;
                var region = root.TryGetProperty("regionName", out var regEl) ? regEl.GetString() : null;
                var city = root.TryGetProperty("city", out var cityEl) ? cityEl.GetString() : null;
                var country = root.TryGetProperty("countryCode", out var ccEl) ? ccEl.GetString() : null;
                return (lat, lng, region ?? city, country?.ToUpperInvariant());
            }

            var providers = new Func<Task<(double? lat, double? lng, string? location, string? countryCode)>>[]
            {
                FetchIpApiCo,
                FetchIpWhoIs,
                FetchIpApiCom
            };

            (double? lat, double? lng, string? location) result = (null, null, null);
            foreach (var p in providers)
            {
                try
                {
                    var r = await p();
                    if (r.lat.HasValue && r.lng.HasValue)
                    {
                        result = (r.lat, r.lng, r.location);
                        break;
                    }
                }
                catch
                {
                    // try next provider
                }
            }

            _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(result.lat.HasValue ? 30 : 5)
            });

            return Json(new { lat = result.lat, lng = result.lng, location = result.location });
        }

        [HttpGet]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> RegionSuggestionsByLatLng(double lat, double lng)
        {
            // Fallback endpoint: always return something even if reverse geocode is blocked.
            // Try reverse geocode first; if it fails, show featured products.
            try
            {
                var (location, _) = await ReverseGeocodeLocation(lat, lng);
                if (!string.IsNullOrWhiteSpace(location))
                {
                    return await RegionSuggestions(location);
                }
            }
            catch
            {
                // ignore, fallback below
            }

            // Không reverse được thì vẫn trả danh sách nổi bật, nhưng đổi nhãn theo tọa độ
            // để user thấy click/định vị đã có tác động (và giúp debug).
            var latKey = Math.Round(lat, 4).ToString("0.####", CultureInfo.InvariantCulture);
            var lngKey = Math.Round(lng, 4).ToString("0.####", CultureInfo.InvariantCulture);
            ViewBag.Location = $"Gợi ý quanh ({latKey}, {lngKey})";
            return PartialView("_RegionSuggestions", await GetFeaturedProducts(8));
        }

        private async Task<(string? location, string? countryCode)> ReverseGeocodeLocation(double lat, double lng)
        {
            // Free-first: cache + Nominatim (Google chỉ dùng nếu bạn tự bật key).
            var latKey = Math.Round(lat, 4).ToString("0.####", CultureInfo.InvariantCulture);
            var lngKey = Math.Round(lng, 4).ToString("0.####", CultureInfo.InvariantCulture);
            var cacheKey = $"revgeo:{latKey}:{lngKey}";
            if (_cache.TryGetValue<(string? location, string? countryCode)>(cacheKey, out var cached))
            {
                return cached;
            }

            // Prefer Google (ổn định hơn) nếu có API key, fallback sang Nominatim.
            var apiKey = _config["GoogleMaps:ApiKey"];
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                using var http = new HttpClient();
                var latStr = lat.ToString("0.######", CultureInfo.InvariantCulture);
                var lngStr = lng.ToString("0.######", CultureInfo.InvariantCulture);
                var url = $"https://maps.googleapis.com/maps/api/geocode/json?latlng={latStr},{lngStr}&language=vi&key={Uri.EscapeDataString(apiKey)}";
                var json = await http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var status = root.TryGetProperty("status", out var st) ? st.GetString() : null;
                if (string.Equals(status, "OK", StringComparison.OrdinalIgnoreCase) &&
                    root.TryGetProperty("results", out var results) &&
                    results.ValueKind == JsonValueKind.Array &&
                    results.GetArrayLength() > 0)
                {
                    string? admin1 = null;
                    string? locality = null;
                    string? countryShort = null;

                    foreach (var r in results.EnumerateArray())
                    {
                        if (!r.TryGetProperty("address_components", out var comps) || comps.ValueKind != JsonValueKind.Array) continue;
                        foreach (var c in comps.EnumerateArray())
                        {
                            if (!c.TryGetProperty("types", out var types) || types.ValueKind != JsonValueKind.Array) continue;
                            var shortName = c.TryGetProperty("short_name", out var sn) ? sn.GetString() : null;
                            var longName = c.TryGetProperty("long_name", out var ln) ? ln.GetString() : null;

                            foreach (var t in types.EnumerateArray())
                            {
                                var type = t.GetString();
                                if (admin1 == null && string.Equals(type, "administrative_area_level_1", StringComparison.OrdinalIgnoreCase))
                                {
                                    admin1 = longName;
                                }
                                else if (locality == null && (string.Equals(type, "locality", StringComparison.OrdinalIgnoreCase) ||
                                                             string.Equals(type, "administrative_area_level_2", StringComparison.OrdinalIgnoreCase)))
                                {
                                    locality = longName;
                                }
                                else if (countryShort == null && string.Equals(type, "country", StringComparison.OrdinalIgnoreCase))
                                {
                                    countryShort = shortName?.ToUpperInvariant();
                                }
                            }
                        }

                        if (admin1 != null && countryShort != null) break;
                    }

                    var location = admin1 ?? locality;
                    var result = (location, countryShort);
                    _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
                    });
                    return result;
                }
            }

            // Nominatim fallback
            {
                var result = await ReverseGeocodeNominatimWithRetry(lat, lng);
                // Cache cả null để giảm spam call khi Nominatim đang nghẽn
                _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = result.location == null ? TimeSpan.FromMinutes(10) : TimeSpan.FromDays(7)
                });
                return result;
            }
        }

        private static async Task<(string? location, string? countryCode)> ReverseGeocodeNominatimWithRetry(double lat, double lng)
        {
            // Nominatim có rate limit: retry nhẹ nếu gặp lỗi tạm thời.
            var delays = new[] { 0, 800 };
            Exception? last = null;
            foreach (var d in delays)
            {
                if (d > 0) await Task.Delay(d);
                try
                {
                    var url = $"https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat={lat.ToString("0.######", CultureInfo.InvariantCulture)}&lon={lng.ToString("0.######", CultureInfo.InvariantCulture)}&accept-language=vi";
                    using var http = new HttpClient();
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("EXE101-NguonViet/1.0 (contact: support@nguonviet.vn)");
                    http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                    http.Timeout = TimeSpan.FromSeconds(6);

                    using var res = await http.GetAsync(url);
                    if ((int)res.StatusCode == 429 || (int)res.StatusCode >= 500)
                    {
                        last = new HttpRequestException($"nominatim status {(int)res.StatusCode}");
                        continue;
                    }
                    res.EnsureSuccessStatusCode();
                    var json = await res.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (!doc.RootElement.TryGetProperty("address", out var addr))
                    {
                        return (null, null);
                    }

                    string? GetProp(string name) => addr.TryGetProperty(name, out var v) ? v.GetString() : null;
                    var countryCode = GetProp("country_code")?.ToUpperInvariant();
                    var location =
                        GetProp("state") ??
                        GetProp("city") ??
                        GetProp("town") ??
                        GetProp("village");
                    return (location, countryCode);
                }
                catch (Exception ex)
                {
                    last = ex;
                }
            }

            _ = last;
            return (null, null);
        }
    }
}
