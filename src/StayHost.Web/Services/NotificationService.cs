using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;

namespace StayHost.Web.Services;

/// <summary>
/// Writes an in-app notification and queues the matching email in one step, so a
/// feature never remembers to do one and forget the other.
/// </summary>
public class NotificationService(
    StayHostDbContext db, ILogger<NotificationService> log, SiteSettings site)
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

        // docs/01 TK-10 — the in-app row is always written: it is the record,
        // and docs/03 §11 keeps transactional notices un-silenceable anyway.
        Queue(recipient.Id, kind, title, body, link);

        var topic = NotificationPrefs.TopicOf(kind);
        if (!NotificationPrefs.IsOn(recipient.NotificationMask, topic, NotificationChannel.Email))
        {
            log.LogInformation("User {UserId} turned off {Topic} email; in-app only.", recipient.Id, topic);
            return;
        }

        // Somebody who signed up with a phone has no address to write to
        // (docs/01 TK-01); the in-app notification above is all they get.
        if (string.IsNullOrWhiteSpace(recipient.Email))
        {
            log.LogInformation("No email on file for user {UserId}; in-app only.", recipient.Id);
            return;
        }

        // docs/01 TK-09 — the frame follows the reader's language right here,
        // hand-translated and exact. The CONTENT is still the Vietnamese the
        // call site composed; the dispatcher machine-translates it on the way
        // out, and if that fails the reader gets a correct frame around the
        // Vietnamese original — never a mail stuck waiting on a translator.
        var url = site.Absolute(link);
        db.EmailMessages.Add(new EmailMessage
        {
            ToEmail = recipient.Email,
            ToName = recipient.FullName,
            Subject = title,
            Body = Emails.Compose(recipient.Language, recipient.FullName, title, body, url),
            Language = recipient.Language,
            RawTitle = title,
            RawBody = body,
            CtaUrl = url
        });

        log.LogInformation("Queued {Kind} notification for user {UserId}.", kind, recipient.Id);
        await Task.CompletedTask;
    }

    /// <summary>
    /// docs/07 §2.5 — an email to somebody with no account.
    ///
    /// There is no in-app row to write and no preference mask to consult: both
    /// belong to an account, and this person has none. The address is the only
    /// thing the platform has of them, which is exactly why guest checkout asks
    /// for one and refuses to proceed without it — a booking reference nobody
    /// ever receives is a booking they cannot find again.
    /// </summary>
    public void QueueEmailOnly(string? toEmail, string? toName, string title, string body, string? link)
    {
        if (string.IsNullOrWhiteSpace(toEmail)) return;

        // No account means no language on file, and null means Vietnamese —
        // the only honest answer for a stranger.
        db.EmailMessages.Add(new EmailMessage
        {
            ToEmail = toEmail.Trim(),
            ToName = toName ?? "",
            Subject = title,
            Body = Emails.Compose(null, toName ?? "", title, body, site.Absolute(link))
        });

        log.LogInformation("Queued a guest-checkout email to a person with no account.");
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

    // The frame moved to Emails.Compose in the Domain, where the eight
    // hand-translated versions live together and a ninth language cannot be
    // added to the picker without the drift guard in EmailsTests noticing.
    // site.Absolute still decides whether a link line exists at all: a link the
    // reader cannot click is worse than no link, and a host written into the
    // source is exactly how mails pointed at a domain the platform does not own
    // for as long as they did.
}
