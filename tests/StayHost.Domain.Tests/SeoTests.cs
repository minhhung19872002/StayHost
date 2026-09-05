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

    // --- paging a city page -------------------------------------------------

    [Theory]
    [InlineData(0, 1)]    // an empty city still has a page 1
    [InlineData(1, 1)]
    [InlineData(12, 1)]   // exactly full — the off-by-one that invents an empty page 2
    [InlineData(13, 2)]
    [InlineData(24, 2)]
    [InlineData(25, 3)]
    public void The_last_page_is_not_an_empty_one(int total, int expected)
    {
        Assert.Equal(expected, Seo.TotalPages(total, 12));
    }

    [Theory]
    [InlineData(-5, 1)]
    [InlineData(0, 1)]
    [InlineData(2, 2)]
    [InlineData(99, 3)]   // a crawler following a stale link gets the last page,
    public void A_page_outside_the_range_lands_on_one_that_exists(int asked, int expected)
    {
        // ...not an empty one. Thin pages teach a search engine the site is thin.
        Assert.Equal(expected, Seo.ClampPage(asked, 25, 12));
    }

    [Fact]
    public void Skipping_lines_the_pages_up_without_gaps_or_repeats()
    {
        Assert.Equal(0, Seo.Skip(1, 25, 12));
        Assert.Equal(12, Seo.Skip(2, 25, 12));
        Assert.Equal(24, Seo.Skip(3, 25, 12));
    }

    [Fact]
    public void Page_one_is_the_bare_address_not_trang_equals_one()
    {
        // Two addresses for the same page is the duplicate the canonical rules
        // exist to prevent, so the series must not start with a parameter.
        Assert.Equal("/thanh-pho/da-lat", Seo.CityPagePath("da-lat", 1));
        Assert.Equal("/thanh-pho/da-lat", Seo.CityPagePath("da-lat", 0));
        Assert.Equal("/thanh-pho/da-lat?trang=2", Seo.CityPagePath("da-lat", 2));
    }

    [Fact]
    public void A_paged_city_address_stays_crawlable()
    {
        // The paging strip is the only path to the places past page one; a rule
        // that quietly blocked "?trang=" would undo the whole thing.
        Assert.False(Seo.IsPrivate(Seo.CityPagePath("da-lat", 2)));
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

    // ------------------------------------------------------- which addresses exist

    [Fact]
    public void An_address_no_route_answers_is_not_a_page()
    {
        // The soft 404. Until SpaRoutes existed, every one of these came back 200
        // carrying the home page title and an empty body, and Google filed them.
        Assert.Equal(PageKind.Unknown, SpaRoutes.Resolve("/khong-co-gi-o-day").Kind);
        Assert.Equal(PageKind.Unknown, SpaRoutes.Resolve("/rooms").Kind);
        Assert.Equal(PageKind.Unknown, SpaRoutes.Resolve("/a/b/c/d").Kind);
        Assert.Equal(PageKind.Unknown, SpaRoutes.Resolve("").Kind);
    }

    [Fact]
    public void A_content_address_carries_its_slug()
    {
        Assert.Equal(new PageRoute(PageKind.Listing, "bai-dai-pool-villa-34"),
                     SpaRoutes.Resolve("/rooms/bai-dai-pool-villa-34"));
        Assert.Equal(new PageRoute(PageKind.City, "da-lat"),
                     SpaRoutes.Resolve("/thanh-pho/da-lat"));
        Assert.Equal(new PageRoute(PageKind.HelpArticle, "huy-dat-phong"),
                     SpaRoutes.Resolve("/help/huy-dat-phong"));

        // A trailing slash is the same page, not a second one.
        Assert.Equal(SpaRoutes.Resolve("/rooms/x"), SpaRoutes.Resolve("/rooms/x/"));
    }

    [Fact]
    public void A_fixed_page_sitting_where_a_slug_would_wins()
    {
        // "/experiences/bookings" is a real screen. Resolved as a slug it would
        // send the server looking for an experience called "bookings", find none,
        // and answer 404 on a page that works.
        Assert.Equal(PageKind.App, SpaRoutes.Resolve("/experiences/bookings").Kind);
        Assert.Equal(PageKind.App, SpaRoutes.Resolve("/services/bookings").Kind);
        Assert.Equal(PageKind.Experience, SpaRoutes.Resolve("/experiences/lan-bien-nha-trang").Kind);
    }

    [Fact]
    public void A_checkout_address_is_answered_without_a_lookup()
    {
        // Behind a session and noindex anyway; a 404 here would land on a screen
        // that cannot show one.
        Assert.Equal(PageKind.App, SpaRoutes.Resolve("/experiences/x/thanh-toan").Kind);
        Assert.Equal(PageKind.App, SpaRoutes.Resolve("/services/x/thanh-toan").Kind);
        Assert.Equal(PageKind.App, SpaRoutes.Resolve("/thanh-toan/ket-qua").Kind);
        Assert.Equal(PageKind.App, SpaRoutes.Resolve("/split/abc123").Kind);
        Assert.Equal(PageKind.App, SpaRoutes.Resolve("/users/12").Kind);
    }

    [Fact]
    public void Only_content_addresses_ask_the_database()
    {
        Assert.True(SpaRoutes.Resolve("/rooms/x").NeedsLookup);
        Assert.True(SpaRoutes.Resolve("/thanh-pho/hue").NeedsLookup);
        Assert.False(SpaRoutes.Resolve("/trips").NeedsLookup);
        Assert.False(SpaRoutes.Resolve("/khong-co-gi").NeedsLookup);
    }

    [Fact]
    public void Every_fixed_route_resolves_to_itself()
    {
        // Catches a route added to the list in a shape Resolve cannot match, which
        // would answer 404 on a screen that works.
        foreach (var route in SpaRoutes.Fixed)
            Assert.Equal(PageKind.App, SpaRoutes.Resolve(route).Kind);
    }

    [Fact]
    public void A_missing_file_is_not_answered_with_the_app_shell()
    {
        // A <script> tag that receives HTML fails as a syntax error inside the
        // app, which reads nothing like "that bundle name is stale".
        Assert.True(SpaRoutes.LooksLikeAsset("/assets/index-ABC123.js"));
        Assert.True(SpaRoutes.LooksLikeAsset("/uploads/1-abc.png"));
        Assert.True(SpaRoutes.LooksLikeAsset("/favicon.svg"));

        // A slug is not a file, even when it carries a dot.
        Assert.False(SpaRoutes.LooksLikeAsset("/rooms/villa-2.5-sao"));
        Assert.False(SpaRoutes.LooksLikeAsset("/thanh-pho/da-lat"));
        Assert.False(SpaRoutes.LooksLikeAsset("/"));
    }

    /* ---------------------------------------------------- docs/02 F1 — cài đặt */

    /// <summary>
    /// The hub and all nine groups are fixed routes — literal entries, not a
    /// fallback arm. The distinction is what an invented tenth group gets: a
    /// fallback would answer it 200 with an empty shell, the exact soft 404
    /// MapFallbackToFile used to leave.
    /// </summary>
    [Theory]
    [InlineData("/cai-dat")]
    [InlineData("/cai-dat/ho-so")]
    [InlineData("/cai-dat/bao-mat")]
    [InlineData("/cai-dat/thanh-toan")]
    [InlineData("/cai-dat/nhan-tien")]
    [InlineData("/cai-dat/thong-bao")]
    [InlineData("/cai-dat/quyen-rieng-tu")]
    [InlineData("/cai-dat/tuy-chinh")]
    [InlineData("/cai-dat/cong-tac")]
    [InlineData("/cai-dat/gioi-thieu")]
    public void Every_settings_group_is_a_real_page(string path)
    {
        Assert.Equal(PageKind.App, SpaRoutes.Resolve(path).Kind);
    }

    [Fact]
    public void An_invented_settings_group_is_an_honest_404()
    {
        Assert.Equal(PageKind.Unknown, SpaRoutes.Resolve("/cai-dat/khong-co-that").Kind);
    }

    /// <summary>Somebody's own preferences and devices. Never in the sitemap.</summary>
    [Theory]
    [InlineData("/cai-dat")]
    [InlineData("/cai-dat/bao-mat")]
    public void Settings_pages_are_private(string path)
    {
        Assert.True(Seo.IsPrivate(path));
    }
}
