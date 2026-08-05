using Microsoft.AspNetCore.Mvc;
using StayHost.Web.Contracts;
using StayHost.Web.Infrastructure;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

[ApiController]
[Route("api")]
public class ListingsController(CatalogService catalog) : ControllerBase
{
    [HttpGet("meta")]
    public async Task<ActionResult<MetaDto>> Meta(CancellationToken ct) =>
        Ok(await catalog.GetMetaAsync(ct));

    [HttpGet("home")]
    public async Task<ActionResult<HomeDto>> Home(CancellationToken ct) =>
        Ok(await catalog.GetHomeAsync(HttpContext.SessionId(), ct));

    [HttpGet("suggest")]
    public async Task<ActionResult<IReadOnlyList<CatalogService.SuggestionDto>>> Suggest(
        [FromQuery] string? q, CancellationToken ct) =>
        Ok(await catalog.SuggestAsync(q, ct));

    [HttpGet("listings")]
    public async Task<ActionResult<SearchResultDto>> Search(
        [FromQuery] string? q,
        [FromQuery] string? category,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] int guests = 0,
        [FromQuery] string? amenities = null,
        [FromQuery] string? sort = "reco",
        [FromQuery] string? roomType = "any",
        [FromQuery] int? bedrooms = null,
        [FromQuery] int? beds = null,
        [FromQuery] int? bathrooms = null,
        [FromQuery] bool superhost = false,
        [FromQuery] bool guestFavorite = false,
        [FromQuery] bool instantBook = false,
        [FromQuery] bool freeCancellation = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        CancellationToken ct = default)
    {
        var keys = (amenities ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var query = new CatalogService.SearchQuery(
            q, category, minPrice, maxPrice, guests, keys, sort, roomType,
            bedrooms, beds, bathrooms, superhost, guestFavorite, instantBook, freeCancellation, page, pageSize);

        return Ok(await catalog.SearchAsync(query, HttpContext.SessionId(), ct));
    }

    [HttpGet("listings/{idOrSlug}")]
    public async Task<ActionResult<ListingDetailDto>> Detail(string idOrSlug, CancellationToken ct)
    {
        var detail = await catalog.GetDetailAsync(idOrSlug, HttpContext.SessionId(), ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpGet("quote")]
    public async Task<ActionResult<QuoteDto>> Quote(
        [FromQuery] int listingId,
        [FromQuery] DateOnly checkIn,
        [FromQuery] DateOnly checkOut,
        [FromQuery] int guests = 1,
        CancellationToken ct = default)
    {
        var quote = await catalog.QuoteAsync(listingId, checkIn, checkOut, guests, ct);
        return quote is null ? NotFound() : Ok(quote);
    }
}
