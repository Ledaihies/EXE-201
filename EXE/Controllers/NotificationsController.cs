using EXE.Services;
using Microsoft.AspNetCore.Mvc;

namespace EXE.Controllers;

public class NotificationsController : Controller
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task<IActionResult> Index()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue) return RedirectToAction("Login", "Auth");

        var notifications = await _notificationService.GetUserNotifications(userId.Value, 100);
        ViewBag.UnreadCount = await _notificationService.GetUnreadCount(userId.Value);
        return View(notifications);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue) return RedirectToAction("Login", "Auth");

        await _notificationService.MarkAsRead(id, userId.Value);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue) return RedirectToAction("Login", "Auth");

        await _notificationService.MarkAllAsRead(userId.Value);
        return RedirectToAction(nameof(Index));
    }
}
