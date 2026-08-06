using Microsoft.AspNetCore.Mvc;
using StayHost.Web.Contracts;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

/// <summary>
/// docs/01 MR-05 → MR-07 — services booked by the time slot, at an address the
/// guest gives, from a host or from a partner the platform takes a cut of.
/// </summary>
[ApiController]
[Route("api/services")]
public class ServicesController(AuthService auth, ServiceMarketService market) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ServiceCardDto>>> Browse(
        [FromQuery] string? q, [FromQuery] string? category, [FromQuery] string? city,
        CancellationToken ct = default) =>
        Ok(await market.BrowseAsync(q, category, city, ct));

    [HttpGet("bookings")]
    public async Task<ActionResult<IReadOnlyList<ServiceBookingDto>>> MyBookings(CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        return Ok(await market.MyBookingsAsync(user.Id, ct));
    }

    [HttpPost("bookings/{id:int}/cancel")]
    public async Task<ActionResult<ServiceBookingDto>> Cancel(int id, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var error = await market.CancelAsync(user.Id, id, ct);
        if (error is not null) return BadRequest(new { message = error });

        return Ok(await market.BookingDtoAsync(id, ct));
    }

    [HttpGet("{idOrSlug}")]
    public async Task<ActionResult<ServiceDetailDto>> Detail(string idOrSlug, CancellationToken ct)
    {
        var detail = await market.DetailAsync(idOrSlug, ct);
        return detail is null ? NotFound() : Ok(detail);
    }

    /// <summary>
    /// What a job would cost and whether it can be taken at all — the address
    /// matters here, because a provider only travels so far (docs/01 MR-05).
    /// </summary>
    [HttpPost("{id:int}/quote")]
    public async Task<ActionResult<ServiceQuoteDto>> Quote(
        int id, [FromBody] QuoteServiceRequest req, CancellationToken ct)
    {
        var quote = await market.QuoteAsync(id, req, ct);
        return quote is null ? NotFound() : Ok(quote);
    }

    [HttpPost("{id:int}/book")]
    public async Task<ActionResult<ServiceBookingDto>> Book(
        int id, [FromBody] BookServiceRequest req, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var (booking, error) = await market.BookAsync(user, id, req, ct);
        if (booking is null) return BadRequest(new { message = error });

        return Ok(await market.BookingDtoAsync(booking.Id, ct));
    }
}
