using Microsoft.AspNetCore.Mvc;
using EXE.Models;
using Microsoft.EntityFrameworkCore;
using EXE.Security;

namespace EXE.Controllers
{
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuthController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Login(string email, string password)
        {
            email = (email ?? string.Empty).Trim();

            var user = _context.Users
                .Include(u => u.Role)
                .FirstOrDefault(x => x.Email == email);

            if (user != null && PasswordHasher.Verify(password, user.PasswordHash))
            {
                HttpContext.Session.SetInt32("UserId", user.UserId);
                if (user.RoleId.HasValue) HttpContext.Session.SetInt32("RoleId", user.RoleId.Value);
                HttpContext.Session.SetString("FullName", user.FullName ?? user.Email ?? "User");

                var roleName = (user.Role?.RoleName ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(roleName) && user.RoleId.HasValue)
                {
                    roleName = (_context.Roles.AsNoTracking()
                        .Where(r => r.RoleId == user.RoleId.Value)
                        .Select(r => r.RoleName)
                        .FirstOrDefault() ?? string.Empty).Trim();
                }

                HttpContext.Session.SetString("RoleName", roleName);

                // Điều hướng theo vai trò
                if (string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    return RedirectToAction("Index", "Admin");
                }
                if (string.Equals(roleName, "Staff", StringComparison.OrdinalIgnoreCase))
                {
                    return RedirectToAction("Index", "Staff");
                }
                if (string.Equals(roleName, "Seller", StringComparison.OrdinalIgnoreCase))
                {
                    return RedirectToAction("Index", "Seller");
                }

                return RedirectToAction("Index", "Home");
            }

            ViewBag.Error = "Login Failed";

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(string fullName, string email, string password, string confirmPassword, string? referralCode = null)
        {
            email = (email ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Vui lòng nhập email và mật khẩu.";
                return View("Login");
            }

            if (password != confirmPassword)
            {
                ViewBag.Error = "Mật khẩu xác nhận không khớp.";
                return View("Login");
            }

            var exists = await _context.Users.AnyAsync(x => x.Email == email);
            if (exists)
            {
                ViewBag.Error = "Email đã tồn tại.";
                return View("Login");
            }

            var user = new User
            {
                FullName = fullName,
                Email = email,
                PasswordHash = PasswordHasher.Hash(password),
                RoleId = await _context.Roles.Where(r => r.RoleName == "User").Select(r => (int?)r.RoleId).FirstOrDefaultAsync(),
                CreatedDate = DateTime.Now
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            HttpContext.Session.SetInt32("UserId", user.UserId);
            if (user.RoleId.HasValue) HttpContext.Session.SetInt32("RoleId", user.RoleId.Value);
            HttpContext.Session.SetString("RoleName", "User");
            HttpContext.Session.SetString("FullName", user.FullName ?? user.Email ?? "User");
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> RegisterSeller(string shopName, string ownerName, string email, string phone, string address, string password, string confirmPassword)
        {
            email = (email ?? string.Empty).Trim();
            shopName = (shopName ?? string.Empty).Trim();
            ownerName = (ownerName ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(shopName) ||
                string.IsNullOrWhiteSpace(ownerName) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ thông tin đăng ký người bán.";
                return View("Login");
            }

            if (password != confirmPassword)
            {
                ViewBag.Error = "Mật khẩu xác nhận không khớp.";
                return View("Login");
            }

            var exists = await _context.Users.AnyAsync(x => x.Email == email);
            if (exists)
            {
                ViewBag.Error = "Email đã tồn tại.";
                return View("Login");
            }

            var sellerRoleId = await _context.Roles
                .Where(r => r.RoleName == "Seller")
                .Select(r => (int?)r.RoleId)
                .FirstOrDefaultAsync();

            if (!sellerRoleId.HasValue)
            {
                var role = new Role { RoleName = "Seller" };
                _context.Roles.Add(role);
                await _context.SaveChangesAsync();
                sellerRoleId = role.RoleId;
            }

            var user = new User
            {
                FullName = shopName,
                Email = email,
                Phone = phone,
                Address = $"Chủ shop: {ownerName}; Địa chỉ lấy hàng: {address}",
                PasswordHash = PasswordHasher.Hash(password),
                RoleId = sellerRoleId.Value,
                CreatedDate = DateTime.Now
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetInt32("RoleId", sellerRoleId.Value);
            HttpContext.Session.SetString("RoleName", "Seller");
            HttpContext.Session.SetString("FullName", user.FullName ?? user.Email ?? "Seller");

            return RedirectToAction("Index", "Seller");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();

            return RedirectToAction("Index", "Home");
        }

        // Referral chương trình đã được gỡ bỏ, nên không sinh mã giới thiệu nữa.
    }
}
