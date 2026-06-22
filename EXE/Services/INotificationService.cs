using EXE.Models;

namespace EXE.Services;

public interface INotificationService
{
    Task<Notification> CreateNotification(int userId, string title, string message, string type, string? relatedEntityType = null, int? relatedEntityId = null);
    Task<int> CreateNotificationForRole(string roleName, string title, string message, string type, string? relatedEntityType = null, int? relatedEntityId = null);
    Task<bool> MarkAsRead(int notificationId, int userId);
    Task<int> MarkAllAsRead(int userId);
    Task<int> GetUnreadCount(int userId);
    Task<List<Notification>> GetUserNotifications(int userId, int take = 50);
}
