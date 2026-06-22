using EXE.Models;

namespace EXE.ViewModels;

public class NotificationBellViewModel
{
    public int UnreadCount { get; set; }
    public List<Notification> Notifications { get; set; } = new();
}
