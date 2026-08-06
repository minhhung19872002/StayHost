namespace StayHost.Domain;

/// <summary>Who an article is written for (docs/01 AT-07).</summary>
public enum HelpAudience
{
    Everyone = 0,
    Guest = 1,
    Host = 2
}

public class HelpArticle
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public HelpAudience Audience { get; set; } = HelpAudience.Everyone;

    /// <summary>One line, shown in the list and in search results.</summary>
    public string Summary { get; set; } = "";

    /// <summary>The article itself. Blank lines separate paragraphs; "- " starts a list item.</summary>
    public string Body { get; set; } = "";

    /// <summary>Title, summary and body normalised, so "huy don" finds "huỷ đơn".</summary>
    public string SearchText { get; set; } = "";

    public int SortOrder { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public void RefreshSearchText() =>
        SearchText = SearchTextOf(Title, Summary, Body, Category);

    public static string SearchTextOf(params string[] parts) =>
        StayHost.Domain.SearchText.Normalize(string.Join(' ', parts));

    public static string AudienceLabel(HelpAudience audience) => audience switch
    {
        HelpAudience.Guest => "Dành cho khách",
        HelpAudience.Host => "Dành cho chủ nhà",
        _ => "Chung"
    };
}
