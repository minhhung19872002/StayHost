using Microsoft.AspNetCore.Mvc;
using StayHost.Domain;
using StayHost.Web.Contracts;
using StayHost.Web.Infrastructure;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

[ApiController]
[Route("api")]
public class ListingsController(CatalogService catalog, BookingService bookings) : ControllerBase
{
    /// <summary>
    /// Nightly rates and availability for the date picker (docs/01 TM-05), plus
    /// the next free windows when the guest's dates are gone (TĐ-09).
    /// </summary>
    [HttpGet("listings/{id:int}/calendar")]
    public async Task<ActionResult<ListingCalendarDto>> Calendar(
        int id,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct = default)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var calendar = await bookings.CalendarAsync(id, start, to ?? start.AddMonths(4), ct);
        return calendar is null ? NotFound() : Ok(calendar);
    }

    [HttpGet("meta")]
    public async Task<ActionResult<MetaDto>> Meta(CancellationToken ct) =>
        Ok(await catalog.GetMetaAsync(ct));

    /// <summary>
    /// Dates are optional but, when present, every card comes back priced for
    /// them — that is what keeps the card, the room page and checkout in step
    /// (docs/00 §6.8).
    /// </summary>
    [HttpGet("home")]
    public async Task<ActionResult<HomeDto>> Home(
        [FromQuery] DateOnly? checkIn,
        [FromQuery] DateOnly? checkOut,
        [FromQuery] int guests = 1,
        CancellationToken ct = default) =>
        Ok(await catalog.GetHomeAsync(HttpContext.SessionId(), checkIn, checkOut, guests, ct));

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
        [FromQuery] DateOnly? checkIn = null,
        [FromQuery] DateOnly? checkOut = null,
        [FromQuery] double? south = null,
        [FromQuery] double? west = null,
        [FromQuery] double? north = null,
        [FromQuery] double? east = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 24,
        CancellationToken ct = default)
    {
        var query = BuildQuery(
            q, category, minPrice, maxPrice, guests, amenities, sort, roomType,
            bedrooms, beds, bathrooms, superhost, guestFavorite, instantBook, freeCancellation,
            checkIn, checkOut, south, west, north, east, page, pageSize);

        return Ok(await catalog.SearchAsync(query, HttpContext.SessionId(), ct));
    }

    /// <summary>
    /// docs/01 TM-19 — the filter panel needs the number of matches as the guest
    /// changes a filter, without paying for a page of listings.
    /// </summary>
    [HttpGet("listings/count")]
    public async Task<ActionResult<object>> Count(
        [FromQuery] string? q,
        [FromQuery] string? category,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] int guests = 0,
        [FromQuery] string? amenities = null,
        [FromQuery] string? roomType = "any",
        [FromQuery] int? bedrooms = null,
        [FromQuery] int? beds = null,
        [FromQuery] int? bathrooms = null,
        [FromQuery] bool superhost = false,
        [FromQuery] bool guestFavorite = false,
        [FromQuery] bool instantBook = false,
        [FromQuery] bool freeCancellation = false,
        CancellationToken ct = default)
    {
        var query = BuildQuery(
            q, category, minPrice, maxPrice, guests, amenities, "reco", roomType,
            bedrooms, beds, bathrooms, superhost, guestFavorite, instantBook, freeCancellation,
            null, null, null, null, null, null, 1, 1);

        return Ok(new { total = await catalog.CountAsync(query, ct) });
    }

    private static CatalogService.SearchQuery BuildQuery(
        string? q, string? category, decimal? minPrice, decimal? maxPrice, int guests,
        string? amenities, string? sort, string? roomType,
        int? bedrooms, int? beds, int? bathrooms,
        bool superhost, bool guestFavorite, bool instantBook, bool freeCancellation,
        DateOnly? checkIn, DateOnly? checkOut,
        double? south, double? west, double? north, double? east,
        int page, int pageSize)
    {
        var keys = (amenities ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var bounds = south is not null && west is not null && north is not null && east is not null
            ? new CatalogService.MapBounds(south.Value, west.Value, north.Value, east.Value)
            : (CatalogService.MapBounds?)null;

        return new CatalogService.SearchQuery(
            q, category, minPrice, maxPrice, guests, keys, sort, roomType,
            bedrooms, beds, bathrooms, superhost, guestFavorite, instantBook, freeCancellation,
            page, pageSize, checkIn, checkOut, bounds);
    }

    [HttpGet("listings/{idOrSlug}")]
    public async Task<ActionResult<ListingDetailDto>> Detail(
        string idOrSlug,
        [FromQuery] DateOnly? checkIn,
        [FromQuery] DateOnly? checkOut,
        [FromQuery] int guests = 1,
        CancellationToken ct = default)
    {
        var detail = await catalog.GetDetailAsync(idOrSlug, HttpContext.SessionId(), checkIn, checkOut, guests, ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpGet("quote")]
    public async Task<ActionResult<QuoteDto>> Quote(
        [FromQuery] int listingId,
        [FromQuery] DateOnly checkIn,
        [FromQuery] DateOnly checkOut,
        [FromQuery] int guests = 1,
        [FromQuery] int? adults = null,
        [FromQuery] int children = 0,
        [FromQuery] int infants = 0,
        [FromQuery] int pets = 0,
        CancellationToken ct = default)
    {
        // `guests` is the legacy single number; adults/children win when supplied.
        var party = adults is null
            ? PartySize.Of(guests) with { Infants = infants, Pets = pets }
            : new PartySize(Math.Max(1, adults.Value), children, infants, pets);

        var quote = await catalog.QuoteAsync(listingId, checkIn, checkOut, party, ct);
        return quote is null ? NotFound() : Ok(quote);
    }
}
