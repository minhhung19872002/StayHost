using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;

namespace StayHost.Web.Services;

/// <summary>
/// Writes an in-app notification and queues the matching email in one step, so a
/// feature never remembers to do one and forget the other.
/// </summary>
public class NotificationService(StayHostDbContext db, ILogger<NotificationService> log)
{
    /// <summary>Queues without saving; the caller's SaveChanges commits it with the rest.</summary>
    public void Queue(int userId, NotificationKind kind, string title, string body, string? link = null)
    {
        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Kind = kind,
            Title = title,
            Body = body,
            Link = link
        });
    }

    public async Task QueueWithEmailAsync(User? recipient, NotificationKind kind,
        string title, string body, string? link, CancellationToken ct)
    {
        if (recipient is null) return;

        Queue(recipient.Id, kind, title, body, link);

        // Somebody who signed up with a phone has no address to write to
        // (docs/01 TK-01); the in-app notification above is all they get.
        if (string.IsNullOrWhiteSpace(recipient.Email))
        {
            log.LogInformation("No email on file for user {UserId}; in-app only.", recipient.Id);
            return;
        }

        db.EmailMessages.Add(new EmailMessage
        {
            ToEmail = recipient.Email,
            ToName = recipient.FullName,
            Subject = title,
            Body = BuildEmailBody(recipient.FullName, title, body, link)
        });

        log.LogInformation("Queued {Kind} notification for user {UserId}.", kind, recipient.Id);
        await Task.CompletedTask;
    }

    public Task<int> UnreadCountAsync(int userId, CancellationToken ct) =>
        db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null, ct);

    public Task<List<Notification>> RecentAsync(int userId, int take, CancellationToken ct) =>
        db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

    public async Task MarkAllReadAsync(int userId, CancellationToken ct)
    {
        var unread = await db.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ToListAsync(ct);

        foreach (var n in unread) n.ReadAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static string BuildEmailBody(string name, string title, string body, string? link)
    {
        var cta = link is null ? "" : $"\n\nXem chi tiết: https://stayhost.vn{link}";
        return $"Chào {name},\n\n{title}\n\n{body}{cta}\n\n— Đội ngũ StayHost";
    }
}
