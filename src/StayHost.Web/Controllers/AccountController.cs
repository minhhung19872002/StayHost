using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Contracts;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

[ApiController]
[Route("api/account")]
public class AccountController(AuthService auth, StayHostDbContext db) : ControllerBase
{
    /// <summary>204 when nobody is signed in, so clients get an unambiguous empty response.</summary>
    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserDto>> Me(CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        return user is null ? NoContent() : Ok(await ToDtoAsync(user, ct));
    }

    [HttpPost("register")]
    public async Task<ActionResult<CurrentUserDto>> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        var result = await auth.RegisterAsync(req.Email, req.Password, req.FullName, req.Phone, ct);
        if (!result.Ok) return BadRequest(new { message = result.Error });
        return Ok(await ToDtoAsync(result.User!, ct));
    }

    [HttpPost("login")]
    public async Task<ActionResult<CurrentUserDto>> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var result = await auth.LoginAsync(req.Email, req.Password, ct);
        if (!result.Ok) return Unauthorized(new { message = result.Error });
        return Ok(await ToDtoAsync(result.User!, ct));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(ct);
        return NoContent();
    }

    [HttpPut("profile")]
    public async Task<ActionResult<CurrentUserDto>> UpdateProfile([FromBody] UpdateProfileRequest req, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        if (!string.IsNullOrWhiteSpace(req.FullName))
        {
            user.FullName = req.FullName.Trim();
            user.Initials = AuthService.MakeInitials(user.FullName);
        }
        user.Phone = req.Phone?.Trim();
        user.Bio = req.Bio?.Trim();

        var host = await db.Hosts.FirstOrDefaultAsync(h => h.UserId == user.Id, ct);
        if (host is not null)
        {
            host.Name = user.FullName;
            host.Initials = user.Initials;
            host.Bio = user.Bio;
        }

        await db.SaveChangesAsync(ct);
        return Ok(await ToDtoAsync(user, ct));
    }

    /// <summary>Turns a guest account into a host account without publishing anything yet.</summary>
    [HttpPost("become-host")]
    public async Task<ActionResult<CurrentUserDto>> BecomeHost(CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        await auth.EnsureHostProfileAsync(user, ct);
        return Ok(await ToDtoAsync(user, ct));
    }

    private async Task<CurrentUserDto> ToDtoAsync(User user, CancellationToken ct)
    {
        var host = await db.Hosts.FirstOrDefaultAsync(h => h.UserId == user.Id, ct);
        var listingCount = host is null ? 0 : await db.Listings.CountAsync(l => l.HostId == host.Id, ct);
        var unread = await db.Messages
            .CountAsync(m => m.SenderUserId != user.Id && m.ReadAt == null &&
                             (m.Thread!.GuestUserId == user.Id || m.Thread.HostUserId == user.Id), ct);

        return new CurrentUserDto(
            user.Id, user.Email, user.FullName, user.Initials, user.Phone, user.Bio,
            user.Role.ToString(), host is not null, host?.Id, listingCount, unread,
            $"Tham gia StayHost tháng {user.CreatedAt.Month}, {user.CreatedAt.Year}");
    }
}
