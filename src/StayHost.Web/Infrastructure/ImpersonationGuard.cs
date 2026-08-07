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
        if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) || !Impersonation.BlocksPath(path))
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
            .Where(s => s.AdminUserId == admin.Id && s.EndedAt == null && s.ExpiresAt > now)
            .FirstOrDefaultAsync(ctx.RequestAborted);

        if (session is null)
        {
            await next(ctx);
            return;
        }

        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        await ctx.Response.WriteAsJsonAsync(new
        {
            message = "Không được thực hiện thao tác này khi đang ở chế độ thay mặt người dùng.",
            forbidden = Impersonation.ForbiddenLabels
        }, ctx.RequestAborted);
    }
}
