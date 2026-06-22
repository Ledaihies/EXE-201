using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EXE.Models;
using EXE.Services;
using EXE.ViewModels;

namespace EXE.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IWalletService _walletService;
        private readonly IBankTransferService _bankTransferService;
        private readonly ISePayTransactionLookupService _sePayLookupService;
        private readonly INotificationService _notificationService;

        public AccountController(ApplicationDbContext context, IWebHostEnvironment env, IWalletService walletService, IBankTransferService bankTransferService, ISePayTransactionLookupService sePayLookupService, INotificationService notificationService)
        {
            _context = context;
            _env = env;
            _walletService = walletService;
            _bankTransferService = bankTransferService;
            _sePayLookupService = sePayLookupService;
            _notificationService = notificationService;
        }

        private int? GetCurrentUserId()
        {
            return HttpContext.Session.GetInt32("UserId");
        }

        private bool IsCustomer()
        {
            var roleName = HttpContext.Session.GetString("RoleName") ?? string.Empty;
            return !string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(roleName, "Staff", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Hóa đơn của tôi - danh sách đơn hàng của khách hàng đang đăng nhập
        /// </summary>
        public async Task<IActionResult> MyOrders()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return RedirectToAction("Login", "Auth");

            if (!IsCustomer())
                return RedirectToAction("Index", "Home");

            var orders = await _context.Orders
                .Include(o => o.Status)
                .Include(o => o.ReturnRequests)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Where(o => o.UserId == userId.Value)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        /// <summary>
        /// Chi tiết đơn hàng (chỉ xem được đơn của mình)
        /// </summary>
        public async Task<IActionResult> OrderDetails(int id)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return RedirectToAction("Login", "Auth");

            var order = await _context.Orders
                .Include(o => o.User)
                .Include(o => o.Status)
                .Include(o => o.Payments)
                    .ThenInclude(p => p.PaymentMethod)
                .Include(o => o.ReturnRequests)
                    .ThenInclude(r => r.SellerConfirmedByUser)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p!.ProductImages)
                .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId.Value);

            if (order == null)
                return NotFound();

            var productIds = order.OrderItems
                .Where(oi => oi.ProductId.HasValue)
                .Select(oi => oi.ProductId!.Value)
                .ToList();
            ViewBag.ReviewedProductIds = await _context.Reviews
                .Where(r => r.UserId == userId.Value && r.ProductId.HasValue && productIds.Contains(r.ProductId.Value))
                .Select(r => r.ProductId!.Value)
                .ToListAsync();

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Auth");

            var order = await _context.Orders
                .Include(o => o.Status)
                .Include(o => o.CODCollection)
                .Include(o => o.OrderSettlements)
                .Include(o => o.Payments)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId.Value);

            if (order == null) return NotFound();

            var status = order.Status?.StatusName ?? string.Empty;
            if (CanCustomerCancel(status))
            {
                order.StatusId = await GetStatusIdAsync("Da huy");
                order.PaymentStatus = string.Equals(order.PaymentStatus, "PendingCODCollection", StringComparison.OrdinalIgnoreCase)
                    ? "Cancelled"
                    : order.PaymentStatus;
                if (order.CODCollection != null)
                {
                    order.CODCollection.Status = "Cancelled";
                }
                foreach (var settlement in order.OrderSettlements)
                {
                    settlement.SettlementStatus = "Cancelled";
                }
                foreach (var item in order.OrderItems)
                {
                    if (item.Product != null)
                    {
                        item.Product.Stock = (item.Product.Stock ?? 0) + (item.Quantity ?? 0);
                    }
                }
                await _context.SaveChangesAsync();
                var paidAmount = order.Payments.Where(p => (p.Amount ?? 0) > 0).Sum(p => p.Amount ?? 0);
                if (paidAmount > 0)
                {
                    await _walletService.CreditAsync(userId.Value, paidAmount, "Refund", $"Hoàn tiền đơn #{order.OrderId}", order.OrderId);
                }
                TempData["OrderMessage"] = "Đã hủy đơn hàng.";
            }
            else
            {
                TempData["OrderMessage"] = "Đơn hàng đã được giao cho vận chuyển nên không thể hủy trực tiếp.";
            }

            return RedirectToAction(nameof(OrderDetails), new { id });
        }

        private static bool CanCustomerCancel(string? status)
        {
            return string.Equals(status, "Cho nguoi ban xac nhan", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "Cho lay hang", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<IActionResult> Wallet()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Auth");

            var wallet = await _walletService.GetOrCreateWalletAsync(userId.Value);
            wallet.Transactions = await _context.WalletTransactions
                .Where(t => t.WalletId == wallet.WalletId)
                .OrderByDescending(t => t.CreatedDate)
                .Take(100)
                .ToListAsync();

            return View(wallet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TopUpWallet(decimal amount)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Auth");

            if (amount <= 0)
            {
                TempData["WalletMessage"] = "Số tiền nạp không hợp lệ.";
                return RedirectToAction(nameof(Wallet));
            }

            var request = new WalletTopUpRequest
            {
                UserId = userId.Value,
                Amount = amount,
                Status = "Pending",
                CreatedDate = DateTime.Now
            };
            _context.WalletTopUpRequests.Add(request);
            await _context.SaveChangesAsync();
            request.TransferContent = $"VI{request.RequestId}";
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(WalletTopUpPayment), new { id = request.RequestId });
        }

        public async Task<IActionResult> WalletTopUpPayment(int id)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Auth");

            var request = await _context.WalletTopUpRequests
                .FirstOrDefaultAsync(r => r.RequestId == id && r.UserId == userId.Value);
            if (request == null) return NotFound();

            return View(new WalletTopUpPaymentViewModel
            {
                Request = request,
                QrUrl = _bankTransferService.BuildVietQrUrl(request.Amount, request.TransferContent),
                IsPaid = string.Equals(request.Status, "Paid", StringComparison.OrdinalIgnoreCase)
            });
        }

        [HttpGet]
        public async Task<IActionResult> WalletTopUpStatus(int id)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var request = await _context.WalletTopUpRequests.FirstOrDefaultAsync(r =>
                r.RequestId == id &&
                r.UserId == userId.Value);
            if (request == null) return NotFound();

            var isPaid = string.Equals(request.Status, "Paid", StringComparison.OrdinalIgnoreCase);
            if (!isPaid)
            {
                var paymentCode = string.IsNullOrWhiteSpace(request.TransferContent) ? $"VI{request.RequestId}" : request.TransferContent;
                var found = await _sePayLookupService.HasIncomingTransactionAsync(paymentCode, request.Amount, request.CreatedDate);
                if (found)
                {
                    request.Status = "Paid";
                    request.ConfirmedDate = DateTime.Now;
                    await _context.SaveChangesAsync();
                    await _walletService.CreditAsync(request.UserId, request.Amount, "TopUp", $"Nap vi #{request.RequestId}");
                    isPaid = true;
                }
            }

            return Json(new { isPaid });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmReceived(int id)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Auth");

            var order = await _context.Orders
                .Include(o => o.Status)
                .Include(o => o.CODCollection)
                .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId.Value);

            if (order == null) return NotFound();

            var status = order.Status?.StatusName ?? string.Empty;
            if (status == "Cho giao hang")
            {
                order.StatusId = await GetStatusIdAsync("Da nhan hang");
                if (order.CODCollection != null)
                {
                    order.CODCollection.Status = "CollectedFromCustomer";
                    order.CODCollection.CollectedAt = DateTime.Now;
                }
                await _context.SaveChangesAsync();
                TempData["OrderMessage"] = "Cảm ơn bạn đã xác nhận nhận hàng. Bạn có thể đánh giá sản phẩm.";
            }
            else
            {
                TempData["OrderMessage"] = "Đơn hàng chưa ở trạng thái có thể xác nhận đã nhận.";
            }

            return RedirectToAction(nameof(OrderDetails), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestReturn(int id, string? reason, IFormFile? proofImage)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Auth");

            var order = await _context.Orders
                .Include(o => o.Status)
                .Include(o => o.ReturnRequests)
                .FirstOrDefaultAsync(o => o.OrderId == id && o.UserId == userId.Value);

            if (order == null) return NotFound();

            var status = order.Status?.StatusName ?? string.Empty;
            if (!string.Equals(status, "Da nhan hang", StringComparison.OrdinalIgnoreCase))
            {
                TempData["OrderMessage"] = "Chỉ có thể yêu cầu hoàn hàng sau khi đã xác nhận nhận hàng.";
                return RedirectToAction(nameof(OrderDetails), new { id });
            }

            if (order.ReturnRequests.Any(r => r.Status == "Pending" || r.Status == "Approved"))
            {
                TempData["OrderMessage"] = "Đơn hàng này đã có yêu cầu hoàn hàng đang xử lý hoặc đã duyệt.";
                return RedirectToAction(nameof(OrderDetails), new { id });
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["OrderMessage"] = "Vui lòng nhập lý do hoàn hàng.";
                return RedirectToAction(nameof(OrderDetails), new { id });
            }

            var proofFileName = await SaveReturnProofImage(proofImage);
            if (string.IsNullOrWhiteSpace(proofFileName))
            {
                TempData["OrderMessage"] = "Vui lòng tải lên ảnh minh chứng hợp lệ (.jpg, .png, .gif, .webp).";
                return RedirectToAction(nameof(OrderDetails), new { id });
            }

            _context.OrderReturnRequests.Add(new OrderReturnRequest
            {
                OrderId = order.OrderId,
                UserId = userId.Value,
                Reason = reason.Trim(),
                ProofImageUrl = proofFileName,
                Status = "Pending",
                RefundAmount = order.TotalAmount ?? 0m,
                CreatedDate = DateTime.Now
            });
            order.StatusId = await GetStatusIdAsync("Cho xac nhan hoan hang");

            await _context.SaveChangesAsync();
            TempData["OrderMessage"] = "Đã gửi yêu cầu hoàn hàng. Vui lòng chờ người bán xác nhận.";
            return RedirectToAction(nameof(OrderDetails), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewProduct(int orderId, int productId, int rating, string? comment)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return RedirectToAction("Login", "Auth");

            rating = Math.Clamp(rating, 1, 5);
            var canReview = await _context.Orders
                .Include(o => o.Status)
                .Include(o => o.OrderItems)
                .AnyAsync(o => o.OrderId == orderId &&
                               o.UserId == userId.Value &&
                               o.Status != null &&
                               o.Status.StatusName == "Da nhan hang" &&
                               o.OrderItems.Any(oi => oi.ProductId == productId));

            if (!canReview) return Forbid();

            var exists = await _context.Reviews.AnyAsync(r => r.UserId == userId.Value && r.ProductId == productId);
            if (exists)
            {
                TempData["OrderMessage"] = "Bạn đã đánh giá sản phẩm này rồi.";
                return RedirectToAction(nameof(OrderDetails), new { id = orderId });
            }

            _context.Reviews.Add(new Review
            {
                UserId = userId.Value,
                ProductId = productId,
                Rating = rating,
                Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
                CreatedDate = DateTime.Now
            });
            await _context.SaveChangesAsync();

            TempData["OrderMessage"] = "Đã gửi đánh giá sản phẩm.";
            return RedirectToAction(nameof(OrderDetails), new { id = orderId });
        }

        /// <summary>
        /// Trang chỉnh sửa tài khoản
        /// </summary>
        public async Task<IActionResult> Edit()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return RedirectToAction("Login", "Auth");

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == userId.Value);
            if (user == null)
                return NotFound();

            return View(user);
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

        private async Task<string?> SaveReturnProofImage(IFormFile? imageFile)
        {
            if (imageFile == null || imageFile.Length <= 0) return null;

            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg", ".jpeg", ".png", ".gif", ".webp"
            };
            var ext = Path.GetExtension(imageFile.FileName);
            if (!allowed.Contains(ext)) return null;

            var path = Path.Combine(_env.WebRootPath, "img", "returns");
            Directory.CreateDirectory(path);

            var fileName = $"returns/{Guid.NewGuid():N}{ext}";
            var fullPath = Path.Combine(_env.WebRootPath, "img", fileName);
            await using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            return fileName;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int userId, string fullName, string? phone, string? address)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue || currentUserId.Value != userId)
                return RedirectToAction("Login", "Auth");

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound();

            user.FullName = fullName?.Trim() ?? user.FullName;
            user.Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
            user.Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();

            await _context.SaveChangesAsync();

            HttpContext.Session.SetString("FullName", user.FullName ?? user.Email ?? "User");
            TempData["Success"] = "Cập nhật tài khoản thành công.";
            return RedirectToAction(nameof(Edit));
        }
    }
}
