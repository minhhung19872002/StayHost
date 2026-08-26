using StayHost.Domain;

namespace StayHost.Domain.Tests;

public class PresenceTests
{
    private static readonly DateTime Now = new(2026, 8, 26, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Somebody_quiet_for_longer_than_the_window_is_gone()
    {
        Assert.True(Presence.StillHere(Now.AddMinutes(-4), Now));
        Assert.False(Presence.StillHere(Now.AddMinutes(-6), Now));

        // Still remembered, so a returning reader keeps their identity.
        Assert.False(Presence.Stale(Now.AddMinutes(-6), Now));
        Assert.True(Presence.Stale(Now.AddMinutes(-31), Now));
    }

    [Fact]
    public void A_payment_gateway_calling_in_is_not_a_visitor()
    {
        // The one that would really mislead: VNPay, MoMo and ZaloPay post an IPN
        // for every settled order, so counting these would make the site look
        // busiest at the moment it is quietest.
        Assert.True(Presence.IsMachineAddress("/api/payments/vnpay/ipn"));
        Assert.True(Presence.IsMachineAddress("/api/payments/momo/ipn"));
        Assert.True(Presence.IsMachineAddress("/health"));
        Assert.True(Presence.IsMachineAddress("/robots.txt"));
        Assert.True(Presence.IsMachineAddress("/sitemap.xml"));

        // A guest coming back from the gateway's own page IS a visitor.
        Assert.False(Presence.IsMachineAddress("/api/payments/vnpay/return"));
        Assert.False(Presence.IsMachineAddress("/rooms/bai-dai-pool-villa-34"));
    }

    [Fact]
    public void Crawlers_and_link_scrapers_do_not_count()
    {
        Assert.True(Presence.LooksLikeRobot("Mozilla/5.0 (compatible; Googlebot/2.1)"));
        Assert.True(Presence.LooksLikeRobot("facebookexternalhit/1.1"));
        Assert.True(Presence.LooksLikeRobot("curl/8.4.0"));
        Assert.True(Presence.LooksLikeRobot("python-requests/2.31"));

        // No user agent at all is not a browser.
        Assert.True(Presence.LooksLikeRobot(""));
        Assert.True(Presence.LooksLikeRobot(null));

        Assert.False(Presence.LooksLikeRobot(
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 Safari/604.1"));
    }

    [Fact]
    public void A_request_that_did_not_bring_the_cookie_back_is_not_counted()
    {
        const string phone = "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) Safari/604.1";

        // This is what keeps one cookie-less crawler from reading as a fleet:
        // every request of its own gets a brand-new identity.
        Assert.False(Presence.CountsAsVisit("/", phone, broughtCookie: false));
        Assert.True(Presence.CountsAsVisit("/", phone, broughtCookie: true));
    }

    [Fact]
    public void The_count_splits_guests_from_signed_in_people()
    {
        Presence.Visitor[] visitors =
        [
            new(Now.AddMinutes(-1), 7),      // signed in, here
            new(Now.AddMinutes(-2), null),   // guest, here
            new(Now.AddSeconds(-10), null),  // guest, here
            new(Now.AddMinutes(-9), 12),     // signed in, but left
        ];

        var tally = Presence.Count(visitors, Now);

        Assert.Equal(3, tally.Total);
        Assert.Equal(1, tally.SignedIn);
        Assert.Equal(2, tally.Guests);
    }

    [Fact]
    public void An_empty_site_counts_zero_rather_than_throwing()
    {
        var tally = Presence.Count([], Now);
        Assert.Equal(0, tally.Total);
        Assert.Equal(0, tally.SignedIn);
        Assert.Equal(0, tally.Guests);
    }
}
