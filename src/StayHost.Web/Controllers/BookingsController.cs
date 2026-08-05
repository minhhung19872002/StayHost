using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Contracts;
using StayHost.Web.Infrastructure;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

[ApiController]
[Route("api/bookings")]
public class BookingsController(StayHostDbContext db, AuthService auth, NotificationService notifications)
    : ControllerBase
{
    /// <summary>Share of the guest total the platform keeps (the StayHost service fee).</summary>
    private const decimal PlatformCut = 1.0m;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BookingDto>>> List(CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        var sid = HttpContext.SessionId();

        var bookings = await db.Bookings
            .Where(b => user != null ? b.GuestUserId == user.Id : b.SessionId == sid && b.GuestUserId == null)
            .Include(b => b.Listing!).ThenInclude(l => l.Images)
            .Include(b => b.Listing!).ThenInclude(l => l.Host)
            .Include(b => b.Payment)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);

        return Ok(bookings.Select(ToDto).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<BookingDto>> Create([FromBody] CreateBookingRequest req, CancellationToken ct)
    {
        var listing = await db.Listings
            .Include(l => l.Images)
            .Include(l => l.Host)
            .FirstOrDefaultAsync(l => l.Id == req.ListingId, ct);
        if (listing is null) return NotFound(new { message = "Chỗ nghỉ không tồn tại." });
        if (!listing.IsPublished) return BadRequest(new { message = "Chỗ nghỉ này hiện không nhận đặt." });

        if (req.CheckOut <= req.CheckIn)
            return BadRequest(new { message = "Ngày trả phòng phải sau ngày nhận phòng." });

        var nights = req.CheckOut.DayNumber - req.CheckIn.DayNumber;
        if (nights < listing.MinNights)
            return BadRequest(new { message = $"Chỗ nghỉ này yêu cầu tối thiểu {listing.MinNights} đêm." });

        if (req.Guests < 1 || req.Guests > listing.MaxGuests)
            return BadRequest(new { message = $"Chỗ nghỉ này nhận tối đa {listing.MaxGuests} khách." });

        if (req.CheckIn < DateOnly.FromDateTime(DateTime.UtcNow))
            return BadRequest(new { message = "Không thể đặt ngày trong quá khứ." });

        var overlaps = await db.Bookings.AnyAsync(b =>
            b.ListingId == listing.Id &&
            b.Status != BookingStatus.Cancelled &&
            b.CheckIn < req.CheckOut && req.CheckIn < b.CheckOut, ct);
        if (overlaps)
            return Conflict(new { message = "Khoảng ngày này đã có người đặt. Vui lòng chọn ngày khác." });

        var blocked = await db.CalendarBlocks.AnyAsync(b =>
            b.ListingId == listing.Id && b.From < req.CheckOut && req.CheckIn <= b.To, ct);
        if (blocked)
            return Conflict(new { message = "Chủ nhà đã khoá lịch trong khoảng ngày này." });

        // Bookings carry money and liability, so they need a real account behind them.
        var user = await auth.CurrentUserAsync(ct);
        if (user is null)
            return Unauthorized(new { message = "Bạn cần đăng nhập để đặt chỗ." });

        var price = Pricing.Quote(listing, req.CheckIn, req.CheckOut);

        var booking = new Booking
        {
            Reference = "SH" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            SessionId = HttpContext.SessionId(),
            GuestUserId = user.Id,
            ListingId = listing.Id,
            Listing = listing,
            CheckIn = req.CheckIn,
            CheckOut = req.CheckOut,
            Guests = req.Guests,
            Nights = price.Nights,
            Subtotal = price.Subtotal,
            CleaningFee = price.CleaningFee,
            ServiceFee = price.ServiceFee,
            Tax = price.Tax,
            Total = price.Total,
            CancellationTier = listing.CancellationTier,
            GuestName = req.GuestName ?? user.FullName,
            GuestEmail = req.GuestEmail ?? user.Email,
            GuestNote = req.GuestNote,
            // Instant-book listings confirm immediately; the rest wait for the host.
            Status = listing.InstantBook ? BookingStatus.Confirmed : BookingStatus.Pending
        };

        // The platform keeps the service fee; tax is remitted, the rest goes to the host.
        var platformFee = Math.Round(price.ServiceFee * PlatformCut, 0, MidpointRounding.AwayFromZero);
        booking.Payment = new Payment
        {
            Reference = "PAY" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            Amount = price.Total,
            Currency = "VND",
            Method = string.IsNullOrWhiteSpace(req.PaymentMethod) ? "card" : req.PaymentMethod,
            CardLast4 = req.CardLast4 ?? "4242",
            Status = listing.InstantBook ? PaymentStatus.Captured : PaymentStatus.Authorized,
            CapturedAt = listing.InstantBook ? DateTime.UtcNow : null,
            PlatformFee = platformFee,
            HostPayout = price.Total - platformFee - price.Tax,
            PayoutDueOn = req.CheckIn.AddDays(1)
        };

        db.Bookings.Add(booking);
        await db.SaveChangesAsync(ct);

        var hostUser = await db.Users.FirstOrDefaultAsync(u => u.HostProfile!.Id == listing.HostId, ct);
        await notifications.QueueWithEmailAsync(hostUser, NotificationKind.BookingCreated,
            listing.InstantBook ? "Bạn có lượt đặt mới" : "Có yêu cầu đặt chỗ cần duyệt",
            $"{booking.GuestName} đặt \"{listing.Title}\" từ {booking.CheckIn:dd/MM} đến {booking.CheckOut:dd/MM} " +
            $"({booking.Nights} đêm, {booking.Guests} khách).",
            "/hosting", ct);

        await notifications.QueueWithEmailAsync(user, NotificationKind.BookingCreated,
            listing.InstantBook ? "Đặt chỗ đã được xác nhận" : "Đã gửi yêu cầu đặt chỗ",
            $"Mã đặt chỗ {booking.Reference} · {listing.Title} · {booking.Nights} đêm.",
            $"/trips/{booking.Id}", ct);

        await db.SaveChangesAsync(ct);

        return Created($"/api/bookings/{booking.Id}", ToDto(booking));
    }

    /// <summary>What the guest would get back if they cancelled right now.</summary>
    [HttpGet("{id:int}/refund-preview")]
    public async Task<ActionResult<RefundPreviewDto>> RefundPreview(int id, CancellationToken ct)
    {
        var booking = await FindOwnedAsync(id, ct);
        if (booking is null) return NotFound();

        var outcome = Pricing.Refund(booking, DateOnly.FromDateTime(DateTime.UtcNow));
        return Ok(new RefundPreviewDto(outcome.Amount, outcome.Penalty, booking.Total, outcome.Explanation));
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<RefundPreviewDto>> Cancel(int id, CancellationToken ct)
    {
        var booking = await FindOwnedAsync(id, ct);
        if (booking is null) return NotFound();
        if (booking.Status == BookingStatus.Cancelled)
            return BadRequest(new { message = "Chuyến đi này đã được huỷ." });

        var outcome = Pricing.Refund(booking, DateOnly.FromDateTime(DateTime.UtcNow));

        booking.Status = BookingStatus.Cancelled;
        booking.CancellationReason = "Khách huỷ";
        booking.RefundedAmount = outcome.Amount;

        if (booking.Payment is not null)
        {
            booking.Payment.Status = outcome.Amount >= booking.Total
                ? PaymentStatus.Refunded
                : PaymentStatus.Captured;
            // The host keeps whatever the policy does not refund, minus the platform cut.
            booking.Payment.HostPayout = Math.Max(0m, outcome.Penalty - booking.Payment.PlatformFee);
            booking.Payment.PayoutStatus = PayoutStatus.OnHold;
        }

        var listing = await db.Listings.Include(l => l.Host!).ThenInclude(h => h.User)
            .FirstOrDefaultAsync(l => l.Id == booking.ListingId, ct);

        await notifications.QueueWithEmailAsync(listing?.Host?.User, NotificationKind.BookingCancelled,
            "Khách đã huỷ đặt chỗ",
            $"Mã {booking.Reference} · {booking.CheckIn:dd/MM}–{booking.CheckOut:dd/MM} đã được huỷ.",
            "/hosting", ct);

        await db.SaveChangesAsync(ct);
        return Ok(new RefundPreviewDto(outcome.Amount, outcome.Penalty, booking.Total, outcome.Explanation));
    }

    /// <summary>A guest may review a stay once, after checkout.</summary>
    [HttpPost("{id:int}/review")]
    public async Task<IActionResult> Review(int id, [FromBody] SubmitReviewRequest req, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập để đánh giá." });

        var booking = await db.Bookings.Include(b => b.Listing)
            .FirstOrDefaultAsync(b => b.Id == id && b.GuestUserId == user.Id, ct);

        if (booking is null) return NotFound();
        if (booking.HasReview) return Conflict(new { message = "Bạn đã đánh giá chuyến đi này rồi." });
        if (booking.Status == BookingStatus.Cancelled)
            return BadRequest(new { message = "Chuyến đi đã huỷ không thể đánh giá." });
        if (booking.CheckOut > DateOnly.FromDateTime(DateTime.UtcNow))
            return BadRequest(new { message = "Bạn có thể đánh giá sau khi trả phòng." });

        var text = (req.Text ?? "").Trim();
        if (text.Length < 10) return BadRequest(new { message = "Nội dung đánh giá cần tối thiểu 10 ký tự." });

        double Clamp(double v) => Math.Clamp(v, 1, 5);

        var review = new Review
        {
            ListingId = booking.ListingId,
            BookingId = booking.Id,
            AuthorUserId = user.Id,
            AuthorName = user.FullName,
            AuthorInitials = user.Initials,
            When = $"Tháng {DateTime.UtcNow.Month}, {DateTime.UtcNow.Year}",
            Text = text,
            Rating = Clamp(req.Rating),
            Cleanliness = Clamp(req.Cleanliness),
            Accuracy = Clamp(req.Accuracy),
            CheckIn = Clamp(req.CheckIn),
            Communication = Clamp(req.Communication),
            Location = Clamp(req.Location),
            Value = Clamp(req.Value)
        };
        db.Reviews.Add(review);
        booking.HasReview = true;

        // Keep the denormalised rating on the listing in step with the new review.
        var listing = booking.Listing!;
        var existing = await db.Reviews.Where(r => r.ListingId == listing.Id).Select(r => r.Rating).ToListAsync(ct);
        existing.Add(review.Rating);
        listing.Rating = Math.Round(existing.Average(), 2);
        listing.ReviewCount = existing.Count;

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Single booking, used by the trip detail page and the printable receipt.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<BookingDto>> Detail(int id, CancellationToken ct)
    {
        var booking = await FindOwnedAsync(id, ct, includeListing: true);
        return booking is null ? NotFound() : Ok(ToDto(booking));
    }

    private async Task<Booking?> FindOwnedAsync(int id, CancellationToken ct, bool includeListing = false)
    {
        var user = await auth.CurrentUserAsync(ct);
        var sid = HttpContext.SessionId();

        var query = db.Bookings.Include(b => b.Payment).AsQueryable();
        if (includeListing)
        {
            query = query
                .Include(b => b.Listing!).ThenInclude(l => l.Images)
                .Include(b => b.Listing!).ThenInclude(l => l.Host);
        }

        return await query.FirstOrDefaultAsync(b =>
            b.Id == id && (user != null ? b.GuestUserId == user.Id : b.SessionId == sid), ct);
    }

    private static BookingDto ToDto(Booking b)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return new BookingDto(
            b.Id,
            b.Reference,
            b.ListingId,
            b.Listing?.Title ?? "",
            b.Listing?.City ?? "",
            b.Listing?.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault() ?? "",
            b.Listing?.Slug ?? "",
            b.CheckIn,
            b.CheckOut,
            b.Nights,
            b.Guests,
            b.Subtotal,
            b.CleaningFee,
            b.ServiceFee,
            b.Tax,
            b.Total,
            b.RefundedAmount,
            Pricing.TierLabel(b.CancellationTier),
            Pricing.TierSummary(b.CancellationTier),
            b.Status.ToString(),
            b.Payment?.Status.ToString() ?? "Pending",
            b.Payment?.Reference,
            b.Payment?.Method,
            b.Payment?.CardLast4,
            b.HasReview,
            b.CheckOut <= today && !b.HasReview && b.Status != BookingStatus.Cancelled,
            b.Status != BookingStatus.Cancelled && b.CheckIn > today,
            b.GuestNote,
            b.Listing?.Host?.Name ?? "",
            b.CreatedAt);
    }
}
