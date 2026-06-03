using EXE.Models;
using EXE.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EXE.Controllers
{
	public class GameController : Controller
	{
		private readonly ApplicationDbContext _db;

		public GameController(ApplicationDbContext db)
		{
			_db = db;
		}

		[HttpGet]
		public async Task<IActionResult> Index()
		{
			// Lấy một tập sản phẩm đủ lớn để tạo câu hỏi đa dạng.
			// Ưu tiên sản phẩm có ảnh, có vùng miền/danh mục.
			var raw = await _db.Products
				.AsNoTracking()
				.Include(p => p.ProductImages)
				.Include(p => p.Region)
				.Include(p => p.Category)
				.Where(p => p.ApprovalStatus == "Approved")
				.OrderByDescending(p => p.CreatedDate)
				.Take(400)
				.ToListAsync();

			var products = raw
				.Where(p => !string.IsNullOrWhiteSpace(p.ProductName))
				.Select(p => new GameProductDto
				{
					ProductId = p.ProductId,
					Name = p.ProductName!.Trim(),
					Description = p.Description,
					Price = p.Price,
					Category = p.Category?.CategoryName,
					Region = p.Region?.RegionName,
					Province = p.Region?.Province,
					ImageUrl = p.ProductImages.FirstOrDefault()?.ImageUrl ?? "no-image.svg"
				})
				.ToList();

			// Fallback: nếu DB chưa có dữ liệu, vẫn render trang chơi với dataset rỗng (view sẽ tự dùng demo).
			var vm = new GameIndexViewModel { Products = products };
			return View(vm);
		}
	}
}

