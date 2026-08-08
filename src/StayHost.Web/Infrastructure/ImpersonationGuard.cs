using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Services;

namespace StayHost.Web.Infrastructure;

/// <summary>
/// docs/08 §7.6 — "Cấm tuyệt đối trong chế độ này."
///
/// It is middleware rather than a check in each controller because the list is
/// absolute: nine things that must not happen no matter which endpoint somebody
/// reaches for. Enforcing it per-controller would mean nine chances to add a
/// tenth route and forget.
/// </summary>
public class ImpersonationGuard(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, StayHostDbContext db, AuthService auth)
    {
        var method = ctx.Request.Method;

        // Reading is the whole point of the mode; only changes are policed.
        if (method is "GET" or "HEAD" or "OPTIONS")
        {
            await next(ctx);
            return;
        }

        var path = ctx.Request.Path.Value ?? "";
        if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            await next(ctx);
            return;
        }

        var admin = await auth.CurrentUserAsync(ctx.RequestAborted);
        if (admin is null || admin.Role != UserRole.Admin)
        {
            await next(ctx);
            return;
        }

        var now = DateTime.UtcNow;

        var session = await db.ImpersonationSessions
            .Include(s => s.TargetUser)
            .Where(s => s.AdminUserId == admin.Id && s.EndedAt == null && s.ExpiresAt > now)
            .FirstOrDefaultAsync(ctx.RequestAborted);

        if (session is null)
        {
            await next(ctx);
            return;
        }

        if (Impersonation.BlocksPath(path))
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsJsonAsync(new
            {
                message = "Không được thực hiện thao tác này khi đang ở chế độ thay mặt người dùng.",
                forbidden = Impersonation.ForbiddenLabels
            }, ctx.RequestAborted);
            return;
        }

        // docs/08 §7.7 — every write during the session is logged as "admin X
        // thay mặt Y", never as though the person did it themselves. Written
        // before the action runs, so even a request that then fails leaves the
        // attempt on the record.
        db.AdminAudit.Add(new AdminAuditEntry
        {
            ActorUserId = admin.Id,
            Action = "impersonation.action",
            Target = $"user:{session.TargetUserId}",
            After = $"{method} {path}",
            Note = Impersonation.ActorTag(admin.FullName, session.TargetUser?.FullName ?? $"#{session.TargetUserId}")
        });
        await db.SaveChangesAsync(ctx.RequestAborted);

        await next(ctx);
    }
}
