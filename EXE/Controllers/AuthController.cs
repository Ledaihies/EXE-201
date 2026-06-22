using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EXE.Models;
using EXE.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EXE.Controllers
{
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public AuthController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            email = (email ?? string.Empty).Trim();

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(x => x.Email == email);

            if (user != null && PasswordHasher.Verify(password, user.PasswordHash))
            {
                HttpContext.Session.SetInt32("UserId", user.UserId);
                if (user.RoleId.HasValue) HttpContext.Session.SetInt32("RoleId", user.RoleId.Value);
                HttpContext.Session.SetString("FullName", user.FullName ?? user.Email ?? "User");

                var roleName = RoleAccess.Normalize(user.Role?.RoleName);
                if (string.IsNullOrWhiteSpace(roleName) && user.RoleId.HasValue)
                {
                    roleName = RoleAccess.Normalize(await _context.Roles.AsNoTracking()
                        .Where(r => r.RoleId == user.RoleId.Value)
                        .Select(r => r.RoleName)
                        .FirstOrDefaultAsync());
                }

                HttpContext.Session.SetString("RoleName", roleName);

                if (RoleAccess.IsRole(roleName, RoleAccess.Admin))
                {
                    return RedirectToAction("Index", "Admin");
                }

                if (RoleAccess.IsRole(roleName, RoleAccess.Staff))
                {
                    return RedirectToAction("Index", "Staff");
                }

                if (RoleAccess.IsRole(roleName, RoleAccess.Seller))
                {
                    var approvalStatus = await _context.Users
                        .AsNoTracking()
                        .Where(u => u.UserId == user.UserId)
                        .Select(u => u.SellerApprovalStatus)
                        .FirstOrDefaultAsync();

                    if (string.Equals(approvalStatus, "Approved", StringComparison.OrdinalIgnoreCase))
                    {
                        return RedirectToAction("Index", "Seller");
                    }

                    TempData["SellerMessage"] = GetSellerAccessMessage(approvalStatus);
                    return RedirectToAction("Index", "Home");
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
                RoleId = await _context.Roles
                    .Where(r => r.RoleName != null &&
                                (r.RoleName.Trim().ToLower() == "user" ||
                                 r.RoleName.Trim().ToLower() == "buyer" ||
                                 r.RoleName.Trim().ToLower() == "customer"))
                    .Select(r => (int?)r.RoleId)
                    .FirstOrDefaultAsync(),
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
        public async Task<IActionResult> RegisterSeller(
            string shopName,
            string ownerName,
            string email,
            string phone,
            string address,
            string password,
            string confirmPassword,
            IFormFile? licenseImageFile,
            IFormFile? originProofImageFile)
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

            if (!IsAllowedImageFile(licenseImageFile) || !IsAllowedImageFile(originProofImageFile))
            {
                ViewBag.Error = "Vui lòng tải lên ảnh giấy phép kinh doanh và ảnh chứng minh nguồn gốc hợp lệ.";
                return View("Login");
            }

            var sellerRoleId = await _context.Roles
                .Where(r => r.RoleName != null &&
                            (r.RoleName.Trim().ToLower() == "seller" ||
                             r.RoleName.Trim().ToLower() == "nguoi ban" ||
                             r.RoleName.Trim().ToLower() == "người bán" ||
                             r.RoleName.Trim().ToLower() == "shop"))
                .Select(r => (int?)r.RoleId)
                .FirstOrDefaultAsync();

            if (!sellerRoleId.HasValue)
            {
                var role = new Role { RoleName = "Seller" };
                _context.Roles.Add(role);
                await _context.SaveChangesAsync();
                sellerRoleId = role.RoleId;
            }

            var licenseImageUrl = await SaveUploadedImage(licenseImageFile);
            var originProofImageUrl = await SaveUploadedImage(originProofImageFile);
            if (string.IsNullOrWhiteSpace(licenseImageUrl) || string.IsNullOrWhiteSpace(originProofImageUrl))
            {
                ViewBag.Error = "Không thể lưu ảnh giấy phép hoặc ảnh chứng minh nguồn gốc. Vui lòng thử lại.";
                return View("Login");
            }

            var user = new User
            {
                FullName = shopName,
                Email = email,
                Phone = phone,
                Address = $"Chủ shop: {ownerName}; Địa chỉ lấy hàng: {address}",
                PasswordHash = PasswordHasher.Hash(password),
                RoleId = sellerRoleId.Value,
                SellerApprovalStatus = "Pending",
                SellerLicenseImageUrl = licenseImageUrl,
                SellerOriginProofImageUrl = originProofImageUrl,
                CreatedDate = DateTime.Now
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetInt32("RoleId", sellerRoleId.Value);
            HttpContext.Session.SetString("RoleName", "Seller");
            HttpContext.Session.SetString("FullName", user.FullName ?? user.Email ?? "Seller");
            TempData["SellerMessage"] = "Tài khoản người bán của bạn đang chờ Admin xác nhận. Bạn chưa thể đăng sản phẩm.";

            return RedirectToAction("Index", "Home");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Home");
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

        private static bool IsAllowedImageFile(IFormFile? file)
        {
            if (file == null || file.Length <= 0) return false;

            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg", ".jpeg", ".png", ".gif", ".webp"
            };

            return allowed.Contains(Path.GetExtension(file.FileName));
        }

        private static string GetSellerAccessMessage(string? approvalStatus)
        {
            if (string.Equals(approvalStatus, "Rejected", StringComparison.OrdinalIgnoreCase))
            {
                return "Tài khoản người bán của bạn đã bị từ chối. Vui lòng liên hệ Admin để biết lý do.";
            }

            return "Tài khoản người bán của bạn đang chờ Admin xác nhận. Bạn chưa thể đăng sản phẩm.";
        }
    }
}
