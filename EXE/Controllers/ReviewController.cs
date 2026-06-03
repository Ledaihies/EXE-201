using Microsoft.AspNetCore.Mvc;
using EXE.Models;

namespace EXE.Controllers
{
    public class ReviewController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReviewController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var reviews = _context.Reviews.ToList();

            return View(reviews);
        }
    }
}