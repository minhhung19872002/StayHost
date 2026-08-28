using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>
/// docs/01 TĐ-11 and TĐ-21 — the language a review reads as, and what a pile of
/// them keeps saying.
/// </summary>
public class ReviewInsightsTests
{
    /* ------------------------------------------------------------- TĐ-11 */

    [Fact]
    public void Vietnamese_is_told_by_its_marks()
    {
        Assert.Equal("vi", ReviewInsights.GuessLanguage("Chỗ ở sạch sẽ, chủ nhà rất nhiệt tình."));
        Assert.Equal("vi", ReviewInsights.GuessLanguage("Phòng đẹp"));
    }

    [Fact]
    public void Unmarked_latin_is_english_rather_than_a_guess_between_latin_languages()
    {
        // "en" here means "some Latin language"; calling it French on a hunch
        // would sort it wrongly with confidence, which is worse than grouping.
        Assert.Equal("en", ReviewInsights.GuessLanguage("Great place, very clean and quiet."));
        Assert.Equal("en", ReviewInsights.GuessLanguage("Sehr schone Wohnung"));
    }

    [Fact]
    public void Japanese_is_told_from_chinese_by_kana()
    {
        // Japanese prose carries han characters too, so kana has to be checked
        // first or every Japanese review is filed as Chinese.
        Assert.Equal("ja", ReviewInsights.GuessLanguage("とても清潔な部屋でした。"));
        Assert.Equal("ja", ReviewInsights.GuessLanguage("駅から近くて便利です。とても良い。"));
        Assert.Equal("zh", ReviewInsights.GuessLanguage("房间非常干净，位置很好。"));
        Assert.Equal("ko", ReviewInsights.GuessLanguage("숙소가 깨끗하고 조용했습니다."));
    }

    [Fact]
    public void Empty_text_does_not_throw()
    {
        Assert.Equal("en", ReviewInsights.GuessLanguage(null));
        Assert.Equal("en", ReviewInsights.GuessLanguage("   "));
    }

    [Fact]
    public void A_stored_language_beats_the_guess()
    {
        // The writer's own interface language is a better answer than anything
        // read out of the characters — a Vietnamese speaker may write in English.
        Assert.Equal("vi", ReviewInsights.LanguageOf("VI", "Great place, very clean."));
        Assert.Equal("en", ReviewInsights.LanguageOf(null, "Great place, very clean."));
        Assert.Equal("vi", ReviewInsights.LanguageOf("  ", "Phòng rất sạch sẽ."));
    }

    /* ------------------------------------------------------------- TĐ-21 */

    private static (string, double)[] Many(string text, double rating, int count) =>
        Enumerable.Range(0, count).Select(_ => (text, rating)).ToArray();

    [Fact]
    public void A_subject_only_becomes_a_theme_once_enough_people_raise_it()
    {
        var two = Many("Vị trí rất thuận tiện, gần trung tâm.", 5, 2);
        Assert.Empty(ReviewInsights.Themes(two));

        var three = Many("Vị trí rất thuận tiện, gần trung tâm.", 5, ReviewInsights.MinMentions);
        var themes = ReviewInsights.Themes(three);
        Assert.Contains(themes, t => t.Key == "location");
        Assert.Equal(ReviewInsights.MinMentions, themes.Single(t => t.Key == "location").Mentions);
    }

    [Fact]
    public void The_score_on_a_row_is_what_the_people_who_raised_it_gave()
    {
        // The whole point of the row: not the listing's overall score, but how
        // the people who cared about this subject rated the stay.
        var reviews = new List<(string, double)>();
        reviews.AddRange(Many("Phòng rất sạch sẽ và thơm.", 3, 3));
        reviews.AddRange(Many("Chủ nhà nhiệt tình, hỗ trợ nhanh.", 5, 4));

        var themes = ReviewInsights.Themes(reviews);

        Assert.Equal(3, themes.Single(t => t.Key == "clean").Rating);
        Assert.Equal(5, themes.Single(t => t.Key == "host").Rating);
    }

    [Fact]
    public void Diacritics_do_not_matter_because_the_text_is_normalised_first()
    {
        // Same road the search box takes: "sach se" and "sạch sẽ" are one word.
        var themes = ReviewInsights.Themes(Many("Phong rat sach va thoang", 4.5, 4));
        Assert.Contains(themes, t => t.Key == "clean");
    }

    [Fact]
    public void English_reviews_reach_the_same_themes()
    {
        var themes = ReviewInsights.Themes(Many("Very clean and the location is perfect.", 5, 4));
        Assert.Contains(themes, t => t.Key == "clean");
        Assert.Contains(themes, t => t.Key == "location");
    }

    [Fact]
    public void Themes_come_back_strongest_first_and_capped()
    {
        var reviews = new List<(string, double)>();
        reviews.AddRange(Many("Vị trí đẹp, gần biển.", 5, 9));
        reviews.AddRange(Many("Rất sạch sẽ.", 5, 4));

        var themes = ReviewInsights.Themes(reviews, take: 1);

        Assert.Single(themes);
        Assert.Equal("location", themes[0].Key);
    }

    [Fact]
    public void Too_few_reviews_produce_nothing_at_all()
    {
        Assert.Empty(ReviewInsights.Themes([]));
        Assert.Empty(ReviewInsights.Themes([("Vị trí tuyệt vời, sạch sẽ, chủ nhà tốt", 5)]));
    }
}
