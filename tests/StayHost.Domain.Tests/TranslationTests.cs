namespace StayHost.Domain.Tests;

/// <summary>docs/01 TĐ-03, TN-06 — the translation gate and cache key.</summary>
public class TranslationTests
{
    [Fact]
    public void Off_until_a_provider_is_configured()
    {
        Assert.False(new TranslationSettings().IsConfigured);
        Assert.True(new TranslationSettings { Provider = "google" }.IsConfigured);
        Assert.False(new TranslationSettings { Provider = "  " }.IsConfigured);
    }

    [Fact]
    public void Supported_targets_are_a_closed_set()
    {
        Assert.True(Translations.IsSupported("vi"));
        Assert.True(Translations.IsSupported("en"));
        Assert.False(Translations.IsSupported("xx"));
        Assert.False(Translations.IsSupported(null));
    }

    [Fact]
    public void Cache_key_is_stable_and_separates_by_target()
    {
        var a = Translations.CacheKey("Nhà đẹp gần biển", "en");
        Assert.Equal(a, Translations.CacheKey("Nhà đẹp gần biển", "en"));
        Assert.NotEqual(a, Translations.CacheKey("Nhà đẹp gần biển", "ko"));
        Assert.NotEqual(a, Translations.CacheKey("Nhà khác", "en"));
    }

    [Fact]
    public void The_stub_is_obviously_not_a_real_translation()
    {
        Assert.Equal("[en] Xin chào", Translations.Stub("Xin chào", "en"));
    }
}
