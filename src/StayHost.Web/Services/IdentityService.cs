using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;

namespace StayHost.Web.Services;

/// <summary>
/// docs/01 TK-01 and TK-02 — signing up with a phone or an email, proving it
/// with a six-digit code, and attaching a Google, Apple or Facebook account.
/// </summary>
public class IdentityService(
    StayHostDbContext db, AuthService auth, IWebHostEnvironment env, ILogger<IdentityService> log)
{
    public sealed record CodeResult(bool Ok, string? Error = null, string? DevCode = null);

    /* ------------------------------------------------------------- TK-01 */

    /// <summary>
    /// Issues a six-digit code to whichever identifier is being proved. There is
    /// no SMS provider behind this build, so in development the code comes back
    /// in the response — the only way to complete the flow end to end. In any
    /// other environment it is never returned, only delivered.
    /// </summary>
    public async Task<CodeResult> SendCodeAsync(User user, IdentifierKind kind, CancellationToken ct)
    {
        var sentTo = kind == IdentifierKind.Phone ? user.Phone : user.Email;
        if (string.IsNullOrWhiteSpace(sentTo))
            return new(false, $"Tài khoản chưa có {Identity.KindLabel(kind)}.");

        var now = DateTime.UtcNow;

        // A resend button must not become a way to text somebody repeatedly.
        var last = await db.OneTimeCodes
            .Where(c => c.UserId == user.Id && c.Kind == kind)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (last is not null && Identity.ResendTooSoon(last.CreatedAt, now))
        {
            var wait = (int)(Identity.ResendCooldown - (now - last.CreatedAt)).TotalSeconds;
            return new(false, $"Vui lòng đợi {wait} giây rồi gửi lại.");
        }

        // Only the newest code is live; an old one lying around is a second key.
        var stale = await db.OneTimeCodes
            .Where(c => c.UserId == user.Id && c.Kind == kind && c.UsedAt == null)
            .ToListAsync(ct);
        db.OneTimeCodes.RemoveRange(stale);

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString($"D{Identity.CodeLength}");
        var (hash, salt) = PasswordHasher.Hash(code);

        db.OneTimeCodes.Add(new OneTimeCode
        {
            UserId = user.Id,
            SentTo = sentTo!,
            Kind = kind,
            CodeHash = hash,
            CodeSalt = salt,
            ExpiresAt = now + Identity.CodeLifetime
        });

        if (kind == IdentifierKind.Email)
        {
            db.EmailMessages.Add(new EmailMessage
            {
                ToEmail = sentTo!,
                ToName = user.FullName,
                Subject = $"Mã xác thực StayHost: {code}",
                Body = $"Mã xác thực của bạn là {code}. Mã có hiệu lực trong " +
                       $"{Identity.CodeLifetime.TotalMinutes:0} phút."
            });
        }
        else
        {
            // No SMS gateway in this build. The code is logged so the flow can be
            // followed; a deployment swaps this for a real provider.
            log.LogInformation("SMS to {Phone}: mã xác thực StayHost {Code}", sentTo, code);
        }

        await db.SaveChangesAsync(ct);

        return new(true, null, env.IsDevelopment() ? code : null);
    }

    /// <summary>
    /// Checks a code. Wrong ones count against the attempt limit; the right one
    /// marks the identifier confirmed and cannot be used twice.
    /// </summary>
    public async Task<AuthService.AuthResult> ConfirmCodeAsync(
        User user, IdentifierKind kind, string? code, CancellationToken ct)
    {
        var typed = (code ?? "").Trim();
        if (typed.Length != Identity.CodeLength || !typed.All(char.IsDigit))
            return new(false, $"Mã gồm {Identity.CodeLength} chữ số.");

        var record = await db.OneTimeCodes
            .Where(c => c.UserId == user.Id && c.Kind == kind && c.UsedAt == null)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (record is null) return new(false, "Chưa có mã nào được gửi. Bấm gửi mã trước.");

        var now = DateTime.UtcNow;
        if (Identity.CodeExpired(record.ExpiresAt, now))
            return new(false, "Mã đã hết hạn. Bấm gửi lại mã mới.");

        if (Identity.OutOfAttempts(record.Attempts))
            return new(false, "Đã nhập sai quá nhiều lần. Bấm gửi lại mã mới.");

        if (!PasswordHasher.Verify(typed, record.CodeHash, record.CodeSalt))
        {
            record.Attempts++;
            await db.SaveChangesAsync(ct);

            var left = Identity.MaxAttempts - record.Attempts;
            return new(false, left > 0
                ? $"Mã không đúng. Còn {left} lần thử."
                : "Mã không đúng. Bấm gửi lại mã mới.");
        }

        record.UsedAt = now;
        if (kind == IdentifierKind.Phone) user.PhoneConfirmed = true;
        else user.EmailConfirmed = true;

        await db.SaveChangesAsync(ct);
        return new(true, null, user);
    }

    /* ------------------------------------------------------------- TK-02 */

    /// <summary>
    /// docs/01 TK-02. A real deployment verifies the provider's ID token before
    /// this point; without client secrets the handshake is a stand-in, the same
    /// way payments are. What is real is everything after: how an identity is
    /// matched to an account, when it may attach to an existing one, and when a
    /// new account is created.
    /// </summary>
    public async Task<AuthService.AuthResult> SignInWithAsync(
        ExternalProvider provider, string? providerUserId, string? email, string? fullName,
        CancellationToken ct)
    {
        var subject = (providerUserId ?? "").Trim();
        if (subject.Length == 0) return new(false, "Thiếu định danh từ nhà cung cấp.");

        var address = (email ?? "").Trim().ToLowerInvariant();

        var existing = await db.ExternalLogins
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Provider == provider && x.ProviderUserId == subject, ct);

        if (existing?.User is not null)
        {
            existing.LastUsedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await auth.SignInAsync(existing.User, ct);
            return new(true, null, existing.User);
        }

        // Attaching to an account by email only works when this platform already
        // proved that address. Otherwise anyone who can make a provider account
        // claiming an address could walk into somebody else's account.
        User? user = null;
        if (address.Length > 0)
        {
            var match = await db.Users.FirstOrDefaultAsync(u => u.Email == address, ct);
            if (match is not null)
            {
                if (!match.EmailConfirmed)
                    return new(false,
                        $"Email {address} đã có tài khoản nhưng chưa xác thực. " +
                        "Hãy đăng nhập bằng mật khẩu và xác thực email trước khi liên kết " +
                        $"{Identity.ProviderLabel(provider)}.");

                user = match;
            }
        }

        user ??= await auth.CreateExternalUserAsync(
            address, fullName ?? Identity.ProviderLabel(provider) + " user", ct);

        db.ExternalLogins.Add(new ExternalLogin
        {
            UserId = user.Id,
            Provider = provider,
            ProviderUserId = subject,
            ProviderEmail = address.Length > 0 ? address : null,
            LastUsedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(ct);
        await auth.SignInAsync(user, ct);

        log.LogInformation("{Provider} sign-in for user {UserId}.", provider, user.Id);
        return new(true, null, user);
    }

    /// <summary>What is attached to this account, so somebody can see and unlink it.</summary>
    public Task<List<ExternalLogin>> LinkedAsync(int userId, CancellationToken ct) =>
        db.ExternalLogins.Where(x => x.UserId == userId).OrderBy(x => x.Provider).ToListAsync(ct);

    public async Task<string?> UnlinkAsync(User user, ExternalProvider provider, CancellationToken ct)
    {
        var link = await db.ExternalLogins
            .FirstOrDefaultAsync(x => x.UserId == user.Id && x.Provider == provider, ct);
        if (link is null) return null;

        // Removing the last way in would lock somebody out of their own account.
        var others = await db.ExternalLogins.CountAsync(x => x.UserId == user.Id && x.Id != link.Id, ct);
        var hasPassword = !string.IsNullOrWhiteSpace(user.PasswordHash);

        if (others == 0 && !hasPassword)
            return "Đặt mật khẩu trước khi bỏ liên kết, nếu không bạn sẽ không đăng nhập được nữa.";

        db.ExternalLogins.Remove(link);
        await db.SaveChangesAsync(ct);
        return null;
    }
}
