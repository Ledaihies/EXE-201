using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EXE.Models;
using EXE.Security;
using EXE.Services;

namespace EXE.Controllers.Admin
{
    [AdminOnly]
    public class ProductAdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly INotificationService _notificationService;

        public ProductAdminController(ApplicationDbContext context, IWebHostEnvironment env, INotificationService notificationService)
        {
            _context = context;
            _env = env;
            _notificationService = notificationService;
        }

        // LIST PRODUCT
        public async Task<IActionResult> Index()
        {
            var products = await _context.Products
                .Include(p => p.ProductImages)
                .Include(p => p.Seller)
                .Include(p => p.Category)
                .Include(p => p.Region)
                .OrderBy(p => p.ApprovalStatus == "Pending" ? 0 : p.ApprovalStatus == "Rejected" ? 1 : 2)
                .ThenByDescending(p => p.CreatedDate)
                .ToListAsync();

            return View(products);
        }

        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Include(p => p.ProductImages)
                .Include(p => p.Seller)
                .Include(p => p.Category)
                .Include(p => p.Region)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null) return NotFound();
            return View(product);
        }

        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == id);
            if (product == null)
            {
                TempData["ProductAdminError"] = "Không tìm thấy sản phẩm.";
                return RedirectToAction(nameof(Index));
            }

            product.ApprovalStatus = "Approved";
            await _context.SaveChangesAsync();

            if (product.SellerId.HasValue)
            {
                await _notificationService.CreateNotification(
                    product.SellerId.Value,
                    "Sản phẩm đã được duyệt",
                    $"Sản phẩm #{product.ProductId} - {product.ProductName} đã được duyệt và hiển thị trên sàn.",
                    "Product",
                    "Product",
                    product.ProductId);
            }

            TempData["ProductAdminMessage"] = "Đã duyệt sản phẩm.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Reject(int id)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == id);
            if (product == null)
            {
                TempData["ProductAdminError"] = "Không tìm thấy sản phẩm.";
                return RedirectToAction(nameof(Index));
            }

            product.ApprovalStatus = "Rejected";
            await _context.SaveChangesAsync();

            if (product.SellerId.HasValue)
            {
                await _notificationService.CreateNotification(
                    product.SellerId.Value,
                    "Sản phẩm bị từ chối",
                    $"Sản phẩm #{product.ProductId} - {product.ProductName} chưa được duyệt. Vui lòng kiểm tra và cập nhật lại.",
                    "Product",
                    "Product",
                    product.ProductId);
            }

            TempData["ProductAdminMessage"] = "Đã từ chối sản phẩm.";
            return RedirectToAction(nameof(Index));
        }

        // CREATE PAGE
        public async Task<IActionResult> Create()
        {
            return Forbid();
        }

        // CREATE PRODUCT
        [HttpPost]
        public async Task<IActionResult> Create(Product product, IFormFile? imageFile)
        {
            return Forbid();
        }

        // EDIT PAGE
        public async Task<IActionResult> Edit(int id)
        {
            return Forbid();
        }

        // EDIT PRODUCT
        [HttpPost]
        public async Task<IActionResult> Edit(Product input, IFormFile? imageFile)
        {
            return Forbid();
        }

        // DELETE
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products
                .Include(p => p.ProductImages)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null)
            {
                TempData["ProductAdminError"] = "Không tìm thấy sản phẩm.";
                return RedirectToAction("Index");
            }

            var hasReferences = await _context.CartItems.AnyAsync(ci => ci.ProductId == id)
                || await _context.Wishlists.AnyAsync(w => w.ProductId == id)
                || await _context.Reviews.AnyAsync(r => r.ProductId == id)
                || await _context.AdvertisingPaymentRequests.AnyAsync(r => r.ProductId == id)
                || await _context.OrderItems.AnyAsync(oi => oi.ProductId == id);

            if (hasReferences)
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
                TempData["ProductAdminMessage"] = "Đã ẩn sản phẩm vì còn dữ liệu liên quan.";
                return RedirectToAction("Index");
            }

            _context.ProductImages.RemoveRange(product.ProductImages);
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            TempData["ProductAdminMessage"] = "Đã xóa sản phẩm.";
            return RedirectToAction("Index");
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

            try
            {
                var webRoot = string.IsNullOrWhiteSpace(_env.WebRootPath)
                    ? Path.Combine(_env.ContentRootPath, "wwwroot")
                    : _env.WebRootPath;
                var path = Path.Combine(webRoot, "img");
                Directory.CreateDirectory(path);

                var fileName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
                var fullPath = Path.Combine(path, fileName);
                await using var stream = new FileStream(fullPath, FileMode.CreateNew);
                await imageFile.CopyToAsync(stream);
                return fileName;
            }
            catch
            {
                return null;
            }
        }
    }
}
