using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Infrastructure;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

/// <summary>
/// docs/08 §4 — the only door to an identity-document image.
///
/// Two people may open it: the person the document belongs to, and an admin who
/// has just been through the reasoned-view gate of QT-U-11 — proven not by a
/// claim but by the audit line that gate wrote. No recent audit line, no file:
/// the URL alone is worthless, which is the property the public /uploads path
/// could never give.
/// </summary>
[ApiController]
[Route("api/identity-files")]
public partial class IdentityFilesController(
    StayHostDbContext db, AuthService auth, IWebHostEnvironment env) : ControllerBase
{
    /// <summary>How long one reasoned view keeps the images openable.</summary>
    internal static readonly TimeSpan ViewWindow = TimeSpan.FromMinutes(15);

    internal static string Root(IWebHostEnvironment env) =>
        Path.Combine(env.ContentRootPath, "protected", "identity");

    [GeneratedRegex(@"^\d+-[0-9a-f]{32}\.(jpg|png|webp|avif)$")]
    private static partial Regex SafeName();

    [HttpGet("{name}")]
    public async Task<IActionResult> Serve(string name, CancellationToken ct)
    {
        // The name is server-generated on upload; anything else is not a file of ours.
        if (!SafeName().IsMatch(name)) return NotFound();

        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized();

        var url = $"/api/identity-files/{name}";
        var owner = await db.IdentityChecks
            .Where(c => c.FrontImageUrl == url || c.BackImageUrl == url || c.SelfieImageUrl == url)
            .Select(c => (int?)c.UserId)
            .FirstOrDefaultAsync(ct);

        // An orphaned file (submission abandoned) is only its uploader's — the
        // name starts with the uploader's user id.
        owner ??= name.Split('-')[0] == user.Id.ToString() ? user.Id : null;

        if (owner is null) return NotFound();

        if (owner != user.Id && !await MayAdminViewAsync(user, owner.Value, ct))
            return this.Denied("Ảnh giấy tờ chỉ mở được sau khi ghi lý do xem trong hồ sơ người dùng.");

        var path = Path.Combine(Root(env), name);
        if (!System.IO.File.Exists(path)) return NotFound();

        var contentType = Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".avif" => "image/avif",
            _ => "image/jpeg"
        };

        // Never cached: the right to see this file expires with the view window.
        Response.Headers.CacheControl = "no-store";
        return PhysicalFile(path, contentType);
    }

    /// <summary>
    /// The admin path: role says may, the audit trail says did — a
    /// ViewIdentityDocuments read for this exact person, recent enough that it is
    /// still the same sitting.
    /// </summary>
    private async Task<bool> MayAdminViewAsync(User user, int ownerId, CancellationToken ct)
    {
        if (user.Role != UserRole.Admin) return false;
        if (!AdminActions.Allows(user.AdminScope, AdminAction.ViewIdentityDocuments)) return false;

        var since = DateTime.UtcNow - ViewWindow;
        var action = $"admin.read.{AdminAction.ViewIdentityDocuments}".ToLowerInvariant();

        return await db.AdminAudit.AnyAsync(
            a => a.ActorUserId == user.Id
                 && a.Action == action
                 && a.Target == $"user:{ownerId}"
                 && a.CreatedAt >= since, ct);
    }
}
