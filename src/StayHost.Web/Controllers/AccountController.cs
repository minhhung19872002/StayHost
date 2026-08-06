using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Contracts;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

[ApiController]
[Route("api/account")]
public class AccountController(AuthService auth, StayHostDbContext db, WalletService wallet) : ControllerBase
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

        // A referral code typed at signup, or simply the email somebody invited:
        // either way the new account is linked to whoever brought them here.
        await wallet.ClaimAsync(result.User!, req.ReferralCode, ct);

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

    /* ------------------------------------------------------------ password */

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var result = await auth.ChangePasswordAsync(user, req.CurrentPassword, req.NewPassword, ct);
        return result.Ok ? NoContent() : BadRequest(new { message = result.Error });
    }

    /// <summary>
    /// Always reports success so the endpoint cannot be used to discover which
    /// emails exist. In this build the link is returned directly instead of mailed.
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<ActionResult<object>> ForgotPassword([FromBody] ForgotPasswordRequest req, CancellationToken ct)
    {
        var token = await auth.BeginPasswordResetAsync(req.Email, ct);

        return Ok(new
        {
            message = "Nếu email tồn tại, chúng tôi đã gửi liên kết đặt lại mật khẩu.",
            resetLink = token is null ? null : $"/reset-password?token={token}"
        });
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult<CurrentUserDto>> ResetPassword([FromBody] ResetPasswordRequest req, CancellationToken ct)
    {
        var result = await auth.CompletePasswordResetAsync(req.Token, req.NewPassword, ct);
        if (!result.Ok) return BadRequest(new { message = result.Error });
        return Ok(await ToDtoAsync(result.User!, ct));
    }

    /* -------------------------------------------------------- verification */

    [HttpPost("send-verification")]
    public async Task<ActionResult<object>> SendVerification(CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });
        if (user.EmailConfirmed) return Ok(new { message = "Email đã được xác minh." });

        var token = await auth.BeginEmailVerificationAsync(user, ct);
        return Ok(new { message = "Đã gửi liên kết xác minh.", verifyLink = $"/verify-email?token={token}" });
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest req, CancellationToken ct)
    {
        var result = await auth.ConfirmEmailAsync(req.Token, ct);
        return result.Ok ? NoContent() : BadRequest(new { message = result.Error });
    }

    /* ------------------------------------------------------------- devices */

    [HttpGet("sessions")]
    public async Task<ActionResult<IReadOnlyList<SessionDto>>> Sessions(CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var current = auth.CurrentToken();
        var sessions = await auth.ActiveSessionsAsync(user.Id, ct);

        return Ok(sessions.Select(s => new SessionDto(
            s.Id, DescribeDevice(s.UserAgent), s.CreatedAt, s.ExpiresAt, s.Token == current)).ToList());
    }

    [HttpDelete("sessions/{id:int}")]
    public async Task<IActionResult> RevokeSession(int id, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        return await auth.RevokeSessionAsync(user.Id, id, ct) ? NoContent() : NotFound();
    }

    private static string DescribeDevice(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return "Thiết bị không xác định";

        var os = userAgent.Contains("Windows") ? "Windows"
            : userAgent.Contains("Mac OS") ? "macOS"
            : userAgent.Contains("Android") ? "Android"
            : userAgent.Contains("iPhone") || userAgent.Contains("iPad") ? "iOS"
            : userAgent.Contains("Linux") ? "Linux" : "Khác";

        var browser = userAgent.Contains("Edg/") ? "Edge"
            : userAgent.Contains("Chrome/") ? "Chrome"
            : userAgent.Contains("Firefox/") ? "Firefox"
            : userAgent.Contains("Safari/") ? "Safari" : "Trình duyệt khác";

        return $"{browser} trên {os}";
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
            user.EmailConfirmed,
            $"Tham gia StayHost tháng {user.CreatedAt.Month}, {user.CreatedAt.Year}");
    }
}
