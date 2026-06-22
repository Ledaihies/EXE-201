using EXE.Services;
using EXE.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace EXE.ViewComponents;

public class NotificationBellViewComponent : ViewComponent
{
    private readonly INotificationService _notificationService;

    public NotificationBellViewComponent(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (!userId.HasValue)
        {
            return View(new NotificationBellViewModel());
        }

        var model = new NotificationBellViewModel
        {
            UnreadCount = await _notificationService.GetUnreadCount(userId.Value),
            Notifications = await _notificationService.GetUserNotifications(userId.Value, 5)
        };

        return View(model);
    }
}
