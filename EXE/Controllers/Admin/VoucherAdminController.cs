using EXE.Models;
using EXE.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EXE.Controllers.Admin;

[AdminOnly]
public class VoucherAdminController : Controller
{
    private readonly ApplicationDbContext _context;

    public VoucherAdminController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.Categories = await _context.Categories.OrderBy(c => c.CategoryName).ToListAsync();
        ViewBag.Regions = await _context.Regions.OrderBy(r => r.RegionName).ToListAsync();

        var vouchers = await _context.Vouchers
            .Include(v => v.Category)
            .Include(v => v.Region)
            .OrderByDescending(v => v.IsActive)
            .ThenByDescending(v => v.VoucherId)
            .ToListAsync();

        return View(vouchers);
    }

    [HttpPost]
    public async Task<IActionResult> Save(Voucher input)
    {
        if (string.IsNullOrWhiteSpace(input.Code))
        {
            TempData["VoucherAdminError"] = "Vui lòng nhập mã.";
            return RedirectToAction(nameof(Index));
        }

        if (!input.IsFreeShipping && (input.DiscountPercent ?? 0) <= 0 && (input.DiscountFixed ?? 0) <= 0)
        {
            TempData["VoucherAdminError"] = "Vui lòng nhập giá trị giảm giá hoặc chọn miễn phí vận chuyển.";
            return RedirectToAction(nameof(Index));
        }

        var normalizedCode = input.Code.Trim().ToUpperInvariant();
        var duplicate = await _context.Vouchers.AnyAsync(v => v.VoucherId != input.VoucherId && v.Code != null && v.Code.ToUpper() == normalizedCode);
        if (duplicate)
        {
            TempData["VoucherAdminError"] = "Mã này đã tồn tại.";
            return RedirectToAction(nameof(Index));
        }

        Voucher voucher;
        if (input.VoucherId > 0)
        {
            voucher = await _context.Vouchers.FindAsync(input.VoucherId) ?? new Voucher();
            if (voucher.VoucherId == 0) _context.Vouchers.Add(voucher);
        }
        else
        {
            voucher = new Voucher();
            _context.Vouchers.Add(voucher);
        }

        voucher.Code = normalizedCode;
        voucher.Description = input.Description?.Trim();
        voucher.DiscountPercent = input.IsFreeShipping ? null : input.DiscountPercent;
        voucher.DiscountFixed = input.IsFreeShipping ? null : input.DiscountFixed;
        voucher.IsFreeShipping = input.IsFreeShipping;
        voucher.MinOrderAmount = input.MinOrderAmount;
        voucher.StartDate = input.StartDate;
        voucher.ExpiryDate = input.ExpiryDate;
        voucher.MaxUses = input.MaxUses;
        voucher.RegionId = input.RegionId;
        voucher.CategoryId = input.CategoryId;
        voucher.OccasionTag = input.OccasionTag?.Trim();
        voucher.IsActive = input.IsActive;

        await _context.SaveChangesAsync();
        TempData["VoucherAdminMessage"] = "Đã lưu mã ưu đãi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var voucher = await _context.Vouchers.FindAsync(id);
        if (voucher == null)
        {
            TempData["VoucherAdminError"] = "Không tìm thấy mã.";
            return RedirectToAction(nameof(Index));
        }

        var used = await _context.OrderVouchers.AnyAsync(ov => ov.VoucherId == id);
        if (used)
        {
            voucher.IsActive = false;
            TempData["VoucherAdminMessage"] = "Mã đã có lịch sử sử dụng nên được chuyển sang ngừng hoạt động.";
        }
        else
        {
            _context.Vouchers.Remove(voucher);
            TempData["VoucherAdminMessage"] = "Đã xóa mã ưu đãi.";
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
