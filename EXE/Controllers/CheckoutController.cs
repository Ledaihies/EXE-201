using Microsoft.AspNetCore.Mvc;
using EXE.Models;
using Microsoft.EntityFrameworkCore;
using EXE.ViewModels;
using EXE.Services;
using System.Text.Json;

namespace EXE.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IBankTransferService _bankTransferService;
        private readonly IGHNService _ghnService;
        private readonly IWalletService _walletService;
        private readonly ISePayTransactionLookupService _sePayLookupService;
        private readonly ILogger<CheckoutController> _logger;

        public CheckoutController(
            ApplicationDbContext context,
            IBankTransferService bankTransferService,
            IGHNService ghnService,
            IWalletService walletService,
            ISePayTransactionLookupService sePayLookupService,
            ILogger<CheckoutController> logger)
        {
            _context = context;
            _bankTransferService = bankTransferService;
            _ghnService = ghnService;
            _walletService = walletService;
            _sePayLookupService = sePayLookupService;
            _logger = logger;
        }

        private static string BuildShippingAddress(string? address, string? phone, string? name = null)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(name)) parts.Add("Người nhận: " + name.Trim());
            if (!string.IsNullOrWhiteSpace(address)) parts.Add(address.Trim());
            if (!string.IsNullOrWhiteSpace(phone)) parts.Add("SĐT: " + phone.Trim());
            return parts.Count > 0 ? string.Join(". ", parts) : "";
        }

        /// <summary>Validate voucher and compute discount for given cart items and subtotal. Returns (discount amount, voucher, error message).</summary>
        private async Task<(decimal discount, Voucher? voucher, string? error)> ValidateVoucherAsync(string? code, List<CartItem> items, decimal subtotal)
        {
            if (string.IsNullOrWhiteSpace(code))
                return (0, null, null);

            var voucher = await _context.Vouchers
                .Include(v => v.Region)
                .Include(v => v.Category)
                .FirstOrDefaultAsync(v => v.IsActive && v.Code != null && v.Code.Trim().ToUpper() == code.Trim().ToUpper());
            if (voucher == null)
                return (0, null, "Mã giảm giá không tồn tại.");

            var now = DateTime.Now;
            if (voucher.StartDate.HasValue && now < voucher.StartDate.Value)
                return (0, null, "Mã chưa đến thời gian áp dụng.");
            if (voucher.ExpiryDate.HasValue && now > voucher.ExpiryDate.Value)
                return (0, null, "Mã giảm giá đã hết hạn.");

            if (voucher.MinOrderAmount.HasValue && subtotal < voucher.MinOrderAmount.Value)
                return (0, null, $"Đơn tối thiểu {voucher.MinOrderAmount.Value:N0}đ để dùng mã.");

            if (voucher.MaxUses.HasValue)
            {
                var used = await _context.OrderVouchers.CountAsync(ov => ov.VoucherId == voucher.VoucherId);
                if (used >= voucher.MaxUses.Value)
                    return (0, null, "Mã đã hết lượt sử dụng.");
            }

            if (voucher.RegionId.HasValue)
            {
                var hasRegion = items.Any(it => it.Product?.RegionId == voucher.RegionId);
                if (!hasRegion)
                    return (0, null, "Mã chỉ áp dụng cho đơn có đặc sản vùng " + (voucher.Region?.RegionName ?? "đã chọn") + ".");
            }
            if (voucher.CategoryId.HasValue)
            {
                var hasCategory = items.Any(it => it.Product?.CategoryId == voucher.CategoryId);
                if (!hasCategory)
                    return (0, null, "Mã chỉ áp dụng cho danh mục " + (voucher.Category?.CategoryName ?? "đã chọn") + ".");
            }

            decimal discount = 0;
            if (voucher.DiscountPercent.HasValue && voucher.DiscountPercent.Value > 0)
                discount = Math.Round(subtotal * voucher.DiscountPercent.Value / 100m, 0);
            else if (voucher.DiscountFixed.HasValue && voucher.DiscountFixed.Value > 0)
                discount = voucher.DiscountFixed.Value;
            discount = Math.Min(discount, subtotal);
            return (discount, voucher, null);
        }

        private async Task<int> GetOrCreateCartIdAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var sessionCartId = HttpContext.Session.GetInt32("CartId");

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

            if (sessionCartId.HasValue)
                return sessionCartId.Value;

            var guestCart = new Cart { CreatedDate = DateTime.Now };
            _context.Carts.Add(guestCart);
            await _context.SaveChangesAsync();

            HttpContext.Session.SetInt32("CartId", guestCart.CartId);
            return guestCart.CartId;
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

        private async Task<decimal> CalculateShippingFeeAsync(List<CartItem> items, decimal subtotal, CheckoutViewModel input)
        {
            var districtId = input.ToDistrictId.GetValueOrDefault();
            if (districtId <= 0 || string.IsNullOrWhiteSpace(input.ToWardCode))
            {
                return 25000m;
            }

            var itemCount = items.Sum(x => x.Quantity ?? 0);
            return await _ghnService.CalculateFeeAsync(subtotal, districtId, input.ToWardCode.Trim(), itemCount);
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> CalculateShippingFee([FromBody] CheckoutViewModel input)
        {
            var cartId = await GetOrCreateCartIdAsync();
            var items = await _context.CartItems
                .Include(x => x.Product)
                .Where(x => x.CartId == cartId)
                .ToListAsync();

            var subtotal = items.Sum(it => (it.Product?.Price ?? 0) * (it.Quantity ?? 0));
            var fee = await CalculateShippingFeeAsync(items, subtotal, input);
            return Json(new { fee, total = subtotal + fee });
        }

        [HttpGet]
        public async Task<IActionResult> GHNProvinces()
        {
            return Json(await _ghnService.GetProvincesAsync());
        }

        [HttpGet]
        public async Task<IActionResult> GHNDistricts(int provinceId)
        {
            if (provinceId <= 0) return Json(Array.Empty<object>());
            return Json(await _ghnService.GetDistrictsAsync(provinceId));
        }

        [HttpGet]
        public async Task<IActionResult> GHNWards(int districtId)
        {
            if (districtId <= 0) return Json(Array.Empty<object>());
            return Json(await _ghnService.GetWardsAsync(districtId));
        }

        // Chương trình giới thiệu bạn bè đã được gỡ bỏ, không còn phát sinh mã thưởng.

        public async Task<IActionResult> Index()
        {
            var cartId = await GetOrCreateCartIdAsync();
            var userId = HttpContext.Session.GetInt32("UserId");

            var items = await _context.CartItems
                .Include(x => x.Product)
                .ThenInclude(p => p.ProductImages)
                .Where(x => x.CartId == cartId)
                .ToListAsync();

            var unavailable = items.Where(x => x.Product == null || x.Product.ApprovalStatus != "Approved").ToList();
            if (unavailable.Any())
            {
                _context.CartItems.RemoveRange(unavailable);
                await _context.SaveChangesAsync();
                items = items.Except(unavailable).ToList();
                TempData["CheckoutError"] = "Một số sản phẩm chưa được duyệt đã được gỡ khỏi giỏ hàng.";
            }

            var outOfStock = items
                .Where(x => (x.Product?.Stock ?? 0) < (x.Quantity ?? 0))
                .Select(x => x.Product?.ProductName ?? $"SP #{x.ProductId}")
                .ToList();
            if (outOfStock.Any())
            {
                TempData["CheckoutError"] = "Một số sản phẩm không đủ tồn kho: " + string.Join(", ", outOfStock);
                return RedirectToAction("Index");
            }

            decimal subtotal = 0;
            foreach (var it in items)
            {
                var price = it.Product?.Price ?? 0;
                var qty = it.Quantity ?? 0;
                subtotal += price * qty;
            }

            string? defaultAddress = null;
            string? defaultPhone = null;
            string? defaultName = null;
            if (userId.HasValue)
            {
                var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId.Value);
                if (user != null)
                {
                    defaultAddress = user.Address;
                    defaultPhone = user.Phone;
                    defaultName = user.FullName;
                }
            }

            var vm = new CheckoutViewModel
            {
                Items = items,
                Subtotal = subtotal,
                ShippingFee = items.Any() ? 25000 : 0,
                ShippingAddress = defaultAddress,
                ContactPhone = defaultPhone,
                ContactName = defaultName,
                VoucherCode = TempData["VoucherCode"] as string
            };

            return View(vm);
        }

        [HttpPost]
        public async Task<IActionResult> PlaceOrder(CheckoutViewModel input)
        {
            return await PayWithBankTransfer(input);
        }


        [HttpPost]
        public async Task<IActionResult> PayWithBankTransfer(CheckoutViewModel input)
        {
            return await CreateOrderFromCart(input, payWithWallet: false);
        }

        [HttpPost]
        public async Task<IActionResult> PayWithWallet(CheckoutViewModel input)
        {
            return await CreateOrderFromCart(input, payWithWallet: true);
        }

        private async Task<IActionResult> CreateOrderFromCart(CheckoutViewModel input, bool payWithWallet)
        {
            var cartId = await GetOrCreateCartIdAsync();
            var userId = HttpContext.Session.GetInt32("UserId");
            if (payWithWallet && !userId.HasValue)
            {
                return RedirectToAction("Login", "Auth");
            }

            var items = await _context.CartItems
                .Include(x => x.Product)
                .Where(x => x.CartId == cartId)
                .ToListAsync();

            if (!items.Any())
            {
                return RedirectToAction("Index", "Cart");
            }

            if (items.Any(x => x.Product == null || x.Product.ApprovalStatus != "Approved"))
            {
                TempData["CheckoutError"] = "Giỏ hàng có sản phẩm chưa được duyệt. Vui lòng kiểm tra lại.";
                return RedirectToAction("Index");
            }

            decimal subtotal = 0;
            foreach (var it in items)
            {
                var price = it.Product?.Price ?? 0;
                var qty = it.Quantity ?? 0;
                subtotal += price * qty;
            }

            if (input.ToDistrictId.GetValueOrDefault() <= 0 || string.IsNullOrWhiteSpace(input.ToWardCode))
            {
                TempData["CheckoutError"] = "Vui lòng nhập mã quận/huyện và mã phường/xã GHN để tính phí vận chuyển.";
                TempData["VoucherCode"] = input.VoucherCode;
                return RedirectToAction("Index");
            }

            var outOfStockOnSubmit = items
                .Where(x => (x.Product?.Stock ?? 0) < (x.Quantity ?? 0))
                .Select(x => x.Product?.ProductName ?? $"SP #{x.ProductId}")
                .ToList();
            if (outOfStockOnSubmit.Any())
            {
                TempData["CheckoutError"] = "Một số sản phẩm không đủ tồn kho: " + string.Join(", ", outOfStockOnSubmit);
                TempData["VoucherCode"] = input.VoucherCode;
                return RedirectToAction("Index");
            }

            var shippingFee = await CalculateShippingFeeAsync(items, subtotal, input);
            decimal discount = 0;
            Voucher? appliedVoucher = null;
            if (!string.IsNullOrWhiteSpace(input.VoucherCode))
            {
                var (d, v, err) = await ValidateVoucherAsync(input.VoucherCode, items, subtotal);
                if (!string.IsNullOrEmpty(err))
                {
                    TempData["CheckoutError"] = err;
                    TempData["VoucherCode"] = input.VoucherCode;
                    return RedirectToAction("Index");
                }
                discount = d;
                appliedVoucher = v;
            }
            if (appliedVoucher?.IsFreeShipping == true)
            {
                shippingFee = 0;
            }
            var total = Math.Max(0, subtotal + shippingFee - discount);
            if (payWithWallet && userId.HasValue)
            {
                var wallet = await _walletService.GetOrCreateWalletAsync(userId.Value);
                if (wallet.Balance < total)
                {
                    TempData["CheckoutError"] = $"Số dư ví không đủ. Số dư hiện tại: {wallet.Balance:N0} đ.";
                    TempData["VoucherCode"] = input.VoucherCode;
                    return RedirectToAction("Index");
                }
            }

            var fullAddress = input.ShippingAddress?.Trim() ?? "";

            var initialStatus = payWithWallet ? "Cho nguoi ban xac nhan" : "Cho thanh toan";
            var order = new Order
            {
                UserId = userId,
                StatusId = await GetStatusIdAsync(initialStatus),
                ShippingAddress = fullAddress,
                ReceiverName = input.ContactName?.Trim(),
                ReceiverPhone = input.ContactPhone?.Trim(),
                ToDistrictId = input.ToDistrictId,
                ToWardCode = input.ToWardCode?.Trim(),
                ShippingNote = input.Note?.Trim(),
                TotalAmount = total,
                ShippingFee = shippingFee,
                OrderDate = DateTime.Now
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            foreach (var it in items)
            {
                var price = it.Product?.Price ?? 0;
                var qty = it.Quantity ?? 0;
                _context.OrderItems.Add(new OrderItem
                {
                    OrderId = order.OrderId,
                    ProductId = it.ProductId,
                    Quantity = qty,
                    Price = price
                });

                if (it.Product != null)
                {
                    it.Product.Stock = Math.Max(0, (it.Product.Stock ?? 0) - qty);
                }
            }
            if (appliedVoucher != null)
                _context.OrderVouchers.Add(new OrderVoucher { OrderId = order.OrderId, VoucherId = appliedVoucher.VoucherId });

            if (payWithWallet && userId.HasValue)
            {
                var wallet = await _context.Wallets.FirstAsync(w => w.UserId == userId.Value);
                wallet.Balance -= total;
                wallet.UpdatedDate = DateTime.Now;
                _context.WalletTransactions.Add(new WalletTransaction
                {
                    WalletId = wallet.WalletId,
                    OrderId = order.OrderId,
                    Type = "Payment",
                    Amount = -total,
                    BalanceAfter = wallet.Balance,
                    Description = $"Thanh toán đơn #{order.OrderId}",
                    CreatedDate = DateTime.Now
                });

                var walletMethodId = await GetPaymentMethodIdAsync("Wallet");
                _context.Payments.Add(new Payment
                {
                    OrderId = order.OrderId,
                    PaymentMethodId = walletMethodId,
                    Amount = total,
                    PaymentDate = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();

            if (payWithWallet)
            {
                _context.CartItems.RemoveRange(items);
                await _context.SaveChangesAsync();
                TempData["OrderMessage"] = "Đã thanh toán đơn hàng bằng ví.";
                return RedirectToAction("OrderDetails", "Account", new { id = order.OrderId });
            }

            return RedirectToAction(nameof(BankTransfer), new { id = order.OrderId });
        }

        [HttpGet]
        public async Task<IActionResult> BankTransfer(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.OrderId == id);
            if (order == null)
            {
                return NotFound();
            }

            var isPaid = order.Payments.Any();
            if (isPaid)
            {
                var cartId = HttpContext.Session.GetInt32("CartId");
                if (cartId.HasValue)
                {
                    var cartItems = _context.CartItems.Where(c => c.CartId == cartId.Value);
                    _context.CartItems.RemoveRange(cartItems);
                    await _context.SaveChangesAsync();
                }
            }

            return View(new BankTransferPaymentViewModel
            {
                Order = order,
                PaymentCode = _bankTransferService.BuildPaymentCode(order.OrderId),
                QrUrl = _bankTransferService.BuildVietQrUrl(order),
                IsPaid = isPaid
            });
        }

        [HttpGet]
        public async Task<IActionResult> BankTransferStatus(int id)
        {
            var isPaid = await _context.Payments.AnyAsync(p => p.OrderId == id && (p.Amount ?? 0) > 0);
            if (!isPaid)
            {
                var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == id);
                if (order != null)
                {
                    var paymentCode = _bankTransferService.BuildPaymentCode(order.OrderId);
                    var found = await _sePayLookupService.HasIncomingTransactionAsync(paymentCode, order.TotalAmount ?? 0m, order.OrderDate);
                    if (found)
                    {
                        await CompleteBankTransferPaymentAsync(new SePayWebhookPayload
                        {
                            Code = paymentCode,
                            Content = paymentCode,
                            TransferType = "in",
                            TransferAmount = (long)Math.Round(order.TotalAmount ?? 0m, 0, MidpointRounding.AwayFromZero)
                        });
                        isPaid = true;
                    }
                }
            }

            return Json(new { isPaid });
        }

        [HttpPost("/webhooks/sepay")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SePayWebhook()
        {
            using var reader = new StreamReader(Request.Body);
            var rawBody = await reader.ReadToEndAsync();

            if (!await _bankTransferService.ValidateSePayRequestAsync(Request, rawBody))
            {
                _logger.LogWarning("SePay webhook unauthorized. Body: {Body}", rawBody);
                return Unauthorized(new { success = false, message = "Unauthorized" });
            }

            var payload = JsonSerializer.Deserialize<SePayWebhookPayload>(
                rawBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (payload == null)
            {
                _logger.LogWarning("SePay webhook payload is empty or invalid JSON. Body: {Body}", rawBody);
                return Ok(new { success = true });
            }

            var completed = await CompleteBankTransferPaymentAsync(payload);
            if (!completed)
            {
                _logger.LogWarning(
                    "SePay webhook did not match any payment. Id={Id}, Type={Type}, Amount={Amount}, Code={Code}, Content={Content}, Description={Description}, Reference={Reference}",
                    payload.Id,
                    payload.TransferType,
                    payload.TransferAmount,
                    payload.Code,
                    payload.Content,
                    payload.Description,
                    payload.ReferenceCode);
            }

            return Ok(new { success = true });
        }

        private async Task<bool> CompleteBankTransferPaymentAsync(SePayWebhookPayload payload)
        {
            if (string.Equals(payload.TransferType, "out", StringComparison.OrdinalIgnoreCase) ||
                payload.TransferAmount <= 0)
            {
                return false;
            }

            var searchableContent = BuildSePaySearchableContent(payload);
            var orderId = _bankTransferService.ExtractOrderId(payload.Code, searchableContent);
            if (!orderId.HasValue)
            {
                return await CompleteAdvertisingPaymentAsync(payload) ||
                       await CompleteWalletTopUpAsync(payload);
            }

            var order = await _context.Orders
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.OrderId == orderId.Value);
            if (order == null)
            {
                return false;
            }

            var expectedAmount = (long)Math.Round(order.TotalAmount ?? 0, 0, MidpointRounding.AwayFromZero);
            if (expectedAmount != payload.TransferAmount)
            {
                _logger.LogWarning(
                    "SePay order amount mismatch. OrderId={OrderId}, Expected={Expected}, Actual={Actual}",
                    order.OrderId,
                    expectedAmount,
                    payload.TransferAmount);
                return false;
            }

            var methodId = await _context.PaymentMethods
                .Where(m => m.MethodName == "BankTransfer")
                .Select(m => (int?)m.PaymentMethodId)
                .FirstOrDefaultAsync();

            if (!methodId.HasValue)
            {
                var method = new PaymentMethod { MethodName = "BankTransfer" };
                _context.PaymentMethods.Add(method);
                await _context.SaveChangesAsync();
                methodId = method.PaymentMethodId;
            }

            var hasPayment = order.Payments.Any(p => p.PaymentMethodId == methodId);
            if (!hasPayment)
            {
                _context.Payments.Add(new Payment
                {
                    OrderId = order.OrderId,
                    PaymentMethodId = methodId,
                    Amount = order.TotalAmount,
                    PaymentDate = DateTime.Now
                });
            }

            order.StatusId = await GetStatusIdAsync("Cho nguoi ban xac nhan");
            await _context.SaveChangesAsync();

            return true;
        }

        private async Task<bool> CompleteAdvertisingPaymentAsync(SePayWebhookPayload payload)
        {
            var searchableContent = BuildSePaySearchableContent(payload);
            var requestId = _bankTransferService.ExtractAdvertisingRequestId(payload.Code, searchableContent);
            if (!requestId.HasValue)
            {
                return false;
            }

            var request = await _context.AdvertisingPaymentRequests
                .Include(r => r.Product)
                .FirstOrDefaultAsync(r => r.RequestId == requestId.Value);
            if (request == null || request.Product == null)
            {
                return false;
            }

            var expectedAmount = (long)Math.Round(request.Amount, 0, MidpointRounding.AwayFromZero);
            if (expectedAmount != payload.TransferAmount)
            {
                _logger.LogWarning(
                    "SePay advertising amount mismatch. RequestId={RequestId}, Expected={Expected}, Actual={Actual}",
                    request.RequestId,
                    expectedAmount,
                    payload.TransferAmount);
                return false;
            }

            if (string.Equals(request.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            request.Status = "Paid";
            request.ConfirmedDate = DateTime.Now;

            request.Product.AdvertisingBudget = request.Amount;
            request.Product.AdvertisingPackageCode = request.PackageCode;
            request.Product.AdvertisingPackageName = request.PackageName;
            request.Product.AdvertisingDays = request.Days;
            request.Product.AdvertisingPaidDate = DateTime.Now;
            request.Product.AdvertisingEndDate = DateTime.Now.AddDays(request.Days);

            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<bool> CompleteWalletTopUpAsync(SePayWebhookPayload payload)
        {
            var searchableContent = BuildSePaySearchableContent(payload);
            var requestId = _bankTransferService.ExtractWalletTopUpRequestId(payload.Code, searchableContent);
            if (!requestId.HasValue)
            {
                return false;
            }

            var request = await _context.WalletTopUpRequests
                .FirstOrDefaultAsync(r => r.RequestId == requestId.Value);
            if (request == null)
            {
                return false;
            }

            var expectedAmount = (long)Math.Round(request.Amount, 0, MidpointRounding.AwayFromZero);
            if (expectedAmount != payload.TransferAmount)
            {
                _logger.LogWarning(
                    "SePay wallet top-up amount mismatch. RequestId={RequestId}, Expected={Expected}, Actual={Actual}",
                    request.RequestId,
                    expectedAmount,
                    payload.TransferAmount);
                return false;
            }

            if (string.Equals(request.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            request.Status = "Paid";
            request.ConfirmedDate = DateTime.Now;

            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == request.UserId);
            if (wallet == null)
            {
                wallet = new Wallet
                {
                    UserId = request.UserId,
                    Balance = 0,
                    CreatedDate = DateTime.Now,
                    UpdatedDate = DateTime.Now
                };
                _context.Wallets.Add(wallet);
            }

            var topUpDescription = $"Nap vi #{request.RequestId}";
            var alreadyCredited = await _context.WalletTransactions.AnyAsync(t =>
                t.WalletId == wallet.WalletId &&
                t.Type == "TopUp" &&
                t.Description == topUpDescription);

            if (!alreadyCredited)
            {
                wallet.Balance += request.Amount;
                wallet.UpdatedDate = DateTime.Now;
                _context.WalletTransactions.Add(new WalletTransaction
                {
                    WalletId = wallet.WalletId,
                    Wallet = wallet,
                    Type = "TopUp",
                    Amount = request.Amount,
                    BalanceAfter = wallet.Balance,
                    Description = topUpDescription,
                    CreatedDate = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }

        private static string BuildSePaySearchableContent(SePayWebhookPayload payload)
        {
            return string.Join(" ", new[]
            {
                payload.Code,
                payload.Content,
                payload.Description,
                payload.ReferenceCode
            }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }
    }
}


