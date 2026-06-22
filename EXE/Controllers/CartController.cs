using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EXE.Models;

namespace EXE.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CartController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool IsLoggedIn()
        {
            return HttpContext.Session.GetInt32("UserId").HasValue;
        }

        private async Task<int> GetOrCreateCartIdAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var sessionCartId = HttpContext.Session.GetInt32("CartId");

            // If logged in: use (or create) the latest cart for that user.
            if (userId.HasValue)
            {
                var existing = await _context.Carts
                    .Where(c => c.UserId == userId.Value)
                    .OrderByDescending(c => c.CreatedDate)
                    .Select(c => (int?)c.CartId)
                    .FirstOrDefaultAsync();

                if (existing.HasValue)
                {
                    HttpContext.Session.SetInt32("CartId", existing.Value);
                    return existing.Value;
                }

                var cart = new Cart { UserId = userId.Value, CreatedDate = DateTime.Now };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();

                HttpContext.Session.SetInt32("CartId", cart.CartId);
                return cart.CartId;
            }

            // Guest: use existing session cart or create a new one.
            if (sessionCartId.HasValue)
                return sessionCartId.Value;

            var guestCart = new Cart { CreatedDate = DateTime.Now };
            _context.Carts.Add(guestCart);
            await _context.SaveChangesAsync();

            HttpContext.Session.SetInt32("CartId", guestCart.CartId);
            return guestCart.CartId;
        }

        public async Task<IActionResult> Index()
        {
            if (!IsLoggedIn())
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { ok = false, message = "Vui lòng đăng nhập để thêm sản phẩm vào giỏ hàng." });
                return RedirectToAction("Login", "Auth");
            }

            var cartId = await GetOrCreateCartIdAsync();

            var cart = await _context.CartItems
                .Include(c => c.Product)
                    .ThenInclude(p => p.ProductImages)
                .Include(c => c.Product)
                    .ThenInclude(p => p.Region)
                .Where(c => c.CartId == cartId)
                .ToListAsync();

            var productIdsInCart = cart.Select(x => x.ProductId).ToHashSet();
            var suggested = await _context.Products
                .Include(p => p.ProductImages)
                .Include(p => p.Region)
                .Where(p => p.ApprovalStatus == "Approved" && !productIdsInCart.Contains(p.ProductId))
                .OrderByDescending(p => p.CreatedDate)
                .Take(6)
                .ToListAsync();

            ViewBag.SuggestedProducts = suggested;
            return View(cart);
        }

        public async Task<IActionResult> Add(int id, int quantity = 1)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction("Login", "Auth");
            }

            if (quantity < 1) quantity = 1;

            var product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == id && p.ApprovalStatus == "Approved");
            if (product == null)
            {
                TempData["CartError"] = "Sản phẩm chưa được duyệt hoặc không còn bán.";
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { ok = false, message = TempData["CartError"] });
                return RedirectBackOrProducts();
            }
            if (product.Stock.HasValue && product.Stock.Value <= 0)
            {
                const string message = "Sản phẩm đã hết hàng.";
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { ok = false, message });
                TempData["CartError"] = message;
                return RedirectBackOrProducts();
            }
            if (product != null && product.Stock.HasValue && product.Stock.Value > 0 && quantity > product.Stock.Value)
                quantity = product.Stock.Value;

            var cartId = await GetOrCreateCartIdAsync();

            var existing = await _context.CartItems
                .FirstOrDefaultAsync(x => x.CartId == cartId && x.ProductId == id);

            if (existing != null)
            {
                var newQty = (existing.Quantity ?? 0) + quantity;
                if (product?.Stock.HasValue == true && product.Stock.Value > 0 && newQty > product.Stock.Value)
                    newQty = product.Stock.Value;
                existing.Quantity = newQty;
            }
            else
            {
                var item = new CartItem
                {
                    CartId = cartId,
                    ProductId = id,
                    Quantity = quantity
                };

                _context.CartItems.Add(item);
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Thêm sản phẩm vào giỏ hàng thành công!";
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { ok = true, redirectUrl = Url.Action("Index", "Cart"), message = TempData["SuccessMessage"] });

            return RedirectToAction("Index", "Cart");
        }

        private IActionResult RedirectBackOrProducts()
        {
            var referer = Request.Headers.Referer.ToString();
            if (!string.IsNullOrWhiteSpace(referer) &&
                Uri.TryCreate(referer, UriKind.Absolute, out var refererUri) &&
                string.Equals(refererUri.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase))
            {
                return Redirect(referer);
            }

            return RedirectToAction("Index", "Product");
        }

        public async Task<IActionResult> Increase(int id)
        {
            if (!IsLoggedIn())
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { ok = false });
                return RedirectToAction("Login", "Auth");
            }

            var cartId = await GetOrCreateCartIdAsync();
            var item = await _context.CartItems
                .Include(x => x.Product)
                .FirstOrDefaultAsync(x => x.CartItemId == id && x.CartId == cartId);
            if (item != null)
            {
                item.Quantity = (item.Quantity ?? 0) + 1;
                await _context.SaveChangesAsync();
                var price = item.Product?.Price ?? 0;
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { ok = true, qty = item.Quantity, lineTotal = price * (item.Quantity ?? 0) });
            }

            return Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                ? Json(new { ok = false })
                : RedirectToAction("Index");
        }

        public async Task<IActionResult> Decrease(int id)
        {
            if (!IsLoggedIn())
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { ok = false });
                return RedirectToAction("Login", "Auth");
            }

            var cartId = await GetOrCreateCartIdAsync();
            var item = await _context.CartItems
                .Include(x => x.Product)
                .FirstOrDefaultAsync(x => x.CartItemId == id && x.CartId == cartId);
            if (item != null)
            {
                var q = (item.Quantity ?? 0) - 1;
                var price = item.Product?.Price ?? 0;
                if (q <= 0)
                {
                    _context.CartItems.Remove(item);
                    await _context.SaveChangesAsync();
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        return Json(new { ok = true, removed = true, qty = 0, lineTotal = 0 });
                }
                else
                {
                    item.Quantity = q;
                    await _context.SaveChangesAsync();
                    if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        return Json(new { ok = true, removed = false, qty = q, lineTotal = price * q });
                }
            }

            return Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                ? Json(new { ok = false })
                : RedirectToAction("Index");
        }

        public async Task<IActionResult> Remove(int id)
        {
            if (!IsLoggedIn())
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { ok = false });
                return RedirectToAction("Login", "Auth");
            }

            var cartId = await GetOrCreateCartIdAsync();
            var item = await _context.CartItems.FirstOrDefaultAsync(x => x.CartItemId == id && x.CartId == cartId);
            if (item != null)
            {
                _context.CartItems.Remove(item);
                await _context.SaveChangesAsync();
            }

            return Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                ? Json(new { ok = true })
                : RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToWishlist(int productId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue) return Json(new { ok = false });

            var exists = await _context.Wishlists.AnyAsync(w => w.UserId == userId && w.ProductId == productId);
            if (exists) return Json(new { ok = true, message = "Đã có trong danh sách yêu thích" });

            _context.Wishlists.Add(new Wishlist { UserId = userId, ProductId = productId, CreatedDate = DateTime.Now });
            await _context.SaveChangesAsync();
            return Json(new { ok = true, message = "Đã lưu để mua sau" });
        }

        [HttpGet]
        public async Task<IActionResult> CartTotals()
        {
            if (!IsLoggedIn()) return Json(new { ok = false });
            var cartId = await GetOrCreateCartIdAsync();
            var items = await _context.CartItems
                .Include(c => c.Product)
                .Where(c => c.CartId == cartId)
                .ToListAsync();
            decimal subtotal = 0;
            foreach (var x in items)
            {
                var price = x.Product?.Price ?? 0;
                var qty = x.Quantity ?? 0;
                subtotal += price * qty;
            }
            decimal shippingFee = items.Any() ? 25000 : 0;
            return Json(new { ok = true, subtotal, shippingFee, total = subtotal + shippingFee });
        }
    }
}
