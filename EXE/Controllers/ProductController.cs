using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EXE.Models;

namespace EXE.Controllers
{
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? q, int? regionId, int? categoryId, string? occasion, decimal? minPrice, decimal? maxPrice, string? sort, int page = 1, int pageSize = 12)
        {
            var query = _context.Products
                .Include(p => p.ProductImages)
                .Include(p => p.Region)
                .Include(p => p.Category)
                .Where(p => p.ApprovalStatus == "Approved")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var keyword = q.Trim();
                var keywordLower = keyword.ToLower();

                query = query.Where(p =>
                    (p.ProductName != null && p.ProductName.ToLower().Contains(keywordLower)) ||
                    (p.Description != null && p.Description.ToLower().Contains(keywordLower)) ||
                    (p.Region != null && p.Region.RegionName != null && p.Region.RegionName.ToLower().Contains(keywordLower)) ||
                    (p.Category != null && p.Category.CategoryName != null && p.Category.CategoryName.ToLower().Contains(keywordLower)));
            }

            if (regionId.HasValue)
                query = query.Where(p => p.RegionId == regionId.Value);

            // Bộ lọc theo dịp (occasion) đã được gỡ bỏ để không phụ thuộc bảng Occasions/ProductOccasions.

            if (categoryId.HasValue)
                query = query.Where(p => p.CategoryId == categoryId.Value);

            if (minPrice.HasValue)
            {
                var min = minPrice.Value;
                query = query.Where(p => (p.Price ?? 0) >= min);
            }

            if (maxPrice.HasValue)
            {
                var max = maxPrice.Value;
                query = query.Where(p => (p.Price ?? 0) <= max);
            }

            query = sort switch
            {
                "price_asc" => query.OrderBy(p => p.Price ?? 0),
                "price_desc" => query.OrderByDescending(p => p.Price ?? 0),
                "newest" => query.OrderByDescending(p => p.CreatedDate),
                _ => query.OrderByDescending(p => p.ProductId)
            };

            var totalCount = await query.CountAsync();
            if (page < 1) page = 1;
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            if (totalPages == 0) totalPages = 1;
            if (page > totalPages) page = totalPages;

            var products = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Query = q;
            ViewBag.RegionId = regionId;
            ViewBag.CategoryId = categoryId;
            ViewBag.Occasion = occasion;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.Sort = sort;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;

            // Lọc trùng vùng miền (nếu có bản ghi trùng tên vùng).
            ViewBag.Regions = await _context.Regions
                .OrderBy(r => r.RegionName)
                .GroupBy(r => r.RegionName)
                .Select(g => g.First())
                .ToListAsync();
            ViewBag.Categories = await _context.Categories.OrderBy(c => c.CategoryName).ToListAsync();

            return View(products);
        }

        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Include(p => p.ProductImages)
                .Include(p => p.Region)
                .FirstOrDefaultAsync(p => p.ProductId == id && p.ApprovalStatus == "Approved");

            if (product == null)
                return NotFound();

            product.ViewCount = (product.ViewCount ?? 0) + 1;
            await _context.SaveChangesAsync();

            var reviews = await _context.Reviews
                .Include(r => r.User)
                .Where(r => r.ProductId == id)
                .OrderByDescending(r => r.CreatedDate)
                .Take(20)
                .ToListAsync();

            var avgRating = reviews.Any() ? reviews.Average(r => r.Rating ?? 0) : 0;
            var reviewCount = reviews.Count;

            ViewBag.Reviews = reviews;
            ViewBag.AvgRating = avgRating;
            ViewBag.ReviewCount = reviewCount;

            // Gợi ý thêm đặc sản khác cùng vùng miền / danh mục.
            var relatedQuery = _context.Products
                .Include(p => p.ProductImages)
                .Include(p => p.Region)
                .Where(p => p.ProductId != id && p.ApprovalStatus == "Approved");

            if (product.RegionId.HasValue)
            {
                relatedQuery = relatedQuery.Where(p => p.RegionId == product.RegionId);
            }
            else if (product.CategoryId.HasValue)
            {
                relatedQuery = relatedQuery.Where(p => p.CategoryId == product.CategoryId);
            }

            var related = await relatedQuery
                .OrderByDescending(p => p.CreatedDate)
                .Take(6)
                .ToListAsync();

            ViewBag.RelatedProducts = related;

            return View(product);
        }
    }
}
