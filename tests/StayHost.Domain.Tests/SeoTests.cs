using StayHost.Domain;

namespace StayHost.Domain.Tests;

public class SeoTests
{
    // The addresses that carry a secret. Indexing one of these does not expose a
    // page, it publishes somebody's private link — and once a crawler has it, it
    // is public for good. Nothing raises an error if this leaks, which is exactly
    // why it is pinned here.
    [Theory]
    [InlineData("/split/9f3ac2")]
    [InlineData("/wishlist/abc123")]
    [InlineData("/appeal?token=xyz")]
    [InlineData("/chuyen-khoan/SH-2026-0001")]
    public void An_address_that_is_itself_a_secret_is_never_crawlable(string path)
    {
        Assert.True(Seo.IsPrivate(path));
    }

    [Theory]
    [InlineData("/trips")]
    [InlineData("/trips/12")]
    [InlineData("/hosting")]
    [InlineData("/messages/4")]
    [InlineData("/wallet")]
    [InlineData("/admin")]
    [InlineData("/account/sanctions")]
    [InlineData("/users/7")]
    [InlineData("/api/listings")]
    public void Somebody_elses_account_is_never_crawlable(string path)
    {
        Assert.True(Seo.IsPrivate(path));
    }

    [Theory]
    [InlineData("/experiences/lam-gom-bat-trang/thanh-toan")]
    [InlineData("/services/massage-tai-nha/thanh-toan")]
    [InlineData("/experiences/bookings")]
    [InlineData("/services/bookings")]
    public void A_checkout_is_never_crawlable(string path)
    {
        // The wildcard rule has to survive any slug in the middle, which is the
        // whole reason it is a wildcard and not a prefix.
        Assert.True(Seo.IsPrivate(path));
    }

    // The pages this platform actually wants to rank for. A booking site lives on
    // its city pages and its rooms; blocking one of these by accident is not a
    // privacy bug but it is an expensive one, and just as quiet.
    [Theory]
    [InlineData("/")]
    [InlineData("/rooms/villa-da-lat-thong-reo")]
    [InlineData("/thanh-pho/da-lat")]
    [InlineData("/experiences")]
    [InlineData("/experiences/lam-gom-bat-trang")]
    [InlineData("/services")]
    [InlineData("/services/massage-tai-nha")]
    [InlineData("/help")]
    [InlineData("/help/huy-dat-phong")]
    [InlineData("/host")]
    public void The_pages_worth_ranking_stay_open(string path)
    {
        Assert.False(Seo.IsPrivate(path));
    }

    [Fact]
    public void A_narrower_allow_beats_a_broader_disallow()
    {
        // /shield is somebody's own claims; /shield/terms is the public promise
        // docs/06 §11 makes, and it has to be findable. Google resolves this by
        // taking the longest matching rule, so the test pins both halves.
        Assert.True(Seo.IsPrivate("/shield"));
        Assert.True(Seo.IsPrivate("/shield/4"));
        Assert.False(Seo.IsPrivate("/shield/terms"));
    }

    [Fact]
    public void Robots_names_the_sitemap_with_an_absolute_address()
    {
        var txt = Seo.RobotsTxt("https://staylio.vn/sitemap.xml");

        Assert.Contains("User-agent: *", txt);
        Assert.Contains("Sitemap: https://staylio.vn/sitemap.xml", txt);
        Assert.Contains("Disallow: /admin", txt);
        Assert.Contains("Allow: /shield/terms", txt);
    }

    [Fact]
    public void With_no_sitemap_address_the_directive_is_left_out_entirely()
    {
        // A relative Sitemap directive is ignored by every crawler, which looks
        // identical to having no sitemap — so saying nothing is the honest form.
        var txt = Seo.RobotsTxt(null);

        Assert.DoesNotContain("Sitemap:", txt);
        Assert.Contains("User-agent: *", txt);
    }

    [Fact]
    public void Every_disallow_rule_is_actually_private()
    {
        // Guards against a rule being added to the list in a shape IsPrivate
        // cannot match — the list would look right and enforce nothing.
        foreach (var rule in Seo.Disallow)
        {
            var sample = rule.Replace("*", "bat-ky-thu-gi");
            Assert.True(Seo.IsPrivate(sample), $"Luật {rule} không chặn được {sample}");
        }
    }
}
