using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>
/// docs/01 TK-09 — what the account will accept as a display preference. The
/// language list is Translations.Targets, the one list the translator and the
/// picker already share; a second list here is how the "Dịch" button once grew
/// stale labels.
/// </summary>
public class LocalesTests
{
    [Fact]
    public void Every_interface_language_is_accepted_and_nothing_else()
    {
        foreach (var (code, _) in Translations.Targets)
            Assert.Equal(code, Locales.Language(code));

        Assert.Equal("vi", Locales.Language(" VI "));
        Assert.Null(Locales.Language("xx"));
        Assert.Null(Locales.Language("vi-VN"));
        Assert.Null(Locales.Language(""));
        Assert.Null(Locales.Language(null));
    }

    /// <summary>
    /// An unknown timezone costs the preference, never a 500 — the same guard
    /// the experience clock uses for the same reason.
    /// </summary>
    [Fact]
    public void A_timezone_is_whatever_the_runtime_can_resolve()
    {
        Assert.Equal("Asia/Ho_Chi_Minh", Locales.TimeZone("Asia/Ho_Chi_Minh"));
        Assert.Equal("Asia/Tokyo", Locales.TimeZone(" Asia/Tokyo "));
        Assert.Null(Locales.TimeZone("Asia/Khong_Co_That"));
        Assert.Null(Locales.TimeZone(""));
        Assert.Null(Locales.TimeZone(null));
        Assert.Null(Locales.TimeZone(new string('x', 65)));
    }

    /// <summary>Null means "never chosen" and the account starts there.</summary>
    [Fact]
    public void An_account_starts_with_no_preference()
    {
        var user = new User();
        Assert.Null(user.Language);
        Assert.Null(user.Currency);
        Assert.Null(user.TimeZoneId);
    }
}
