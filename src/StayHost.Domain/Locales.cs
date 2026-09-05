namespace StayHost.Domain;

/// <summary>
/// docs/01 TK-09 (P0) — "Cài đặt ngôn ngữ, tiền tệ, múi giờ hiển thị", the
/// account-side half. Until 05/09/2026 the choice lived only in one browser's
/// localStorage: it evaporated on every new device and private window, and the
/// server — which writes the emails — never knew it existed. The client-side
/// comment in store.js said it against itself: "Nothing on the server knows it:
/// the choice lives in this browser."
///
/// Validation, once, here. The language list is Translations.Targets — the one
/// list the translator, the UI picker and this all share, because a second list
/// is how the "Dịch" button once grew stale labels. Currencies are validated
/// against exchange_rates by the caller, since that set now lives in the
/// database. Timezones are whatever the runtime's ICU recognises.
/// </summary>
public static class Locales
{
    /// <summary>A supported UI language code, or null — never an invented one.</summary>
    public static string? Language(string? code)
    {
        var c = (code ?? "").Trim().ToLowerInvariant();
        return Translations.IsSupported(c) ? c : null;
    }

    /// <summary>
    /// A timezone id the runtime can actually resolve, or null. Same guard the
    /// experience clock uses: an unknown id must cost a preference, not a 500.
    /// </summary>
    public static string? TimeZone(string? id)
    {
        var z = (id ?? "").Trim();
        if (z.Length == 0 || z.Length > 64) return null;
        try { TimeZoneInfo.FindSystemTimeZoneById(z); return z; }
        catch { return null; }
    }
}
