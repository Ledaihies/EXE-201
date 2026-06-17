using EXE.Models;
using EXE.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EXE.Controllers.Admin;

[AdminOnly]
public class AdvertisingAdminController : Controller
{
    private readonly ApplicationDbContext _context;

    public AdvertisingAdminController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.Packages = await _context.AdvertisingPackages
            .OrderBy(p => p.Price)
            .ThenBy(p => p.Days)
            .ToListAsync();

        var requests = await _context.AdvertisingPaymentRequests
            .Include(r => r.Product)
            .Include(r => r.Seller)
            .OrderBy(r => r.Status == "Pending" ? 0 : 1)
            .ThenByDescending(r => r.CreatedDate)
            .ToListAsync();

        return View(requests);
    }

    [HttpPost]
    public async Task<IActionResult> SavePackage(AdvertisingPackage input)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Price < 0 || input.Days < 1)
        {
            TempData["AdvertisingAdminError"] = "Vui lòng nhập tên gói, số ngày và giá hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        AdvertisingPackage package;
        if (input.PackageId > 0)
        {
            package = await _context.AdvertisingPackages.FindAsync(input.PackageId) ?? new AdvertisingPackage();
            if (package.PackageId == 0) _context.AdvertisingPackages.Add(package);
        }
        else
        {
            package = new AdvertisingPackage { CreatedDate = DateTime.Now };
            _context.AdvertisingPackages.Add(package);
        }

        package.Code = string.IsNullOrWhiteSpace(input.Code)
            ? $"ad_{Guid.NewGuid():N}"[..12]
            : input.Code.Trim();
        package.Name = input.Name.Trim();
        package.Description = input.Description?.Trim();
        package.Price = input.Price;
        package.Days = input.Days;
        package.IsActive = input.IsActive;

        await _context.SaveChangesAsync();
        TempData["AdvertisingAdminMessage"] = "Đã lưu gói quảng cáo.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> DeletePackage(int id)
    {
        var package = await _context.AdvertisingPackages.FindAsync(id);
        if (package == null)
        {
            TempData["AdvertisingAdminError"] = "Không tìm thấy gói quảng cáo.";
            return RedirectToAction(nameof(Index));
        }

        var hasRequest = await _context.AdvertisingPaymentRequests.AnyAsync(r => r.PackageCode == package.Code);
        if (hasRequest)
        {
            package.IsActive = false;
            TempData["AdvertisingAdminMessage"] = "Gói đã có lịch sử thanh toán nên được chuyển sang ngừng bán.";
        }
        else
        {
            _context.AdvertisingPackages.Remove(package);
            TempData["AdvertisingAdminMessage"] = "Đã xóa gói quảng cáo.";
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ConfirmPayment(int id)
    {
        var request = await _context.AdvertisingPaymentRequests
            .Include(r => r.Product)
            .FirstOrDefaultAsync(r => r.RequestId == id);

        if (request == null)
        {
            TempData["AdvertisingAdminError"] = "Không tìm thấy yêu cầu quảng cáo.";
            return RedirectToAction(nameof(Index));
        }

        if (request.Product == null)
        {
            TempData["AdvertisingAdminError"] = "Sản phẩm trong yêu cầu không còn tồn tại.";
            return RedirectToAction(nameof(Index));
        }

        request.Status = "Paid";
        request.ConfirmedDate = DateTime.Now;
        request.ConfirmedByAdminId = HttpContext.Session.GetInt32("UserId");

        request.Product.AdvertisingBudget = request.Amount;
        request.Product.AdvertisingPackageCode = request.PackageCode;
        request.Product.AdvertisingPackageName = request.PackageName;
        request.Product.AdvertisingDays = request.Days;
        request.Product.AdvertisingPaidDate = DateTime.Now;
        request.Product.AdvertisingEndDate = DateTime.Now.AddDays(request.Days);

        await _context.SaveChangesAsync();
        TempData["AdvertisingAdminMessage"] = "Đã xác nhận thanh toán và bật quảng cáo cho sản phẩm.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> RejectPayment(int id)
    {
        var request = await _context.AdvertisingPaymentRequests.FindAsync(id);
        if (request == null)
        {
            TempData["AdvertisingAdminError"] = "Không tìm thấy yêu cầu quảng cáo.";
            return RedirectToAction(nameof(Index));
        }

        request.Status = "Rejected";
        await _context.SaveChangesAsync();
        TempData["AdvertisingAdminMessage"] = "Đã đánh dấu yêu cầu quảng cáo là chưa nhận được tiền.";
        return RedirectToAction(nameof(Index));
    }
}
