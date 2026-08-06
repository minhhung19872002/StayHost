using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;

namespace StayHost.Web.Services;

/// <summary>
/// docs/00 §3.4 and docs/01 QT-09 — the two things every admin action needs:
/// a scope check before it runs, and a row afterwards saying who did what.
/// </summary>
public class AdminAudit(StayHostDbContext db, AuthService auth)
{
    /// <summary>
    /// The signed-in admin if they hold <paramref name="scope"/>, otherwise null.
    /// Super covers everything, so it is checked as well as the exact flag.
    /// </summary>
    public async Task<User?> RequireAsync(AdminScope scope, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user?.Role != UserRole.Admin) return null;

        return user.AdminScope.HasFlag(scope) || user.AdminScope.HasFlag(AdminScope.Super) ? user : null;
    }

    /// <summary>Adds the audit row. The caller still owns the SaveChanges.</summary>
    public void Record(User admin, string action, string target, string? before, string? after, string? note = null) =>
        db.AdminAudit.Add(new AdminAuditEntry
        {
            ActorUserId = admin.Id,
            Action = action,
            Target = target,
            Before = Trim(before),
            After = Trim(after),
            Note = Trim(note)
        });

    private static string? Trim(string? v) =>
        string.IsNullOrWhiteSpace(v) ? null : v.Length > 2000 ? v[..2000] : v;

    /// <summary>The log itself, newest first, for the admin console.</summary>
    public async Task<List<AdminAuditRow>> RecentAsync(int take, CancellationToken ct) =>
        await db.AdminAudit
            .Include(a => a.ActorUser)
            .OrderByDescending(a => a.CreatedAt)
            .Take(take)
            .Select(a => new AdminAuditRow(
                a.ActorUser!.FullName, a.Action, a.Target, a.Before, a.After, a.Note, a.CreatedAt))
            .ToListAsync(ct);

    public record AdminAuditRow(
        string Actor, string Action, string Target, string? Before, string? After, string? Note, DateTime At);
}
