using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Contracts;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

[ApiController]
[Route("api/account")]
public class AccountController(
    AuthService auth, StayHostDbContext db, WalletService wallet, IdentityService identity,
    ExternalTokenVerifier verifier, ExternalLoginSettings externalLogin)
    : ControllerBase
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
        var result = await auth.RegisterAsync(
            req.Email, req.Password, req.FullName, req.Phone, ct, req.DateOfBirth);
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

    /* ------------------------------------------------- docs/01 TK-01: OTP */

    /// <summary>
    /// Sends a six-digit code to the account's phone or email. In development
    /// the code comes back in the response — there is no SMS provider behind
    /// this build and it is the only way to finish the flow end to end.
    /// </summary>
    [HttpPost("send-code")]
    public async Task<IActionResult> SendCode([FromBody] SendCodeRequest req, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var kind = ParseKind(req.Kind);
        var result = await identity.SendCodeAsync(user, kind, ct);

        return result.Ok
            ? Ok(new
            {
                message = $"Đã gửi mã tới {Identity.KindLabel(kind)} của bạn.",
                devCode = result.DevCode
            })
            : BadRequest(new { message = result.Error });
    }

    [HttpPost("confirm-code")]
    public async Task<ActionResult<CurrentUserDto>> ConfirmCode(
        [FromBody] ConfirmCodeRequest req, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var result = await identity.ConfirmCodeAsync(user, ParseKind(req.Kind), req.Code, ct);
        if (!result.Ok) return BadRequest(new { message = result.Error });

        return Ok(await ToDtoAsync(user, ct));
    }

    /// <summary>What is confirmed and what is linked, for the account screen.</summary>
    [HttpGet("verification")]
    public async Task<ActionResult<VerificationStateDto>> Verification(CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var linked = await identity.LinkedAsync(user.Id, ct);

        return Ok(new VerificationStateDto(
            string.IsNullOrWhiteSpace(user.Email) ? null : user.Email,
            user.EmailConfirmed,
            user.Phone,
            user.PhoneConfirmed,
            Identity.CodeLength,
            (int)Identity.CodeLifetime.TotalMinutes,
            linked.Select(l => new LinkedLoginDto(
                l.Provider.ToString().ToLower(), Identity.ProviderLabel(l.Provider),
                l.ProviderEmail, l.LastUsedAt)).ToList()));
    }

    /* --------------------------------- docs/01 TK-02: Google, Apple, Facebook */

    /// <summary>
    /// What the browser needs before it can put a provider's own button on screen.
    /// The ids are public by design — they end up in the page either way.
    /// </summary>
    [HttpGet("external/config")]
    public ActionResult<ExternalLoginConfigDto> ExternalConfig() =>
        Ok(new ExternalLoginConfigDto(
            externalLogin.HasGoogle ? externalLogin.GoogleClientId : null,
            externalLogin.HasApple ? externalLogin.AppleServicesId : null,
            externalLogin.HasApple ? externalLogin.AppleRedirectUri : null,
            externalLogin.HasFacebook ? externalLogin.FacebookAppId : null));

    [HttpPost("external")]
    public async Task<ActionResult<CurrentUserDto>> External(
        [FromBody] ExternalSignInRequest req, CancellationToken ct)
    {
        if (!Enum.TryParse<ExternalProvider>(req.Provider, true, out var provider))
            return BadRequest(new { message = "Chỉ hỗ trợ Google, Apple hoặc Facebook." });

        // Who somebody is comes out of the provider's signed token, never out of
        // the request body. Anything the browser claims about itself is ignored.
        var checkResult = await verifier.VerifyAsync(provider, req.Credential, ct);
        if (!checkResult.Ok) return BadRequest(new { message = checkResult.Error });

        var who = checkResult.Identity!;

        var result = await identity.SignInWithAsync(
            provider, who.Subject,
            // An unconfirmed address must not be allowed to attach itself to an
            // existing account; it is kept as a display detail only.
            who.EmailVerified ? who.Email : null,
            who.FullName, ct);

        if (!result.Ok) return BadRequest(new { message = result.Error });

        return Ok(await ToDtoAsync(result.User!, ct));
    }

    [HttpDelete("external/{provider}")]
    public async Task<IActionResult> Unlink(string provider, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        if (!Enum.TryParse<ExternalProvider>(provider, true, out var parsed))
            return BadRequest(new { message = "Nhà cung cấp không hợp lệ." });

        var error = await identity.UnlinkAsync(user, parsed, ct);
        return error is null ? NoContent() : BadRequest(new { message = error });
    }

    private static IdentifierKind ParseKind(string? kind) =>
        string.Equals(kind, "phone", StringComparison.OrdinalIgnoreCase)
            ? IdentifierKind.Phone
            : IdentifierKind.Email;

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

        if (!string.IsNullOrWhiteSpace(req.FullName)) user.FullName = req.FullName.Trim();
        user.Phone = req.Phone?.Trim();

        // docs/01 TK-04 — everything below is free text somebody typed, so it is
        // trimmed and capped here rather than trusted at the length the browser
        // happened to allow.
        user.Bio = Profiles.TidyBio(req.Bio);
        user.DisplayName = Profiles.Tidy(req.DisplayName, Profiles.LineMax);
        user.Location = Profiles.Tidy(req.Location, Profiles.LineMax);
        user.Occupation = Profiles.Tidy(req.Occupation, Profiles.LineMax);
        user.SpokenLanguages = Profiles.PackLanguages(req.Languages);
        user.Interests = Profiles.PackInterests(req.Interests) is { Length: > 0 } packed ? packed : null;

        if (string.IsNullOrWhiteSpace(req.AvatarUrl)) user.AvatarUrl = null;
        else if (Profiles.IsOwnUpload(req.AvatarUrl)) user.AvatarUrl = req.AvatarUrl.Trim();
        else return BadRequest(new { message = "Ảnh đại diện phải là ảnh bạn vừa tải lên." });

        // The grey circle stands in for the photo, so it has to spell the name
        // people actually see — otherwise "Hưng" sits next to a circle reading KD.
        var shown = Profiles.DisplayNameOf(user.DisplayName, user.FullName);
        user.Initials = Profiles.InitialsOf(shown);

        // The host card on a listing shows the same person, so it follows.
        var host = await db.Hosts.FirstOrDefaultAsync(h => h.UserId == user.Id, ct);
        if (host is not null)
        {
            host.Name = shown;
            host.Initials = user.Initials;
            host.Bio = user.Bio;
            host.AvatarUrl = user.AvatarUrl;
        }

        await db.SaveChangesAsync(ct);
        return Ok(await ToDtoAsync(user, ct));
    }

    /// <summary>docs/01 TK-04 — the spoken languages the editor may offer.</summary>
    [HttpGet("profile-options")]
    public ActionResult<IReadOnlyList<SpokenLanguageDto>> ProfileOptions() =>
        Ok(Profiles.SpokenLanguages.Select(l => new SpokenLanguageDto(l.Code, l.Label)).ToList());

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
            Profiles.JoinedLabel(user.CreatedAt),
            user.PhoneConfirmed,
            user.DisplayName,
            user.AvatarUrl,
            Profiles.UnpackLanguages(user.SpokenLanguages),
            user.Location,
            user.Occupation,
            Profiles.UnpackInterests(user.Interests));
    }
}
