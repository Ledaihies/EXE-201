using EXE.Models;
using EXE.Services;
using EXE.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace EXE.Controllers;

public class SellerController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly BankTransferSettings _bankTransferSettings;
    private readonly IBankTransferService _bankTransferService;
    private readonly IInvoicePdfService _invoicePdfService;
    private readonly IGHNService _ghnService;
    private readonly IWalletService _walletService;
    private readonly ISePayTransactionLookupService _sePayLookupService;
    private static readonly List<AdvertisingPackageOption> AdvertisingPackages = new()
    {
        new("none", "Không quảng cáo", "Sản phẩm chỉ hiển thị trên sàn bình thường.", 0m, 0),
        new("banner_1d", "Banner 1 ngày", "Hiển thị trên popup/banner và thanh quảng cáo trang chủ trong 1 ngày.", 23000m, 1),
        new("banner_3d", "Banner 3 ngày", "Gói thử nghiệm 3 ngày trên banner và thanh quảng cáo.", 65000m, 3),
        new("banner_7d", "Banner 7 ngày", "Phù hợp sản phẩm mới cần tăng nhận diện trong tuần đầu.", 149000m, 7),
        new("banner_14d", "Banner 14 ngày", "Ưu tiên hiển thị dài hơn cho sản phẩm đang bán tốt.", 279000m, 14),
        new("banner_30d", "Banner 30 ngày", "Gói tháng cho shop muốn duy trì hiện diện trên trang chủ.", 529000m, 30)
    };

    public SellerController(
        ApplicationDbContext context,
        IWebHostEnvironment env,
        IOptions<BankTransferSettings> bankTransferSettings,
        IBankTransferService bankTransferService,
        IInvoicePdfService invoicePdfService,
        IGHNService ghnService,
        IWalletService walletService,
        ISePayTransactionLookupService sePayLookupService)
    {
        _context = context;
        _env = env;
        _bankTransferSettings = bankTransferSettings.Value;
        _bankTransferService = bankTransferService;
        _invoicePdfService = invoicePdfService;
        _ghnService = ghnService;
        _walletService = walletService;
        _sePayLookupService = sePayLookupService;
    }

    public async Task<IActionResult> Index()
    {
        if (!EnsureSellerAccess(out var accessResult)) return accessResult!;

        var sellerId = CurrentUserId();
        var products = await SellerProducts(sellerId)
            .Include(p => p.ProductImages)
            .Include(p => p.Category)
            .Include(p => p.Region)
            .OrderByDescending(p => p.CreatedDate)
            .ToListAsync();

        ViewBag.ProductCount = products.Count;
        ViewBag.TotalStock = products.Sum(p => p.Stock ?? 0);
        ViewBag.OrderCount = await _context.OrderItems
            .Where(oi => oi.Product != null && oi.Product.SellerId == sellerId)
            .Select(oi => oi.OrderId)
            .Distinct()
            .CountAsync();
        ViewBag.TotalViews = products.Sum(p => p.ViewCount ?? 0);

        var orderItems = await _context.OrderItems
            .Include(oi => oi.Product)
            .Include(oi => oi.Order)
                .ThenInclude(o => o!.Status)
            .Where(oi => oi.Product != null && oi.Product.SellerId == sellerId)
            .ToListAsync();

        var revenueItems = orderItems
            .Where(oi => string.Equals(oi.Order?.Status?.StatusName, "Da nhan hang", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var sellerWalletId = await _context.Wallets
            .Where(w => w.UserId == sellerId)
            .Select(w => (int?)w.WalletId)
            .FirstOrDefaultAsync();
        var shippingRefundTotal = sellerWalletId.HasValue
            ? await _context.WalletTransactions
                .Where(t => t.WalletId == sellerWalletId.Value && t.Type == "ShippingRefund")
                .SumAsync(t => (decimal?)t.Amount) ?? 0m
            : 0m;

        ViewBag.TotalRevenue = revenueItems.Sum(oi => (oi.Price ?? 0m) * (oi.Quantity ?? 0));
        ViewBag.TotalShippingRefund = shippingRefundTotal;
        ViewBag.CompletedOrderCount = revenueItems.Select(oi => oi.OrderId).Distinct().Count();
        ViewBag.PendingOrderCount = orderItems.Count(oi => oi.Order?.Status?.StatusName == "Cho nguoi ban xac nhan");
        ViewBag.PickupOrderCount = orderItems.Count(oi => oi.Order?.Status?.StatusName == "Cho lay hang");
        ViewBag.ShippingOrderCount = orderItems.Count(oi => oi.Order?.Status?.StatusName == "Cho giao hang");
        ViewBag.CancelledOrderCount = orderItems.Count(oi => oi.Order?.Status?.StatusName == "Da huy");
        ViewBag.AdvertisingTotal = products.Sum(p => p.AdvertisingBudget ?? 0m);
        ViewBag.ApprovedProductCount = products.Count(p => p.ApprovalStatus == "Approved");
        ViewBag.PendingProductCount = products.Count(p => p.ApprovalStatus != "Approved" && p.ApprovalStatus != "Rejected");

        var seller = await _context.Users
            .Where(u => u.UserId == sellerId)
            .Select(u => new
            {
                u.BankName,
                u.BankAccountNumber,
                u.BankAccountName,
                u.BankBranch,
                u.BankUpdatedDate
            })
            .FirstOrDefaultAsync();

        ViewBag.SellerBankName = seller?.BankName ?? string.Empty;
        ViewBag.SellerBankAccountNumber = seller?.BankAccountNumber ?? string.Empty;
        ViewBag.SellerBankAccountName = seller?.BankAccountName ?? string.Empty;
        ViewBag.SellerBankBranch = seller?.BankBranch ?? string.Empty;
        ViewBag.SellerBankUpdatedDate = seller?.BankUpdatedDate;

        var topProducts = products
            .Select(p => new
            {
                Name = string.IsNullOrWhiteSpace(p.ProductName) ? $"SP #{p.ProductId}" : p.ProductName,
                Views = p.ViewCount ?? 0,
                Revenue = revenueItems
                    .Where(oi => oi.ProductId == p.ProductId)
                    .Sum(oi => (oi.Price ?? 0m) * (oi.Quantity ?? 0))
            })
            .OrderByDescending(p => p.Revenue)
            .ThenByDescending(p => p.Views)
            .Take(8)
            .ToList();

        ViewBag.ChartLabelsJson = JsonSerializer.Serialize(topProducts.Select(p => p.Name));
        ViewBag.RevenueDataJson = JsonSerializer.Serialize(topProducts.Select(p => p.Revenue));
        ViewBag.ViewDataJson = JsonSerializer.Serialize(topProducts.Select(p => p.Views));

        return View(products);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateBankAccount(string? bankName, string? bankAccountNumber, string? bankAccountName, string? bankBranch)
    {
        if (!EnsureSellerAccess(out var accessResult)) return accessResult!;

        bankName = bankName?.Trim();
        bankAccountNumber = bankAccountNumber?.Trim();
        bankAccountName = bankAccountName?.Trim();
        bankBranch = bankBranch?.Trim();

        if (string.IsNullOrWhiteSpace(bankName) ||
            string.IsNullOrWhiteSpace(bankAccountNumber) ||
            string.IsNullOrWhiteSpace(bankAccountName))
        {
            TempData["SellerMessage"] = "Vui long nhap day du ten ngan hang, so tai khoan va ten chu tai khoan.";
            return RedirectToAction(nameof(Index));
        }

        if (bankName.Length > 120 || bankAccountNumber.Length > 50 || bankAccountName.Length > 150 || (bankBranch?.Length ?? 0) > 150)
        {
            TempData["SellerMessage"] = "Thong tin tai khoan ngan hang vuot qua do dai cho phep.";
            return RedirectToAction(nameof(Index));
        }

        var seller = await _context.Users.FirstOrDefaultAsync(u => u.UserId == CurrentUserId());
        if (seller == null) return NotFound();

        seller.BankName = bankName;
        seller.BankAccountNumber = bankAccountNumber;
        seller.BankAccountName = bankAccountName;
        seller.BankBranch = string.IsNullOrWhiteSpace(bankBranch) ? null : bankBranch;
        seller.BankUpdatedDate = DateTime.Now;
        await _context.SaveChangesAsync();

        TempData["SellerMessage"] = "Da cap nhat tai khoan ngan hang de admin chuyen tien.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Create()
    {
        if (!EnsureSellerAccess(out var accessResult)) return accessResult!;

        await LoadCatalogOptions();
        return View(new Product());
    }

    [HttpPost]
    public async Task<IActionResult> Create(Product input, IFormFile? imageFile, IFormFile? originProofFile, string? advertisingPackageCode)
    {
        if (!EnsureSellerAccess(out var accessResult)) return accessResult!;

        if (string.IsNullOrWhiteSpace(input.ProductName) || !input.Price.HasValue || input.Price < 0)
        {
            ViewBag.Error = "Vui lòng nhập tên sản phẩm và giá hợp lệ.";
            await LoadCatalogOptions();
            return View(input);
        }

        var adPackage = await ResolveAdvertisingPackage(advertisingPackageCode);
        if (originProofFile == null || originProofFile.Length <= 0)
        {
            ViewBag.Error = "Người bán phải tải ảnh chứng minh nguồn gốc xuất xứ của sản phẩm.";
            await LoadCatalogOptions();
            return View(input);
        }

        var originProofFileName = await SaveUploadedImage(originProofFile);
        if (string.IsNullOrWhiteSpace(originProofFileName))
        {
            ViewBag.Error = "Ảnh chứng minh nguồn gốc phải là file ảnh hợp lệ (.jpg, .png, .gif, .webp).";
            await LoadCatalogOptions();
            return View(input);
        }

        var product = new Product
        {
            ProductName = input.ProductName?.Trim(),
            Description = input.Description,
            Price = input.Price,
            Stock = input.Stock ?? 0,
            CategoryId = input.CategoryId,
            RegionId = input.RegionId,
            SellerId = CurrentUserId(),
            ApprovalStatus = "Pending",
            OriginProofImageUrl = originProofFileName,
            AdvertisingBudget = 0,
            AdvertisingPackageCode = null,
            AdvertisingPackageName = null,
            AdvertisingDays = null,
            AdvertisingPaidDate = null,
            AdvertisingEndDate = null,
            CreatedDate = DateTime.Now
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        await SaveProductImage(product.ProductId, imageFile);
        if (adPackage.Price > 0)
        {
            var request = await CreateAdvertisingPaymentRequest(product.ProductId, CurrentUserId(), adPackage);
            return RedirectToAction(nameof(AdvertisingPayment), new { id = request.RequestId });
        }
        TempData["SellerMessage"] = "Đã gửi sản phẩm cho admin duyệt. Sản phẩm sẽ lên sàn sau khi được phê duyệt.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        if (!EnsureSellerAccess(out var accessResult)) return accessResult!;

        var sellerId = CurrentUserId();
        var product = await SellerProducts(sellerId)
            .Include(p => p.ProductImages)
            .FirstOrDefaultAsync(p => p.ProductId == id);

        if (product == null) return NotFound();

        await LoadCatalogOptions();
        return View(product);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Product input, IFormFile? imageFile, IFormFile? originProofFile, string? advertisingPackageCode)
    {
        if (!EnsureSellerAccess(out var accessResult)) return accessResult!;

        var sellerId = CurrentUserId();
        var product = await SellerProducts(sellerId)
            .Include(p => p.ProductImages)
            .FirstOrDefaultAsync(p => p.ProductId == input.ProductId);

        if (product == null) return NotFound();

        if (string.IsNullOrWhiteSpace(input.ProductName) || !input.Price.HasValue || input.Price < 0)
        {
            ViewBag.Error = "Vui lòng nhập tên sản phẩm và giá hợp lệ.";
            await LoadCatalogOptions();
            return View(product);
        }

        var adPackage = await ResolveAdvertisingPackage(advertisingPackageCode);
        product.ProductName = input.ProductName?.Trim();
        product.Description = input.Description;
        product.Price = input.Price;
        product.Stock = input.Stock ?? 0;
        product.CategoryId = input.CategoryId;
        product.RegionId = input.RegionId;
        product.ApprovalStatus = "Pending";
        if (adPackage.Price > 0)
        {
            var request = await CreateAdvertisingPaymentRequest(product.ProductId, sellerId, adPackage);
            await SaveProductImage(product.ProductId, imageFile);
            if (originProofFile != null && originProofFile.Length > 0)
            {
                product.OriginProofImageUrl = await SaveUploadedImage(originProofFile);
            }
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(AdvertisingPayment), new { id = request.RequestId });
        }
        else if (adPackage.Price <= 0)
        {
            product.AdvertisingBudget = 0;
            product.AdvertisingPackageCode = null;
            product.AdvertisingPackageName = null;
            product.AdvertisingDays = null;
            product.AdvertisingPaidDate = null;
            product.AdvertisingEndDate = null;
        }

        await SaveProductImage(product.ProductId, imageFile);
        if (originProofFile != null && originProofFile.Length > 0)
        {
            product.OriginProofImageUrl = await SaveUploadedImage(originProofFile);
        }
        await _context.SaveChangesAsync();

        TempData["SellerMessage"] = "Đã cập nhật sản phẩm và gửi lại cho admin duyệt.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> AdvertisingPayment(int id)
    {
        if (!EnsureSellerAccess(out var accessResult)) return accessResult!;

        var sellerId = CurrentUserId();
        var roleName = HttpContext.Session.GetString("RoleName") ?? string.Empty;
        var request = await _context.AdvertisingPaymentRequests
            .Include(r => r.Product)
            .FirstOrDefaultAsync(r => r.RequestId == id &&
                                      (r.SellerId == sellerId || string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase)));

        if (request == null) return NotFound();

        var paymentCode = string.IsNullOrWhiteSpace(request.TransferContent) ? $"QC{request.RequestId}" : request.TransferContent;
        return View(new AdvertisingBankTransferViewModel
        {
            Request = request,
            PaymentCode = paymentCode,
            QrUrl = _bankTransferService.BuildVietQrUrl(request.Amount, paymentCode),
            IsPaid = string.Equals(request.Status, "Paid", StringComparison.OrdinalIgnoreCase)
        });
    }

    [HttpGet]
    public async Task<IActionResult> AdvertisingPaymentStatus(int id)
    {
        if (!EnsureSellerAccess(out var accessResult)) return accessResult!;

        var sellerId = CurrentUserId();
        var roleName = HttpContext.Session.GetString("RoleName") ?? string.Empty;
        var request = await _context.AdvertisingPaymentRequests
            .Include(r => r.Product)
            .FirstOrDefaultAsync(r =>
                r.RequestId == id &&
                (r.SellerId == sellerId || string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase)));
        if (request == null) return NotFound();

        var isPaid = string.Equals(request.Status, "Paid", StringComparison.OrdinalIgnoreCase);
        if (!isPaid)
        {
            var paymentCode = string.IsNullOrWhiteSpace(request.TransferContent) ? $"QC{request.RequestId}" : request.TransferContent;
            var found = await _sePayLookupService.HasIncomingTransactionAsync(paymentCode, request.Amount, request.CreatedDate);
            if (found && request.Product != null)
            {
                request.Status = "Paid";
                request.ConfirmedDate = DateTime.Now;
                request.Product.AdvertisingBudget = request.Amount;
                request.Product.AdvertisingPackageCode = request.PackageCode;
                request.Product.AdvertisingPackageName = request.PackageName;
                request.Product.AdvertisingDays = request.Days;
                request.Product.AdvertisingPaidDate = DateTime.Now;
                request.Product.AdvertisingEndDate = DateTime.Now.AddDays(request.Days);
                await _context.SaveChangesAsync();
                isPaid = true;
            }
        }

        return Json(new { isPaid });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        if (!EnsureSellerAccess(out var accessResult)) return accessResult!;

        var sellerId = CurrentUserId();
        var product = await SellerProducts(sellerId).FirstOrDefaultAsync(p => p.ProductId == id);
        if (product != null)
        {
            var hasOrderItems = await _context.OrderItems.AnyAsync(oi => oi.ProductId == product.ProductId);
            if (hasOrderItems)
            {
                product.ApprovalStatus = "Deleted";
                product.Stock = 0;
                product.AdvertisingBudget = 0;
                product.AdvertisingPackageCode = null;
                product.AdvertisingPackageName = null;
                product.AdvertisingDays = null;
                product.AdvertisingPaidDate = null;
                product.AdvertisingEndDate = null;
                await _context.SaveChangesAsync();
                TempData["SellerMessage"] = "Đã ẩn sản phẩm khỏi gian hàng. Sản phẩm có đơn hàng nên không xóa cứng khỏi dữ liệu.";
                return RedirectToAction(nameof(Index));
            }

            var productImages = await _context.ProductImages.Where(pi => pi.ProductId == product.ProductId).ToListAsync();
            var cartItems = await _context.CartItems.Where(ci => ci.ProductId == product.ProductId).ToListAsync();
            var wishlists = await _context.Wishlists.Where(w => w.ProductId == product.ProductId).ToListAsync();
            var reviews = await _context.Reviews.Where(r => r.ProductId == product.ProductId).ToListAsync();

            _context.ProductImages.RemoveRange(productImages);
            _context.CartItems.RemoveRange(cartItems);
            _context.Wishlists.RemoveRange(wishlists);
            _context.Reviews.RemoveRange(reviews);
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();
            TempData["SellerMessage"] = "Đã xóa sản phẩm.";
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Orders()
    {
        if (!EnsureSellerAccess(out var accessResult)) return accessResult!;

        var sellerId = CurrentUserId();
        var items = await _context.OrderItems
            .Include(oi => oi.Product)
            .Include(oi => oi.Order)
                .ThenInclude(o => o!.User)
            .Include(oi => oi.Order)
                .ThenInclude(o => o!.Status)
            .Include(oi => oi.Order)
                .ThenInclude(o => o!.OrderShippings)
            .Include(oi => oi.Order)
                .ThenInclude(o => o!.ReturnRequests)
            .Where(oi => oi.Product != null && oi.Product.SellerId == sellerId)
            .OrderByDescending(oi => oi.Order!.OrderDate)
            .ToListAsync();

        return View(items);
    }

    [HttpPost]
    public async Task<IActionResult> ConfirmOrder(int id, string? shippingChoice)
    {
        if (!EnsureSellerAccess(out var accessResult)) return accessResult!;

        var requestGhnPickup = !string.Equals(shippingChoice, "dropoff", StringComparison.OrdinalIgnoreCase);
        return await MoveSellerOrder(id, "Cho nguoi ban xac nhan", "Cho lay hang", createGhnOrder: requestGhnPickup);
    }

    [HttpPost]
    public async Task<IActionResult> HandOverOrder(int id)
    {
        if (!EnsureSellerAccess(out var accessResult)) return accessResult!;

        return await MoveSellerOrder(id, "Cho lay hang", "Cho giao hang");
    }

    public async Task<IActionResult> InvoicePdf(int id)
    {
        if (!EnsureSellerAccess(out var accessResult)) return accessResult!;

        var sellerId = CurrentUserId();
        var roleName = HttpContext.Session.GetString("RoleName") ?? string.Empty;
        var isAdmin = string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase);

        var order = await _context.Orders
            .Include(o => o.User)
            .Include(o => o.Status)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p!.Seller)
            .FirstOrDefaultAsync(o => o.OrderId == id);

        if (order == null) return NotFound();

        var sellerItems = order.OrderItems
            .Where(oi => oi.Product != null && (isAdmin || oi.Product.SellerId == sellerId))
            .ToList();

        if (!sellerItems.Any()) return NotFound();

        var seller = isAdmin
            ? sellerItems.First().Product?.Seller
            : await _context.Users.FirstOrDefaultAsync(u => u.UserId == sellerId);

        if (seller == null) return NotFound();

        var pdf = _invoicePdfService.CreateSellerInvoice(order, seller, sellerItems);
        return File(pdf, "application/pdf", $"hoa-don-{order.OrderId:000000}.pdf");
    }

    [HttpPost]
    public async Task<IActionResult> ConfirmReturn(int id)
    {
        if (!EnsureSellerAccess(out var accessResult)) return accessResult!;

        var sellerId = CurrentUserId();
        var order = await _context.Orders
            .Include(o => o.Status)
            .Include(o => o.ReturnRequests)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.OrderId == id &&
                                      o.OrderItems.Any(oi => oi.Product != null && oi.Product.SellerId == sellerId));

        if (order == null) return NotFound();

        var request = order.ReturnRequests
            .OrderByDescending(r => r.CreatedDate)
            .FirstOrDefault(r => r.Status == "Pending");

        if (request == null || !string.Equals(order.Status?.StatusName, "Cho xac nhan hoan hang", StringComparison.OrdinalIgnoreCase))
        {
            TempData["SellerMessage"] = "Don hang khong co yeu cau hoan hang dang cho xac nhan.";
            return RedirectToAction(nameof(Orders));
        }

        request.Status = "Approved";
        request.SellerConfirmedByUserId = sellerId;
        request.SellerConfirmedDate = DateTime.Now;
        request.RefundedDate = DateTime.Now;
        request.RefundAmount = order.TotalAmount ?? request.RefundAmount;
        order.StatusId = await GetStatusIdAsync("Da hoan tien");
        if (order.UserId.HasValue && request.RefundAmount > 0)
        {
            await _walletService.CreditAsync(order.UserId.Value, request.RefundAmount, "Refund", $"Hoàn tiền đơn #{order.OrderId}", order.OrderId);
        }

        var refundMethodId = await GetPaymentMethodIdAsync("Refund");
        var alreadyRefunded = await _context.Payments.AnyAsync(p =>
            p.OrderId == order.OrderId &&
            p.PaymentMethodId == refundMethodId &&
            p.Amount < 0);

        if (!alreadyRefunded)
        {
            _context.Payments.Add(new Payment
            {
                OrderId = order.OrderId,
                PaymentMethodId = refundMethodId,
                Amount = -Math.Abs(request.RefundAmount),
                PaymentDate = DateTime.Now
            });
        }

        await _context.SaveChangesAsync();
        TempData["SellerMessage"] = "Da xac nhan hoan hang va ghi nhan hoan tien cho nguoi mua.";
        return RedirectToAction(nameof(Orders));
    }

    [HttpPost]
    public async Task<IActionResult> RejectReturn(int id)
    {
        if (!EnsureSellerAccess(out var accessResult)) return accessResult!;

        var sellerId = CurrentUserId();
        var order = await _context.Orders
            .Include(o => o.ReturnRequests)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.OrderId == id &&
                                      o.OrderItems.Any(oi => oi.Product != null && oi.Product.SellerId == sellerId));

        if (order == null) return NotFound();

        var request = order.ReturnRequests
            .OrderByDescending(r => r.CreatedDate)
            .FirstOrDefault(r => r.Status == "Pending");

        if (request == null)
        {
            TempData["SellerMessage"] = "Don hang khong co yeu cau hoan hang dang cho xac nhan.";
            return RedirectToAction(nameof(Orders));
        }

        request.Status = "Rejected";
        request.SellerConfirmedByUserId = sellerId;
        request.SellerConfirmedDate = DateTime.Now;
        order.StatusId = await GetStatusIdAsync("Da nhan hang");

        await _context.SaveChangesAsync();
        TempData["SellerMessage"] = "Da tu choi yeu cau hoan hang.";
        return RedirectToAction(nameof(Orders));
    }

    private async Task<IActionResult> MoveSellerOrder(int orderId, string expectedStatus, string nextStatus, bool createGhnOrder = false)
    {
        var sellerId = CurrentUserId();
        var order = await _context.Orders
            .Include(o => o.User)
            .Include(o => o.Status)
            .Include(o => o.OrderShippings)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.OrderId == orderId &&
                                      o.OrderItems.Any(oi => oi.Product != null && oi.Product.SellerId == sellerId));

        if (order == null) return NotFound();

        var current = order.Status?.StatusName ?? string.Empty;
        if (!string.Equals(current, expectedStatus, StringComparison.OrdinalIgnoreCase))
        {
            TempData["SellerMessage"] = "Đơn hàng không ở trạng thái có thể cập nhật.";
            return RedirectToAction(nameof(Orders));
        }

        if (createGhnOrder)
        {
            var sellerItems = order.OrderItems
                .Where(oi => oi.Product != null && oi.Product.SellerId == sellerId)
                .ToList();
            var shippingResult = await CreateGhnShippingOrder(order, sellerItems);
            if (shippingResult != null)
            {
                return shippingResult;
            }
        }

        order.StatusId = await GetStatusIdAsync(nextStatus);
        await _context.SaveChangesAsync();

        if (string.Equals(nextStatus, "Cho giao hang", StringComparison.OrdinalIgnoreCase))
        {
            await CreditSellerShippingRefundAsync(order, sellerId);
        }
        TempData["SellerMessage"] = "Đã cập nhật trạng thái đơn hàng.";
        return RedirectToAction(nameof(Orders));
    }

    private async Task CreditSellerShippingRefundAsync(Order order, int sellerId)
    {
        var shippingFee = GetOrderShippingFee(order);
        if (shippingFee <= 0) return;

        var sellerSubtotal = order.OrderItems
            .Where(oi => oi.Product != null && oi.Product.SellerId == sellerId)
            .Sum(oi => (oi.Price ?? 0m) * (oi.Quantity ?? 0));
        if (sellerSubtotal <= 0) return;

        var orderSubtotal = order.OrderItems.Sum(oi => (oi.Price ?? 0m) * (oi.Quantity ?? 0));
        if (orderSubtotal <= 0) return;

        var refundAmount = orderSubtotal == sellerSubtotal
            ? shippingFee
            : Math.Round(shippingFee * sellerSubtotal / orderSubtotal, 0, MidpointRounding.AwayFromZero);
        if (refundAmount <= 0) return;

        var wallet = await _walletService.GetOrCreateWalletAsync(sellerId);
        var alreadyRefunded = await _context.WalletTransactions.AnyAsync(t =>
            t.WalletId == wallet.WalletId &&
            t.OrderId == order.OrderId &&
            t.Type == "ShippingRefund");
        if (alreadyRefunded) return;

        await _walletService.CreditAsync(
            sellerId,
            refundAmount,
            "ShippingRefund",
            $"Hoan phi giao hang don #{order.OrderId}",
            order.OrderId);
    }

    private static decimal GetOrderShippingFee(Order order)
    {
        if (order.ShippingFee.HasValue)
        {
            return Math.Max(0m, order.ShippingFee.Value);
        }

        var subtotal = order.OrderItems.Sum(oi => (oi.Price ?? 0m) * (oi.Quantity ?? 0));
        return Math.Max(0m, (order.TotalAmount ?? 0m) - subtotal);
    }

    private async Task<IActionResult?> CreateGhnShippingOrder(Order order, List<OrderItem> sellerItems)
    {
        var existingTracking = order.OrderShippings
            .OrderByDescending(x => x.ShippingDate)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.TrackingNumber));
        if (existingTracking != null)
        {
            return null;
        }

        var toDistrictId = order.ToDistrictId.GetValueOrDefault();
        var toWardCode = order.ToWardCode?.Trim() ?? "";
        if (toDistrictId <= 0 || string.IsNullOrWhiteSpace(toWardCode))
        {
            TempData["SellerMessage"] = "Khong the goi GHN: don hang thieu ma quan/huyen hoac phuong/xa GHN cua nguoi nhan.";
            return RedirectToAction(nameof(Orders));
        }

        var receiverName = FirstNonEmpty(order.ReceiverName, order.User?.FullName, "Khach hang");
        var receiverPhone = FirstNonEmpty(order.ReceiverPhone, order.User?.Phone);
        var receiverAddress = order.ShippingAddress?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(receiverPhone) || string.IsNullOrWhiteSpace(receiverAddress))
        {
            TempData["SellerMessage"] = "Khong the goi GHN: don hang thieu ten, so dien thoai hoac dia chi nguoi nhan.";
            return RedirectToAction(nameof(Orders));
        }

        var ghnResult = await _ghnService.CreateOrderAsync(new GHNCreateOrderInput
        {
            ClientOrderCode = $"ORDER-{order.OrderId}",
            ToName = receiverName,
            ToPhone = receiverPhone,
            ToAddress = receiverAddress,
            ToDistrictId = toDistrictId,
            ToWardCode = toWardCode,
            Note = order.ShippingNote ?? "",
            InsuranceValue = sellerItems.Sum(x => (x.Price ?? 0m) * (x.Quantity ?? 0)),
            Items = sellerItems.Select(x => new GHNCreateOrderItem
            {
                Name = x.Product?.ProductName ?? $"SP {x.ProductId}",
                Code = x.ProductId?.ToString() ?? "",
                Quantity = Math.Max(1, x.Quantity ?? 1),
                Price = x.Price ?? 0m
            })
        });

        if (!ghnResult.Success || string.IsNullOrWhiteSpace(ghnResult.OrderCode))
        {
            TempData["SellerMessage"] = "GHN chua tao duoc van don: " + (ghnResult.Message ?? "Loi khong xac dinh.");
            return RedirectToAction(nameof(Orders));
        }

        var methodId = await GetShippingMethodIdAsync("Giao Hang Nhanh");
        _context.OrderShippings.Add(new OrderShipping
        {
            OrderId = order.OrderId,
            ShippingMethodId = methodId,
            TrackingNumber = ghnResult.OrderCode,
            ShippingDate = DateTime.Now
        });
        return null;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? "";
    }

    private int CurrentUserId()
    {
        return HttpContext.Session.GetInt32("UserId") ?? 0;
    }

    private bool EnsureSellerAccess(out IActionResult? result)
    {
        result = null;
        var userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
        {
            result = RedirectToAction("Login", "Auth");
            return false;
        }

        var roleName = HttpContext.Session.GetString("RoleName") ?? string.Empty;
        if (string.Equals(roleName, "Seller", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        TempData["SellerAccessMessage"] = "Bạn chưa đăng ký tài khoản người bán. Vui lòng đăng ký người bán để mở gian hàng.";
        result = RedirectToAction("Login", "Auth");
        return false;
    }

    private IQueryable<Product> SellerProducts(int sellerId)
    {
        var roleName = HttpContext.Session.GetString("RoleName") ?? string.Empty;
        var query = _context.Products.Where(p => p.ApprovalStatus != "Deleted");
        return string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase)
            ? query
            : query.Where(p => p.SellerId == sellerId);
    }

    private async Task LoadCatalogOptions()
    {
        ViewBag.Categories = await _context.Categories.OrderBy(c => c.CategoryName).ToListAsync();
        ViewBag.Regions = await _context.Regions.OrderBy(r => r.RegionName).ToListAsync();
        ViewBag.AdPackages = await GetAdvertisingPackageOptions();
        ViewBag.BankTransferBankId = _bankTransferSettings.BankId;
        ViewBag.BankTransferAccountNo = _bankTransferSettings.AccountNo;
        ViewBag.BankTransferAccountName = _bankTransferSettings.AccountName;
        ViewBag.BankTransferTemplate = string.IsNullOrWhiteSpace(_bankTransferSettings.Template) ? "compact2" : _bankTransferSettings.Template;
    }

    private async Task<List<AdvertisingPackageOption>> GetAdvertisingPackageOptions()
    {
        var packages = await _context.AdvertisingPackages
            .Where(p => p.IsActive)
            .OrderBy(p => p.Price)
            .ThenBy(p => p.Days)
            .Select(p => new AdvertisingPackageOption(p.Code ?? string.Empty, p.Name ?? string.Empty, p.Description ?? string.Empty, p.Price, p.Days))
            .ToListAsync();

        packages.Insert(0, new AdvertisingPackageOption("none", "Không quảng cáo", "Sản phẩm chỉ hiển thị trên sàn bình thường.", 0m, 0));
        return packages;
    }

    private async Task<AdvertisingPackageOption> ResolveAdvertisingPackage(string? code)
    {
        var packages = await GetAdvertisingPackageOptions();
        return packages.FirstOrDefault(p => string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase))
            ?? packages[0];
    }

    public sealed record AdvertisingPackageOption(string Code, string Name, string Description, decimal Price, int Days);

    private async Task<AdvertisingPaymentRequest> CreateAdvertisingPaymentRequest(int productId, int sellerId, AdvertisingPackageOption adPackage)
    {
        var pending = await _context.AdvertisingPaymentRequests
            .Where(r => r.ProductId == productId && r.Status == "Pending")
            .ToListAsync();

        foreach (var item in pending)
        {
            item.Status = "Cancelled";
        }

        var request = new AdvertisingPaymentRequest
        {
            ProductId = productId,
            SellerId = sellerId,
            PackageCode = adPackage.Code,
            PackageName = adPackage.Name,
            Amount = adPackage.Price,
            Days = adPackage.Days,
            Status = "Pending",
            TransferContent = "",
            CreatedDate = DateTime.Now
        };

        _context.AdvertisingPaymentRequests.Add(request);
        await _context.SaveChangesAsync();
        request.TransferContent = $"QC{request.RequestId}";
        await _context.SaveChangesAsync();
        TempData["SellerMessage"] = "Đã tạo yêu cầu quảng cáo. Vui lòng quét VietQR và chuyển đúng nội dung để hệ thống tự xác nhận.";
        return request;
    }

    private async Task<int?> GetStatusIdAsync(string statusName)
    {
        var id = await _context.OrderStatuses
            .Where(s => s.StatusName == statusName)
            .Select(s => (int?)s.StatusId)
            .FirstOrDefaultAsync();

        if (id.HasValue) return id;

        var status = new OrderStatus { StatusName = statusName };
        _context.OrderStatuses.Add(status);
        await _context.SaveChangesAsync();
        return status.StatusId;
    }

    private async Task<int?> GetPaymentMethodIdAsync(string methodName)
    {
        var id = await _context.PaymentMethods
            .Where(m => m.MethodName == methodName)
            .Select(m => (int?)m.PaymentMethodId)
            .FirstOrDefaultAsync();

        if (id.HasValue) return id;

        var method = new PaymentMethod { MethodName = methodName };
        _context.PaymentMethods.Add(method);
        await _context.SaveChangesAsync();
        return method.PaymentMethodId;
    }

    private async Task<int?> GetShippingMethodIdAsync(string methodName)
    {
        var id = await _context.ShippingMethods
            .Where(m => m.MethodName == methodName)
            .Select(m => (int?)m.ShippingMethodId)
            .FirstOrDefaultAsync();

        if (id.HasValue) return id;

        var method = new ShippingMethod { MethodName = methodName, Price = 0 };
        _context.ShippingMethods.Add(method);
        await _context.SaveChangesAsync();
        return method.ShippingMethodId;
    }

    private async Task SaveProductImage(int productId, IFormFile? imageFile)
    {
        if (imageFile == null || imageFile.Length <= 0) return;

        var fileName = await SaveUploadedImage(imageFile);
        if (string.IsNullOrWhiteSpace(fileName)) return;

        _context.ProductImages.Add(new ProductImage
        {
            ProductId = productId,
            ImageUrl = fileName
        });
        await _context.SaveChangesAsync();
    }

    private async Task<string?> SaveUploadedImage(IFormFile? imageFile)
    {
        if (imageFile == null || imageFile.Length <= 0) return null;

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp"
        };
        var ext = Path.GetExtension(imageFile.FileName);
        if (!allowed.Contains(ext)) return null;

        var path = Path.Combine(_env.WebRootPath, "img");
        Directory.CreateDirectory(path);

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(path, fileName);
        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await imageFile.CopyToAsync(stream);
        }

        return fileName;
    }
}
