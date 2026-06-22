using EXE.Models;
using EXE.Security;
using Microsoft.EntityFrameworkCore;

namespace EXE.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;

    public NotificationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Notification> CreateNotification(int userId, string title, string message, string type, string? relatedEntityType = null, int? relatedEntityId = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title?.Trim() ?? string.Empty,
            Message = message?.Trim() ?? string.Empty,
            Type = string.IsNullOrWhiteSpace(type) ? "System" : type.Trim(),
            RelatedEntityType = string.IsNullOrWhiteSpace(relatedEntityType) ? null : relatedEntityType.Trim(),
            RelatedEntityId = relatedEntityId,
            IsRead = false,
            CreatedAt = DateTime.Now
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
        return notification;
    }

    public async Task<int> CreateNotificationForRole(string roleName, string title, string message, string type, string? relatedEntityType = null, int? relatedEntityId = null)
    {
        var normalized = RoleAccess.Normalize(roleName);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return 0;
        }

        var users = await _context.Users
            .Include(u => u.Role)
            .AsNoTracking()
            .ToListAsync();

        var created = 0;
        foreach (var userId in users
            .Where(u => u.Role != null && RoleAccess.IsRole(u.Role.RoleName, normalized))
            .Select(u => u.UserId)
            .Distinct())
        {
            await CreateNotification(userId, title, message, type, relatedEntityType, relatedEntityId);
            created++;
        }

        return created;
    }

    public async Task<bool> MarkAsRead(int notificationId, int userId)
    {
        var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == userId);
        if (notification == null) return false;

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await _context.SaveChangesAsync();
        }

        return true;
    }

    public async Task<int> MarkAllAsRead(int userId)
    {
        var unread = await _context.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync();
        if (!unread.Any()) return 0;

        foreach (var item in unread)
        {
            item.IsRead = true;
        }

        await _context.SaveChangesAsync();
        return unread.Count;
    }

    public Task<int> GetUnreadCount(int userId)
    {
        return _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public Task<List<Notification>> GetUserNotifications(int userId, int take = 50)
    {
        return _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.NotificationId)
            .Take(take)
            .ToListAsync();
    }
}
