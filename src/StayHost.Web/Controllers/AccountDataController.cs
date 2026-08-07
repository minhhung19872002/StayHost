using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

/// <summary>
/// docs/01 TK-11 — "tải toàn bộ dữ liệu cá nhân của tôi".
///
/// One file, downloaded there and then, rather than a job that emails a link
/// later: the whole point is that somebody can see what is held about them
/// without asking anyone. Nothing here is summarised or prettied up — a receipt
/// the platform still holds is worth more to them than a nicer table.
/// </summary>
[ApiController]
[Route("api/account/data")]
public class AccountDataController(StayHostDbContext db, AuthService auth) : ControllerBase
{
    private static readonly JsonSerializerOptions Pretty = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var host = await db.Hosts.FirstOrDefaultAsync(h => h.UserId == user.Id, ct);

        var bookings = await db.Bookings
            .Where(b => b.GuestUserId == user.Id)
            .OrderBy(b => b.CheckIn)
            .Select(b => new
            {
                b.Reference, b.CheckIn, b.CheckOut, b.Nights, b.Guests,
                Listing = b.Listing!.Title,
                b.Listing.City,
                Status = b.Status.ToString(),
                b.Subtotal, b.CleaningFee, b.ServiceFee, b.Tax, b.Total,
                b.RefundedAmount, b.GoodwillCredit, b.GuestNote, b.CreatedAt
            })
            .ToListAsync(ct);

        var reviewsWritten = await db.Reviews
            .Where(r => r.AuthorUserId == user.Id)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new
            {
                Listing = r.Listing!.Title,
                r.Rating, r.Text, r.PrivateNote, r.CreatedAt, r.PublishedAt
            })
            .ToListAsync(ct);

        var reviewsReceived = await db.GuestReviews
            .Where(r => r.GuestUserId == user.Id && r.PublishedAt != null)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new { r.Rating, r.Text, r.WouldHostAgain, r.CreatedAt })
            .ToListAsync(ct);

        var messages = await db.Messages
            .Where(m => m.Thread!.GuestUserId == user.Id || m.Thread.HostUserId == user.Id)
            .OrderBy(m => m.SentAt)
            .Select(m => new
            {
                Thread = m.Thread!.Listing!.Title,
                Mine = m.SenderUserId == user.Id,
                m.Body, m.SentAt, m.ReadAt
            })
            .ToListAsync(ct);

        // docs/00 §6.8 — the balance is the sum of its rows, so the rows are
        // what somebody is owed an answer about.
        var credits = await db.CreditEntries
            .Where(c => c.UserId == user.Id)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new { c.Amount, Reason = c.Reason.ToString(), c.Memo, c.CreatedAt })
            .ToListAsync(ct);

        var notifications = await db.Notifications
            .Where(n => n.UserId == user.Id)
            .OrderBy(n => n.CreatedAt)
            .Select(n => new { Kind = n.Kind.ToString(), n.Title, n.Body, n.CreatedAt, n.ReadAt })
            .ToListAsync(ct);

        var sessions = await db.AuthSessions
            .Where(s => s.UserId == user.Id)
            .OrderBy(s => s.CreatedAt)
            .Select(s => new { s.UserAgent, s.CreatedAt, s.ExpiresAt, s.RevokedAt })
            .ToListAsync(ct);

        var logins = await db.ExternalLogins
            .Where(e => e.UserId == user.Id)
            .Select(e => new { Provider = e.Provider.ToString(), e.ProviderEmail, e.CreatedAt, e.LastUsedAt })
            .ToListAsync(ct);

        var claims = await db.ShieldClaims
            .Where(c => c.OpenedByUserId == user.Id)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new
            {
                c.Reference, Side = c.Side.ToString(), Kind = c.Kind.ToString(),
                Status = c.Status.ToString(), c.Claimed, c.Approved, c.Description, c.CreatedAt
            })
            .ToListAsync(ct);

        var export = new
        {
            ExportedAt = DateTime.UtcNow,
            Notice = "Bản sao dữ liệu cá nhân của bạn trên StayHost OS (docs/01 TK-11).",
            Account = new
            {
                user.Id, user.Email, user.Phone, user.FullName, user.DisplayName,
                user.DateOfBirth, user.Bio, user.Location, user.Occupation,
                Languages = Profiles.UnpackLanguages(user.SpokenLanguages),
                Interests = Profiles.UnpackInterests(user.Interests),
                user.AvatarUrl,
                Role = user.Role.ToString(),
                user.EmailConfirmed, user.PhoneConfirmed, user.IsIdentityVerified,
                user.CreatedAt
            },
            Hosting = host is null
                ? null
                : new { host.Name, host.IsSuperhost, host.JoinedAt, host.ResponseRate, host.ResponseTime },
            Bookings = bookings,
            ReviewsWritten = reviewsWritten,
            ReviewsReceived = reviewsReceived,
            Messages = messages,
            Credits = credits,
            ShieldClaims = claims,
            Notifications = notifications,
            Sessions = sessions,
            ExternalLogins = logins
        };

        var json = JsonSerializer.Serialize(export, Pretty);
        var name = $"stayhost-du-lieu-ca-nhan-{DateTime.UtcNow:yyyy-MM-dd}.json";

        return File(Encoding.UTF8.GetBytes(json), "application/json", name);
    }
}
