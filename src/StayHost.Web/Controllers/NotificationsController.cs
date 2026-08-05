using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayHost.Infrastructure;
using StayHost.Web.Contracts;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationsController(StayHostDbContext db, AuthService auth, NotificationService notifications)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<NotificationFeedDto>> Feed(CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Ok(new NotificationFeedDto(0, []));

        var items = await notifications.RecentAsync(user.Id, 30, ct);
        var unread = items.Count(n => n.ReadAt is null);

        return Ok(new NotificationFeedDto(unread, items.Select(n => new NotificationDto(
            n.Id, n.Kind.ToString(), n.Title, n.Body, n.Link, n.ReadAt is null, n.CreatedAt)).ToList()));
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll(CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        await notifications.MarkAllReadAsync(user.Id, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> Read(int id, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var item = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == user.Id, ct);
        if (item is null) return NotFound();

        item.ReadAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
