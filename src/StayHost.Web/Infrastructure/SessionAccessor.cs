using Microsoft.AspNetCore.Mvc;

namespace StayHost.Web.Infrastructure;

/// <summary>
/// Wishlists and bookings belong to an anonymous browser identity. A single long-lived
/// cookie stands in for a real account so the demo needs no signup flow.
/// </summary>
public static class SessionAccessor
{
    public const string CookieName = "sh_sid";
    private const string ItemKey = "__sh_sid";

    public static string SessionId(this HttpContext ctx)
    {
        if (ctx.Items.TryGetValue(ItemKey, out var cached) && cached is string s) return s;

        var sid = ctx.Request.Cookies[CookieName];
        if (string.IsNullOrWhiteSpace(sid) || sid.Length > 64)
        {
            sid = Guid.NewGuid().ToString("N");
            ctx.Response.Cookies.Append(CookieName, sid, new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddYears(1)
            });
        }

        ctx.Items[ItemKey] = sid;
        return sid;
    }
}

/// <summary>
/// Sessions here are a cookie this app issues itself, with no ASP.NET
/// authentication scheme behind them — so <c>Forbid()</c> has nothing to hand
/// the request to and throws. Every refusal goes through this instead, and
/// comes back as a 403 with a message the interface can show.
/// </summary>
public static class DenyExtensions
{
    public static ObjectResult Denied(
        this ControllerBase controller, string message = "Bạn không có quyền với mục này.") =>
        controller.StatusCode(StatusCodes.Status403Forbidden, new { message });
}
