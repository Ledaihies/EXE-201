using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EXE.Models;
using EXE.Security;

namespace EXE.Controllers.Admin
{
    [AdminOnly]
    public class ProductAdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ProductAdminController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // LIST PRODUCT
        public async Task<IActionResult> Index()
        {
            var products = await _context.Products
                .Include(p => p.ProductImages)
                .Include(p => p.Seller)
                .OrderBy(p => p.ApprovalStatus == "Pending" ? 0 : p.ApprovalStatus == "Rejected" ? 1 : 2)
                .ThenByDescending(p => p.CreatedDate)
                .ToListAsync();

            return View(products);
        }

        // CREATE PAGE
        public async Task<IActionResult> Create()
        {
            ViewBag.Categories = await _context.Categories.OrderBy(c => c.CategoryName).ToListAsync();
            ViewBag.Regions = await _context.Regions.OrderBy(r => r.RegionName).ToListAsync();
            return View();
        }

        // CREATE PRODUCT
        [HttpPost]
        public async Task<IActionResult> Create(Product product, IFormFile imageFile)
        {
            product.ApprovalStatus = "Approved";
            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            if (imageFile != null)
            {
                string path = Path.Combine(_env.WebRootPath, "img");
                Directory.CreateDirectory(path);

                string fileName = Guid.NewGuid() + Path.GetExtension(imageFile.FileName);

                string fullPath = Path.Combine(path, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }

                ProductImage img = new ProductImage
                {
                    ProductId = product.ProductId,
                    ImageUrl = fileName
                };

                _context.ProductImages.Add(img);

                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Approve(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            product.ApprovalStatus = "Approved";
            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> Reject(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            product.ApprovalStatus = "Rejected";
            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        // EDIT PAGE
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.Products
                .Include(p => p.ProductImages)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null)
                return NotFound();

            ViewBag.Categories = await _context.Categories.OrderBy(c => c.CategoryName).ToListAsync();
            ViewBag.Regions = await _context.Regions.OrderBy(r => r.RegionName).ToListAsync();
            return View(product);
        }

        // EDIT PRODUCT
        [HttpPost]
        public async Task<IActionResult> Edit(Product input, IFormFile? imageFile)
        {
            var product = await _context.Products
                .Include(p => p.ProductImages)
                .FirstOrDefaultAsync(p => p.ProductId == input.ProductId);

            if (product == null)
                return NotFound();

            product.ProductName = input.ProductName;
            product.Description = input.Description;
            product.Price = input.Price;
            product.Stock = input.Stock;
            product.CategoryId = input.CategoryId;
            product.RegionId = input.RegionId;

            if (imageFile != null)
            {
                string path = Path.Combine(_env.WebRootPath, "img");
                Directory.CreateDirectory(path);

                string fileName = Guid.NewGuid() + Path.GetExtension(imageFile.FileName);
                string fullPath = Path.Combine(path, fileName);

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }

                _context.ProductImages.Add(new ProductImage
                {
                    ProductId = product.ProductId,
                    ImageUrl = fileName
                });
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
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

            var cartItems = await _context.CartItems.Where(ci => ci.ProductId == id).ToListAsync();
            var wishlists = await _context.Wishlists.Where(w => w.ProductId == id).ToListAsync();
            var reviews = await _context.Reviews.Where(r => r.ProductId == id).ToListAsync();
            var adRequests = await _context.AdvertisingPaymentRequests.Where(r => r.ProductId == id).ToListAsync();
            var orderItems = await _context.OrderItems.Where(oi => oi.ProductId == id).ToListAsync();

            foreach (var item in orderItems)
            {
                item.ProductId = null;
                item.Product = null;
            }

            _context.CartItems.RemoveRange(cartItems);
            _context.Wishlists.RemoveRange(wishlists);
            _context.Reviews.RemoveRange(reviews);
            _context.AdvertisingPaymentRequests.RemoveRange(adRequests);
            _context.ProductImages.RemoveRange(product.ProductImages);
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            TempData["ProductAdminMessage"] = "Đã xóa sản phẩm.";
            return RedirectToAction("Index");
        }
    }
}
