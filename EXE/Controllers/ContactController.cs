using Microsoft.AspNetCore.Mvc;
using EXE.Models;

namespace EXE.Controllers
{
    public class ContactController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ContactController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Send(ContactMessage msg)
        {
            _context.ContactMessages.Add(msg);

            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }
    }
}