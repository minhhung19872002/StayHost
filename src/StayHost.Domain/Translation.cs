using System.Security.Cryptography;
using System.Text;

namespace StayHost.Domain;

/// <summary>
/// docs/01 TĐ-03, TN-06 — machine translation of listing descriptions and chat
/// messages.
///
/// Off by default: with no provider configured the feature does not exist — the
/// "Dịch" button never shows, exactly the rule the social-login buttons follow, so
/// nothing offers an action that cannot run. `docs/07 §15.1`-style, the choice of
/// provider (Google / DeepL / Azure) and paying for the key is the customer's, not
/// a decision taken here. Filling <see cref="TranslationSettings.Provider"/> in
/// switches it on; the machinery below already handles the rest.
/// </summary>
public sealed record TranslationSettings
{
    /// <summary>"stub" for tests, "google", or "libretranslate"; null means off.</summary>
    public string? Provider { get; init; }

    /// <summary>Base URL of a self-hosted engine (LibreTranslate), e.g. http://libretranslate:5000.</summary>
    public string? Url { get; init; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Provider);

    public static TranslationSettings Current { get; set; } = new();
}

/// <summary>
/// docs/01 TĐ-03 — one cached translation, so a paid API is asked for a given text
/// and target language only once. Keyed by a hash of the source so an unbounded
/// description does not become an unbounded index key.
/// </summary>
public class TranslationCache
{
    public int Id { get; set; }
    public string SourceHash { get; set; } = "";
    public string TargetLang { get; set; } = "";
    public string TranslatedText { get; set; } = "";
    public string Provider { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>docs/01 TĐ-03, TN-06 — the pure rules around translation.</summary>
public static class Translations
{
    /// <summary>Target languages the UI may ask for. Codes match the spoken-language set.</summary>
    public static readonly IReadOnlyList<(string Code, string Label)> Targets =
    [
        ("vi", "Tiếng Việt"),
        ("en", "English"),
        ("zh", "中文"),
        ("ko", "한국어"),
        ("ja", "日本語"),
        ("fr", "Français")
    ];

    public static bool IsSupported(string? code) =>
        code is not null && Targets.Any(t => t.Code == code);

    /// <summary>Stable key for the cache: source text + target language, hashed.</summary>
    public static string CacheKey(string text, string targetLang)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{targetLang}\n{text}"));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// The deterministic stand-in used in tests and on a laptop with no key. It is
    /// obviously not a real translation — it tags the text with the target — so it
    /// can never be mistaken for one in a screenshot.
    /// </summary>
    public static string Stub(string text, string targetLang) => $"[{targetLang}] {text}";
}
