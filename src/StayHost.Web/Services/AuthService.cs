using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Infrastructure;

namespace StayHost.Web.Services;

/// <summary>
/// Password auth with PBKDF2 + per-user salt, and opaque session tokens kept in an
/// HttpOnly cookie. No JWT: revoking a session is a single row update.
/// </summary>
public class AuthService(StayHostDbContext db, IHttpContextAccessor accessor)
{
    public const string CookieName = "sh_auth";

    public sealed record AuthResult(bool Ok, string? Error = null, User? User = null);

    private HttpContext Ctx => accessor.HttpContext
        ?? throw new InvalidOperationException("No active HTTP context.");

    /* ------------------------------------------------------------ register */

    public async Task<AuthResult> RegisterAsync(string email, string password, string fullName, string? phone, CancellationToken ct)
    {
        email = email.Trim().ToLowerInvariant();

        if (!email.Contains('@') || email.Length < 5) return new(false, "Email không hợp lệ.");
        if (password.Length < 8) return new(false, "Mật khẩu cần tối thiểu 8 ký tự.");
        if (string.IsNullOrWhiteSpace(fullName)) return new(false, "Vui lòng nhập họ tên.");
        if (await db.Users.AnyAsync(u => u.Email == email, ct)) return new(false, "Email này đã được đăng ký.");

        var (hash, salt) = PasswordHasher.Hash(password);
        var user = new User
        {
            Email = email,
            FullName = fullName.Trim(),
            Initials = MakeInitials(fullName),
            Phone = phone?.Trim(),
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = UserRole.Guest,
            AdoptedSessionId = Ctx.SessionId()
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        await AdoptAnonymousDataAsync(user, ct);
        await IssueSessionAsync(user, ct);
        return new(true, null, user);
    }

    /* --------------------------------------------------------------- login */

    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct)
    {
        email = email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        // Same message either way so the endpoint does not leak which emails exist.
        if (user is null || !PasswordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
            return new(false, "Email hoặc mật khẩu không đúng.");

        await AdoptAnonymousDataAsync(user, ct);
        await IssueSessionAsync(user, ct);
        return new(true, null, user);
    }

    public async Task LogoutAsync(CancellationToken ct)
    {
        var token = Ctx.Request.Cookies[CookieName];
        if (!string.IsNullOrEmpty(token))
        {
            var session = await db.AuthSessions.FirstOrDefaultAsync(s => s.Token == token, ct);
            if (session is not null)
            {
                session.RevokedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }
        Ctx.Response.Cookies.Delete(CookieName);
    }

    /* ------------------------------------------------------------ sessions */

    private async Task IssueSessionAsync(User user, CancellationToken ct)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        db.AuthSessions.Add(new AuthSession
        {
            Token = token,
            UserId = user.Id,
            UserAgent = Truncate(Ctx.Request.Headers.UserAgent.ToString(), 300),
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        });
        await db.SaveChangesAsync(ct);

        Ctx.Response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = Ctx.Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });
    }

    /// <summary>Resolves the signed-in user for this request, or null when anonymous.</summary>
    public async Task<User?> CurrentUserAsync(CancellationToken ct = default)
    {
        if (Ctx.Items.TryGetValue("__sh_user", out var cached)) return cached as User;

        User? user = null;
        var token = Ctx.Request.Cookies[CookieName];
        if (!string.IsNullOrEmpty(token))
        {
            var session = await db.AuthSessions
                .Include(s => s.User!).ThenInclude(u => u.HostProfile)
                .FirstOrDefaultAsync(s => s.Token == token, ct);

            if (session is not null && session.RevokedAt is null && session.ExpiresAt > DateTime.UtcNow)
                user = session.User;
        }

        Ctx.Items["__sh_user"] = user;
        return user;
    }

    /* ------------------------------------------------------------ adoption */

    /// <summary>
    /// Wishlist items and bookings made before signing in belong to a cookie, not an
    /// account. On login we move them across so nothing appears lost.
    /// </summary>
    private async Task AdoptAnonymousDataAsync(User user, CancellationToken ct)
    {
        var sid = Ctx.SessionId();
        if (string.IsNullOrEmpty(sid)) return;

        var alreadyOwned = await db.Favorites
            .Where(f => f.UserId == user.Id)
            .Select(f => f.ListingId)
            .ToListAsync(ct);

        var orphanFavorites = await db.Favorites
            .Where(f => f.SessionId == sid && f.UserId == null)
            .ToListAsync(ct);

        foreach (var fav in orphanFavorites)
        {
            if (alreadyOwned.Contains(fav.ListingId)) db.Favorites.Remove(fav);
            else fav.UserId = user.Id;
        }

        var orphanBookings = await db.Bookings
            .Where(b => b.SessionId == sid && b.GuestUserId == null)
            .ToListAsync(ct);
        foreach (var booking in orphanBookings) booking.GuestUserId = user.Id;

        await db.SaveChangesAsync(ct);
    }

    /* ------------------------------------------------- password management */

    public async Task<AuthResult> ChangePasswordAsync(User user, string current, string next, CancellationToken ct)
    {
        if (!PasswordHasher.Verify(current, user.PasswordHash, user.PasswordSalt))
            return new(false, "Mật khẩu hiện tại không đúng.");
        if (next.Length < 8)
            return new(false, "Mật khẩu mới cần tối thiểu 8 ký tự.");

        var (hash, salt) = PasswordHasher.Hash(next);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;

        // Changing the password logs every other device out.
        var currentToken = Ctx.Request.Cookies[CookieName];
        var others = await db.AuthSessions
            .Where(s => s.UserId == user.Id && s.RevokedAt == null && s.Token != currentToken)
            .ToListAsync(ct);
        foreach (var s in others) s.RevokedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return new(true, null, user);
    }

    /// <summary>
    /// Issues a reset token. Returns null when the email is unknown — the caller still
    /// reports success so the endpoint cannot be used to enumerate accounts.
    /// </summary>
    public async Task<string?> BeginPasswordResetAsync(string email, CancellationToken ct)
    {
        email = email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null) return null;

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        db.UserTokens.Add(new UserToken
        {
            Token = token,
            UserId = user.Id,
            Purpose = TokenPurpose.PasswordReset,
            ExpiresAt = DateTime.UtcNow.AddHours(2)
        });
        await db.SaveChangesAsync(ct);
        return token;
    }

    public async Task<AuthResult> CompletePasswordResetAsync(string token, string newPassword, CancellationToken ct)
    {
        if (newPassword.Length < 8) return new(false, "Mật khẩu mới cần tối thiểu 8 ký tự.");

        var entry = await db.UserTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token && t.Purpose == TokenPurpose.PasswordReset, ct);

        if (entry is null || entry.UsedAt is not null || entry.ExpiresAt < DateTime.UtcNow)
            return new(false, "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.");

        var (hash, salt) = PasswordHasher.Hash(newPassword);
        entry.User!.PasswordHash = hash;
        entry.User.PasswordSalt = salt;
        entry.UsedAt = DateTime.UtcNow;

        // A reset invalidates every existing session.
        var sessions = await db.AuthSessions
            .Where(s => s.UserId == entry.UserId && s.RevokedAt == null).ToListAsync(ct);
        foreach (var s in sessions) s.RevokedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        await IssueSessionAsync(entry.User, ct);
        return new(true, null, entry.User);
    }

    /* --------------------------------------------------------- verification */

    public async Task<string> BeginEmailVerificationAsync(User user, CancellationToken ct)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        db.UserTokens.Add(new UserToken
        {
            Token = token,
            UserId = user.Id,
            Purpose = TokenPurpose.EmailVerification,
            ExpiresAt = DateTime.UtcNow.AddDays(3)
        });
        await db.SaveChangesAsync(ct);
        return token;
    }

    public async Task<AuthResult> ConfirmEmailAsync(string token, CancellationToken ct)
    {
        var entry = await db.UserTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token && t.Purpose == TokenPurpose.EmailVerification, ct);

        if (entry is null || entry.UsedAt is not null || entry.ExpiresAt < DateTime.UtcNow)
            return new(false, "Liên kết xác minh không hợp lệ hoặc đã hết hạn.");

        entry.User!.EmailConfirmed = true;
        entry.UsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new(true, null, entry.User);
    }

    /* ------------------------------------------------------------- devices */

    public Task<List<AuthSession>> ActiveSessionsAsync(int userId, CancellationToken ct) =>
        db.AuthSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

    public string? CurrentToken() => Ctx.Request.Cookies[CookieName];

    public async Task<bool> RevokeSessionAsync(int userId, int sessionId, CancellationToken ct)
    {
        var session = await db.AuthSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, ct);
        if (session is null) return false;

        session.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    /* ---------------------------------------------------------- host setup */

    /// <summary>Creates the host profile the first time a user publishes a listing.</summary>
    public async Task<HostProfile> EnsureHostProfileAsync(User user, CancellationToken ct)
    {
        var existing = await db.Hosts.FirstOrDefaultAsync(h => h.UserId == user.Id, ct);
        if (existing is not null) return existing;

        var host = new HostProfile
        {
            Name = user.FullName,
            Initials = user.Initials,
            Bio = user.Bio,
            IsSuperhost = false,
            YearsHosting = 0,
            JoinedAt = user.CreatedAt,
            UserId = user.Id
        };
        db.Hosts.Add(host);

        if (user.Role == UserRole.Guest) user.Role = UserRole.Host;
        await db.SaveChangesAsync(ct);
        return host;
    }

    public static string MakeInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "??";
        var letters = parts.Select(p => p[0]).ToArray();
        var take = Math.Min(2, letters.Length);
        return new string(letters[^take..]).ToUpperInvariant();
    }

    private static string? Truncate(string? s, int max) =>
        string.IsNullOrEmpty(s) ? s : s.Length <= max ? s : s[..max];
}
