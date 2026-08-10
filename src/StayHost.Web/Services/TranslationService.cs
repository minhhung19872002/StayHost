using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;

namespace StayHost.Web.Services;

/// <summary>Turns text into another language. One implementation per provider.</summary>
public interface ITranslator
{
    string Name { get; }
    Task<string?> TranslateAsync(string text, string targetLang, CancellationToken ct);
}

/// <summary>
/// docs/01 TĐ-03 — the stand-in for tests and a laptop with no key. Deterministic
/// and visibly not a real translation, so the whole path (endpoint → cache → UI)
/// can be exercised without a paid API and without faking a real result.
/// </summary>
public sealed class StubTranslator : ITranslator
{
    public string Name => "stub";
    public Task<string?> TranslateAsync(string text, string targetLang, CancellationToken ct) =>
        Task.FromResult<string?>(Translations.Stub(text, targetLang));
}

/// <summary>
/// docs/01 TĐ-03 — Google Cloud Translation v2. The key never reaches the browser;
/// it is read from Translation__ApiKey in the environment.
/// </summary>
public sealed class GoogleTranslator(IHttpClientFactory http, string apiKey, ILogger<GoogleTranslator> log)
    : ITranslator
{
    public string Name => "google";

    public async Task<string?> TranslateAsync(string text, string targetLang, CancellationToken ct)
    {
        try
        {
            var client = http.CreateClient("translation");
            var url = $"https://translation.googleapis.com/language/translate/v2?key={apiKey}";
            var resp = await client.PostAsJsonAsync(url,
                new { q = text, target = targetLang, format = "text" }, ct);
            if (!resp.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            return doc.RootElement.GetProperty("data").GetProperty("translations")[0]
                .GetProperty("translatedText").GetString();
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Dịch thất bại.");
            return null;
        }
    }
}

/// <summary>
/// docs/01 TĐ-03, TN-06 — translate on demand, remembering each result so a paid
/// API is called once per text and language. With no translator registered the
/// service reports itself off and translates nothing.
/// </summary>
public class TranslationService(StayHostDbContext db, ITranslator? translator = null)
{
    public bool Enabled => translator is not null && TranslationSettings.Current.IsConfigured;

    public sealed record Result(bool Ok, string? Text = null, string? Error = null);

    public async Task<Result> TranslateAsync(string? text, string? targetLang, CancellationToken ct)
    {
        if (!Enabled) return new(false, Error: "Dịch tự động chưa được bật.");

        var source = (text ?? "").Trim();
        if (source.Length == 0) return new(false, Error: "Không có nội dung để dịch.");
        if (source.Length > 5000) source = source[..5000];

        var target = (targetLang ?? "vi").Trim().ToLowerInvariant();
        if (!Translations.IsSupported(target)) return new(false, Error: "Ngôn ngữ đích không được hỗ trợ.");

        var key = Translations.CacheKey(source, target);
        var cached = await db.TranslationCaches
            .Where(c => c.SourceHash == key && c.TargetLang == target)
            .Select(c => c.TranslatedText)
            .FirstOrDefaultAsync(ct);
        if (cached is not null) return new(true, cached);

        var translated = await translator!.TranslateAsync(source, target, ct);
        if (translated is null) return new(false, Error: "Không dịch được lúc này, vui lòng thử lại sau.");

        db.TranslationCaches.Add(new TranslationCache
        {
            SourceHash = key, TargetLang = target, TranslatedText = translated, Provider = translator.Name
        });
        await db.SaveChangesAsync(ct);

        return new(true, translated);
    }
}
