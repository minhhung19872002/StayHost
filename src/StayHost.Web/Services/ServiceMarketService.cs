using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Contracts;

namespace StayHost.Web.Services;

/// <summary>
/// docs/01 MR-05 → MR-07. A chef, a photographer, an airport transfer: sold by
/// the session, often at the guest's address, and sometimes run by a partner
/// the platform takes a commission from rather than a host.
/// </summary>
public class ServiceMarketService(
    StayHostDbContext db, CatalogService catalog, NotificationService notifications,
    PaymentGateway gateway, ILogger<ServiceMarketService> log)
{
    public async Task<IReadOnlyList<ServiceCardDto>> BrowseAsync(
        string? q, string? category, string? city, CancellationToken ct)
    {
        var query = db.ServiceOfferings.Where(o => o.IsPublished);

        foreach (var term in SearchText.Terms(q))
        {
            var t = term;
            query = query.Where(o => EF.Functions.Like(o.SearchText, $"%{t}%"));
        }

        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(o => o.Category == category);
        if (!string.IsNullOrWhiteSpace(city)) query = query.Where(o => o.City == city);

        return await query
            .OrderByDescending(o => o.Rating).ThenBy(o => o.Id)
            .Select(o => new ServiceCardDto(
                o.Id, o.Slug, o.Title, o.Category, o.City, o.Summary,
                o.BasePrice, o.Pricing.ToString(), ServiceRules.PricingLabel(o.Pricing),
                ServiceRules.UnitLabel(o.Pricing),
                o.TravelsToGuest, o.ServiceRadiusKm,
                o.IsPartner, o.PartnerName,
                o.Rating, o.ReviewCount, o.Host!.Name,
                o.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).ToList()))
            .ToListAsync(ct);
    }

    public async Task<ServiceDetailDto?> DetailAsync(string idOrSlug, CancellationToken ct)
    {
        var query = db.ServiceOfferings.Include(o => o.Host).Include(o => o.Images);

        var o = int.TryParse(idOrSlug, out var id)
            ? await query.FirstOrDefaultAsync(x => x.Id == id, ct)
            : await query.FirstOrDefaultAsync(x => x.Slug == idOrSlug, ct);
        if (o is null) return null;

        // The next fortnight of taken slots, so the picker can grey them out.
        var from = DateTime.UtcNow;
        var to = from.AddDays(14);
        var busy = await db.ServiceBookings
            .Where(b => b.OfferingId == o.Id
                        && b.Status != ServiceBookingStatus.CancelledByGuest
                        && b.Status != ServiceBookingStatus.CancelledByProvider
                        && b.StartsAt < to && b.StartsAt >= from.AddDays(-1))
            .Select(b => new { b.StartsAt, b.DurationMinutes })
            .ToListAsync(ct);

        return new ServiceDetailDto(
            o.Id, o.Slug, o.Title, o.Category, o.City, o.Country, o.Summary, o.Description,
            o.BasePrice, o.Pricing.ToString(), ServiceRules.PricingLabel(o.Pricing),
            ServiceRules.UnitLabel(o.Pricing),
            o.MinQuantity, o.MaxQuantity, o.DurationMinutes,
            o.TravelsToGuest, o.ServiceRadiusKm, o.Latitude, o.Longitude,
            o.OpensAtHour, o.ClosesAtHour,
            o.IsPartner, o.PartnerName, o.IsPublished,
            o.Rating, o.ReviewCount, o.Host?.Name ?? "", o.Host?.Initials ?? "",
            o.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).ToList(),
            busy.Select(b => new BusySlotDto(b.StartsAt, b.StartsAt.AddMinutes(b.DurationMinutes))).ToList(),
            ServiceRules.RequiredNote(o.Category) is var kind && kind != ServiceRules.NoteKind.None
                ? ServiceRules.NoteLabel(kind) : null);
    }

    public async Task<ServiceQuoteDto?> QuoteAsync(int offeringId, QuoteServiceRequest req, CancellationToken ct)
    {
        var offering = await db.ServiceOfferings.FirstOrDefaultAsync(o => o.Id == offeringId, ct);
        if (offering is null) return null;

        var check = ServiceRules.CanBook(await BuildCheckAsync(offering, req.StartsAt, req.Quantity,
            req.Address, req.Latitude, req.Longitude, ct));

        var price = Pricing.QuoteService(new Pricing.ServiceRequest
        {
            Offering = offering,
            Quantity = req.Quantity,
            StartsAt = req.StartsAt,
            TaxRules = await catalog.ActiveTaxRulesAsync(ct)
        });

        return new ServiceQuoteDto(
            offering.Id, req.StartsAt, offering.DurationMinutes, price.Quantity,
            price.Subtotal, price.GuestServiceFee, price.Tax, price.Total,
            price.Lines.Select(l => new PriceLineDto(l.Key, l.Label, l.Amount)).ToList(),
            check.Ok, check.Ok ? null : check.Message);
    }

    private async Task<ServiceRules.Request> BuildCheckAsync(
        ServiceOffering offering, DateTime startsAt, int quantity,
        string? address, double? lat, double? lng, CancellationToken ct)
    {
        var day = startsAt.Date;
        var busy = await db.ServiceBookings
            .Where(b => b.OfferingId == offering.Id
                        && b.Status != ServiceBookingStatus.CancelledByGuest
                        && b.Status != ServiceBookingStatus.CancelledByProvider
                        && b.StartsAt >= day.AddDays(-1) && b.StartsAt < day.AddDays(2))
            .Select(b => new { b.StartsAt, b.DurationMinutes })
            .ToListAsync(ct);

        return new ServiceRules.Request
        {
            Offering = offering,
            StartsAt = DateTime.SpecifyKind(startsAt, DateTimeKind.Utc),
            Now = DateTime.UtcNow,
            Quantity = quantity,
            Address = address,
            Latitude = lat,
            Longitude = lng,
            Busy = busy.Select(b => (b.StartsAt, b.StartsAt.AddMinutes(b.DurationMinutes))).ToList()
        };
    }

    public async Task<(ServiceBooking? Booking, string? Error)> BookAsync(
        User user, int offeringId, BookServiceRequest req, CancellationToken ct)
    {
        var offering = await db.ServiceOfferings.FirstOrDefaultAsync(o => o.Id == offeringId, ct);
        if (offering is null) return (null, "Không tìm thấy dịch vụ này.");

        var check = ServiceRules.CanBook(await BuildCheckAsync(
            offering, req.StartsAt, req.Quantity, req.Address, req.Latitude, req.Longitude, ct));
        if (!check.Ok) return (null, check.Message);

        // docs/09 §3.5 (scenario 10) — a chef/massage/fitness job cannot be sent
        // without the safety note its category demands.
        if (ServiceRules.NoteMissing(offering.Category, req.Note))
            return (null, $"Cần điền {ServiceRules.NoteLabel(ServiceRules.RequiredNote(offering.Category))} trước khi đặt.");

        var price = Pricing.QuoteService(new Pricing.ServiceRequest
        {
            Offering = offering,
            Quantity = req.Quantity,
            StartsAt = req.StartsAt,
            TaxRules = await catalog.ActiveTaxRulesAsync(ct)
        });

        var attempt = gateway.Charge(price.Total, req.PaymentMethod ?? "card", req.CardLast4);
        if (!attempt.Ok) return (null, attempt.Reason);

        var booking = new ServiceBooking
        {
            Reference = $"SV{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            OfferingId = offering.Id,
            GuestUserId = user.Id,
            StartsAt = DateTime.SpecifyKind(req.StartsAt, DateTimeKind.Utc),
            DurationMinutes = offering.DurationMinutes,
            Quantity = price.Quantity,
            Address = (req.Address ?? "").Trim(),
            Latitude = req.Latitude ?? 0,
            Longitude = req.Longitude ?? 0,
            Note = req.Note?.Trim(),
            Subtotal = price.Subtotal,
            ServiceFee = price.GuestServiceFee,
            Tax = price.Tax,
            Total = price.Total,
            PlatformCut = price.PlatformCut,
            ProviderPayout = price.ProviderPayout
        };

        db.ServiceBookings.Add(booking);
        await db.SaveChangesAsync(ct);

        db.LedgerEntries.AddRange(Ledger.CaptureService(booking, price, DateTime.UtcNow));
        await db.SaveChangesAsync(ct);

        await notifications.QueueWithEmailAsync(
            user, NotificationKind.BookingConfirmed,
            "Đã đặt dịch vụ",
            $"{offering.Title} · {booking.StartsAt:dd/MM HH:mm} · mã {booking.Reference}.",
            "/services/bookings", ct);
        await db.SaveChangesAsync(ct);

        log.LogInformation("Service {Reference} booked.", booking.Reference);
        return (booking, null);
    }

    public async Task<string?> CancelAsync(int userId, int bookingId, CancellationToken ct)
    {
        var booking = await db.ServiceBookings
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.GuestUserId == userId, ct);
        if (booking is null) return "Không tìm thấy đơn dịch vụ này.";
        if (booking.Status is not (ServiceBookingStatus.Confirmed or ServiceBookingStatus.Requested))
            return "Đơn này không còn hiệu lực.";

        var refund = ServiceRules.GuestRefund(booking, DateTime.UtcNow);

        booking.Status = ServiceBookingStatus.CancelledByGuest;
        booking.RefundedAmount = refund;
        booking.CancelReason = refund > 0
            ? "Khách huỷ trước 24 giờ."
            : "Khách huỷ sát giờ, không hoàn tiền.";
        booking.CancelledAt = DateTime.UtcNow;

        db.LedgerEntries.AddRange(Ledger.RefundService(booking, refund, DateTime.UtcNow));
        await db.SaveChangesAsync(ct);

        return null;
    }

    public async Task<IReadOnlyList<ServiceBookingDto>> MyBookingsAsync(int userId, CancellationToken ct) =>
        await db.ServiceBookings
            .Where(b => b.GuestUserId == userId)
            .OrderByDescending(b => b.StartsAt)
            .Select(b => new ServiceBookingDto(
                b.Id, b.Reference, b.OfferingId, b.Offering!.Title, b.Offering.Slug,
                b.Offering.Category, b.Offering.City,
                b.StartsAt, b.DurationMinutes, b.Quantity,
                ServiceRules.UnitLabel(b.Offering.Pricing),
                b.Address, b.Note,
                b.Subtotal, b.ServiceFee, b.Tax, b.Total, b.RefundedAmount,
                b.Status.ToString(), ServiceRules.StatusLabel(b.Status), ServiceRules.StatusBadge(b.Status),
                b.CancelReason, b.CreatedAt))
            .ToListAsync(ct);

    public async Task<ServiceBookingDto?> BookingDtoAsync(int id, CancellationToken ct) =>
        await db.ServiceBookings
            .Where(b => b.Id == id)
            .Select(b => new ServiceBookingDto(
                b.Id, b.Reference, b.OfferingId, b.Offering!.Title, b.Offering.Slug,
                b.Offering.Category, b.Offering.City,
                b.StartsAt, b.DurationMinutes, b.Quantity,
                ServiceRules.UnitLabel(b.Offering.Pricing),
                b.Address, b.Note,
                b.Subtotal, b.ServiceFee, b.Tax, b.Total, b.RefundedAmount,
                b.Status.ToString(), ServiceRules.StatusLabel(b.Status), ServiceRules.StatusBadge(b.Status),
                b.CancelReason, b.CreatedAt))
            .FirstOrDefaultAsync(ct);
}
