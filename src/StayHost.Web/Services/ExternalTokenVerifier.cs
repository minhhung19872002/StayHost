using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StayHost.Domain;

namespace StayHost.Web.Services;

/// <summary>
/// What Google, Apple or Facebook is prepared to swear to about somebody.
/// Everything in here came out of a token whose signature this class checked —
/// never straight off the wire.
/// </summary>
public sealed record ExternalIdentity(string Subject, string? Email, bool EmailVerified, string? FullName);

/// <summary>
/// docs/01 TK-02. The client ids are public — they travel to the browser anyway —
/// but the Facebook app secret is not, so it belongs in the environment file on
/// the server rather than in appsettings.json.
/// </summary>
public sealed class ExternalLoginSettings
{
    public string? GoogleClientId { get; set; }

    /// <summary>Apple calls it a Services ID; it plays the part of a client id.</summary>
    public string? AppleServicesId { get; set; }

    /// <summary>Must match a Return URL registered against the Services ID, exactly.</summary>
    public string? AppleRedirectUri { get; set; }

    public string? FacebookAppId { get; set; }
    public string? FacebookAppSecret { get; set; }

    public bool HasGoogle => !string.IsNullOrWhiteSpace(GoogleClientId);
    public bool HasApple => !string.IsNullOrWhiteSpace(AppleServicesId) && !string.IsNullOrWhiteSpace(AppleRedirectUri);
    public bool HasFacebook => !string.IsNullOrWhiteSpace(FacebookAppId) && !string.IsNullOrWhiteSpace(FacebookAppSecret);

    public bool Configured(ExternalProvider provider) => provider switch
    {
        ExternalProvider.Google => HasGoogle,
        ExternalProvider.Apple => HasApple,
        ExternalProvider.Facebook => HasFacebook,
        _ => false
    };
}

/// <summary>
/// Checks the thing the browser brings back from a provider. Google and Apple
/// both hand over a signed OpenID token, so they share one code path; Facebook
/// hands over an access token instead, which has to be taken back to Facebook to
/// find out who it belongs to.
/// </summary>
public class ExternalTokenVerifier(
    IHttpClientFactory factory, ExternalLoginSettings settings, ILogger<ExternalTokenVerifier> log)
{
    public sealed record Result(bool Ok, string? Error = null, ExternalIdentity? Identity = null);

    private const string GoogleKeys = "https://www.googleapis.com/oauth2/v3/certs";
    private const string AppleKeys = "https://appleid.apple.com/auth/keys";
    private const string AppleIssuer = "https://appleid.apple.com";
    private const string Graph = "https://graph.facebook.com/v21.0";

    // A provider rotates its signing keys a few times a year, so fetching the set
    // on every sign-in would be both slow and rude. A key id that is not in the
    // cache forces one refetch, and that is what makes a rotation heal itself.
    private static readonly SemaphoreSlim KeyLock = new(1, 1);
    private static readonly Dictionary<string, (DateTime Fetched, JsonDocument Set)> KeyCache = [];
    private static readonly TimeSpan KeyLifetime = TimeSpan.FromHours(6);

    public Task<Result> VerifyAsync(ExternalProvider provider, string? credential, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(credential))
            return Task.FromResult(new Result(false, "Thiếu mã xác thực từ nhà cung cấp."));

        if (!settings.Configured(provider))
            return Task.FromResult(new Result(false,
                $"Đăng nhập bằng {Identity.ProviderLabel(provider)} chưa được cấu hình trên máy chủ."));

        return provider switch
        {
            ExternalProvider.Google => VerifyOpenIdAsync(
                credential, GoogleKeys,
                // Google has issued tokens under both spellings for years.
                ["https://accounts.google.com", "accounts.google.com"],
                settings.GoogleClientId!, ct),

            ExternalProvider.Apple => VerifyOpenIdAsync(
                credential, AppleKeys, [AppleIssuer], settings.AppleServicesId!, ct),

            ExternalProvider.Facebook => VerifyFacebookAsync(credential, ct),

            _ => Task.FromResult(new Result(false, "Nhà cung cấp không hợp lệ."))
        };
    }

    /* --------------------------------------------------------------- OpenID */

    private async Task<Result> VerifyOpenIdAsync(
        string token, string keysUrl, string[] issuers, string audience, CancellationToken ct)
    {
        var parts = token.Split('.');
        if (parts.Length != 3) return new(false, "Mã xác thực không đúng định dạng.");

        JsonElement header, payload;
        try
        {
            header = JsonDocument.Parse(FromBase64Url(parts[0])).RootElement;
            payload = JsonDocument.Parse(FromBase64Url(parts[1])).RootElement;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Malformed identity token.");
            return new(false, "Mã xác thực không đọc được.");
        }

        if (Str(header, "alg") != "RS256") return new(false, "Thuật toán ký không được chấp nhận.");

        var kid = Str(header, "kid");
        if (kid is null) return new(false, "Mã xác thực thiếu định danh khoá.");

        // The signature is checked before a single claim is read, so a forged
        // token never gets as far as being interesting.
        var signed = Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]);
        var signature = FromBase64Url(parts[2]);

        var verified = await CheckSignatureAsync(keysUrl, kid, signed, signature, ct);
        if (!verified) return new(false, "Chữ ký của mã xác thực không hợp lệ.");

        var issuer = Str(payload, "iss");
        if (issuer is null || !issuers.Contains(issuer)) return new(false, "Mã xác thực không đúng nguồn phát hành.");

        if (!AudienceMatches(payload, audience))
            return new(false, "Mã xác thực được cấp cho ứng dụng khác.");

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (!payload.TryGetProperty("exp", out var exp) || exp.GetInt64() <= now)
            return new(false, "Mã xác thực đã hết hạn. Thử đăng nhập lại.");

        var subject = Str(payload, "sub");
        if (string.IsNullOrWhiteSpace(subject)) return new(false, "Mã xác thực thiếu định danh người dùng.");

        return new(true, null, new ExternalIdentity(
            subject!,
            Str(payload, "email"),
            // Apple sends this as a string, Google as a boolean. Both mean the same.
            payload.TryGetProperty("email_verified", out var ev)
                && (ev.ValueKind == JsonValueKind.True
                    || (ev.ValueKind == JsonValueKind.String && ev.GetString() == "true")),
            Str(payload, "name")));
    }

    /// <summary>An audience claim is a single string most days and a list on the others.</summary>
    private static bool AudienceMatches(JsonElement payload, string expected)
    {
        if (!payload.TryGetProperty("aud", out var aud)) return false;

        return aud.ValueKind switch
        {
            JsonValueKind.String => aud.GetString() == expected,
            JsonValueKind.Array => aud.EnumerateArray().Any(x => x.GetString() == expected),
            _ => false
        };
    }

    private async Task<bool> CheckSignatureAsync(
        string keysUrl, string kid, byte[] signed, byte[] signature, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            // A miss on the first pass means the provider has rotated its keys, so
            // the second pass refetches instead of turning people away.
            var set = await KeySetAsync(keysUrl, force: attempt == 1, ct);
            if (set is null) return false;

            foreach (var key in set.RootElement.GetProperty("keys").EnumerateArray())
            {
                if (Str(key, "kid") != kid || Str(key, "kty") != "RSA") continue;

                using var rsa = RSA.Create();
                rsa.ImportParameters(new RSAParameters
                {
                    Modulus = FromBase64Url(Str(key, "n")!),
                    Exponent = FromBase64Url(Str(key, "e")!)
                });

                return rsa.VerifyData(signed, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
        }

        log.LogWarning("No signing key {Kid} at {Url}.", kid, keysUrl);
        return false;
    }

    private async Task<JsonDocument?> KeySetAsync(string url, bool force, CancellationToken ct)
    {
        await KeyLock.WaitAsync(ct);
        try
        {
            if (!force
                && KeyCache.TryGetValue(url, out var cached)
                && DateTime.UtcNow - cached.Fetched < KeyLifetime)
                return cached.Set;

            var client = factory.CreateClient("external-login");
            client.Timeout = TimeSpan.FromSeconds(10);

            var json = await client.GetStringAsync(url, ct);
            var parsed = JsonDocument.Parse(json);

            KeyCache[url] = (DateTime.UtcNow, parsed);
            return parsed;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Could not fetch signing keys from {Url}.", url);

            // Better a stale key set than no sign-in at all while a provider is down.
            return KeyCache.TryGetValue(url, out var stale) ? stale.Set : null;
        }
        finally { KeyLock.Release(); }
    }

    /* ------------------------------------------------------------- Facebook */

    private async Task<Result> VerifyFacebookAsync(string accessToken, CancellationToken ct)
    {
        var client = factory.CreateClient("external-login");
        client.Timeout = TimeSpan.FromSeconds(10);

        try
        {
            // Which app was this token minted for? Without asking, a token from any
            // other Facebook app in the world would be accepted here.
            var appToken = Uri.EscapeDataString($"{settings.FacebookAppId}|{settings.FacebookAppSecret}");
            var debug = JsonDocument.Parse(await client.GetStringAsync(
                $"{Graph}/debug_token?input_token={Uri.EscapeDataString(accessToken)}&access_token={appToken}", ct));

            if (!debug.RootElement.TryGetProperty("data", out var data))
                return new(false, "Facebook không xác nhận được phiên đăng nhập.");

            if (!(data.TryGetProperty("is_valid", out var valid) && valid.ValueKind == JsonValueKind.True))
                return new(false, "Phiên đăng nhập Facebook không còn hiệu lực.");

            if (Str(data, "app_id") != settings.FacebookAppId)
                return new(false, "Phiên đăng nhập được cấp cho ứng dụng khác.");

            var me = JsonDocument.Parse(await client.GetStringAsync(
                $"{Graph}/me?fields=id,name,email&access_token={Uri.EscapeDataString(accessToken)}", ct));

            var id = Str(me.RootElement, "id");
            if (string.IsNullOrWhiteSpace(id)) return new(false, "Facebook không trả về định danh người dùng.");

            return new(true, null, new ExternalIdentity(
                id!,
                Str(me.RootElement, "email"),
                // Facebook only ever hands over an address it has confirmed itself.
                EmailVerified: Str(me.RootElement, "email") is not null,
                Str(me.RootElement, "name")));
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Facebook sign-in check failed.");
            return new(false, "Không liên lạc được với Facebook. Thử lại sau.");
        }
    }

    /* ---------------------------------------------------------------- bits */

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>Base64url has no padding and swaps two characters; the rest is ordinary base64.</summary>
    private static byte[] FromBase64Url(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }
}
