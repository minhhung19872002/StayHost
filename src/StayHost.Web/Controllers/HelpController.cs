using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Contracts;

namespace StayHost.Web.Controllers;

/// <summary>
/// docs/01 AT-07 — a help centre with real articles, a search that copes with
/// missing accents, and guest content kept apart from host content.
/// </summary>
[ApiController]
[Route("api/help")]
public class HelpController(StayHostDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<HelpIndexDto>> Index(
        [FromQuery] string? q, [FromQuery] string? audience, CancellationToken ct = default)
    {
        var wanted = ParseAudience(audience);

        IQueryable<HelpArticle> query = db.HelpArticles;

        // "Chung" articles belong to both sides, so a filtered view still shows them.
        if (wanted is { } only)
            query = query.Where(a => a.Audience == only || a.Audience == HelpAudience.Everyone);

        foreach (var term in SearchText.Terms(q))
        {
            var t = term;
            query = query.Where(a => EF.Functions.Like(a.SearchText, $"%{t}%"));
        }

        var articles = await query
            .OrderBy(a => a.Category).ThenBy(a => a.SortOrder)
            .Select(a => new HelpArticleDto(
                a.Slug, a.Title, a.Category, a.Audience.ToString().ToLower(),
                HelpArticle.AudienceLabel(a.Audience), a.Summary, null, a.UpdatedAt))
            .ToListAsync(ct);

        // The category list comes from everything on this side of the fence, not
        // from the search results, so the tabs do not vanish as you type.
        var categories = await (wanted is { } side
                ? db.HelpArticles.Where(a => a.Audience == side || a.Audience == HelpAudience.Everyone)
                : db.HelpArticles)
            .GroupBy(a => a.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return Ok(new HelpIndexDto(
            articles,
            categories.OrderBy(c => c.Category).Select(c => new HelpCategoryDto(c.Category, c.Count)).ToList(),
            articles.Count));
    }

    [HttpGet("{slug}")]
    public async Task<ActionResult<HelpArticleDto>> Article(string slug, CancellationToken ct)
    {
        var a = await db.HelpArticles.FirstOrDefaultAsync(x => x.Slug == slug, ct);
        if (a is null) return NotFound();

        return Ok(new HelpArticleDto(
            a.Slug, a.Title, a.Category, a.Audience.ToString().ToLower(),
            HelpArticle.AudienceLabel(a.Audience), a.Summary, a.Body, a.UpdatedAt));
    }

    private static HelpAudience? ParseAudience(string? value) => value?.ToLowerInvariant() switch
    {
        "guest" => HelpAudience.Guest,
        "host" => HelpAudience.Host,
        _ => null
    };
}
