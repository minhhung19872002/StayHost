using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>
/// docs/07 §6 — display conversion. The rules are few on purpose: everything
/// that touches real money stays in Pricing and the ledger, and these only
/// decide what an operator may type and when a rate is called stale.
/// </summary>
public class FxTests
{
    [Fact]
    public void A_rate_must_be_positive()
    {
        Assert.False(Fx.IsValidRate("USD", 0m));
        Assert.False(Fx.IsValidRate("USD", -0.00004m));
        Assert.True(Fx.IsValidRate("USD", 0.0000392m));
    }

    /// <summary>
    /// The base currency is always exactly 1. A VND row at 0.9 would rescale
    /// every price on the site in one keystroke, and nothing downstream would
    /// error — prices would simply all be wrong together.
    /// </summary>
    [Fact]
    public void The_base_currency_is_pinned_to_one()
    {
        Assert.True(Fx.IsValidRate("VND", 1m));
        Assert.False(Fx.IsValidRate("VND", 0.9m));
        Assert.False(Fx.IsValidRate("vnd", 23000m));
        Assert.True(Fx.IsValidRate(" VND ", 1m));
    }

    /// <summary>docs/07 §6 — "ít nhất mỗi 6 giờ" is where stale begins.</summary>
    [Fact]
    public void Six_hours_is_the_edge_of_stale()
    {
        var now = new DateTime(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);
        Assert.False(Fx.Stale(now.AddHours(-6), now));
        Assert.True(Fx.Stale(now.AddHours(-6).AddSeconds(-1), now));
        Assert.False(Fx.Stale(now, now));
    }

    /// <summary>
    /// Feed must be the enum's zero so it is the database default: a refresh job
    /// updates only Feed rows, and rows born Manual would make it a silent no-op.
    /// </summary>
    [Fact]
    public void New_rows_default_to_the_feed()
    {
        Assert.Equal(0, (int)ExchangeRateSource.Feed);
        Assert.Equal(ExchangeRateSource.Feed, new ExchangeRate().Source);
        Assert.True(new ExchangeRate().IsActive);
    }
}
