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
public class BookingsController(
    StayHostDbContext db, AuthService auth, NotificationService notifications,
    CatalogService catalog, BookingService rules)
    : ControllerBase
{
    /// <summary>
    /// The exclusion constraint added by the DoubleBookingGuard migration. It is
    /// the only thing that can decide between two simultaneous checkouts, so its
    /// violation is a normal outcome, not a server error.
    /// </summary>
    private static bool IsOverlapViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("bookings_no_overlap", StringComparison.OrdinalIgnoreCase) == true;

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
            .Include(b => b.Events)
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

        // docs/03 §2 rule 2: infants count towards neither capacity nor price.
        var party = req.Adults is null
            ? PartySize.Of(req.Guests) with { Infants = req.Infants, Pets = req.Pets }
            : new PartySize(Math.Max(1, req.Adults.Value), req.Children, req.Infants, req.Pets);

        // The nine checks of docs/03 §2, in order, stopping at the first failure.
        var check = await rules.CheckAsync(listing, req.CheckIn, req.CheckOut, party, ct);
        if (!check.Ok)
        {
            return check.Reason is Availability.Reason.DatesTaken or Availability.Reason.TurnoverTime
                ? Conflict(new { message = check.Message, reason = check.Reason.ToString() })
                : BadRequest(new { message = check.Message, reason = check.Reason.ToString() });
        }

        // Bookings carry money and liability, so they need a real account behind them.
        var user = await auth.CurrentUserAsync(ct);
        if (user is null)
            return Unauthorized(new { message = "Bạn cần đăng nhập để đặt chỗ." });

        // Quoting and booking go through the same builder so the guest is charged
        // exactly what the room page showed them (docs/00 §6.8).
        var quoteRequest = await catalog.BuildQuoteRequestAsync(listing.Id, req.CheckIn, req.CheckOut, party, ct);
        var price = Pricing.Quote(quoteRequest!);

        var booking = new Booking
        {
            Reference = "SH" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
            SessionId = HttpContext.SessionId(),
            GuestUserId = user.Id,
            ListingId = listing.Id,
            Listing = listing,
            CheckIn = req.CheckIn,
            CheckOut = req.CheckOut,
            Guests = party.Counted,
            Adults = party.Adults,
            Children = party.Children,
            Infants = party.Infants,
            Pets = party.Pets,
            Nights = price.Nights,
            RoomBeforeDiscount = price.RoomBeforeDiscount,
            RoomDiscount = price.RoomDiscount,
            DiscountPercent = price.DiscountPercent,
            ExtraGuestFee = price.ExtraGuestFee,
            PetFee = price.PetFee,
            CleaningFee = price.CleaningFee,
            Subtotal = price.Subtotal,
            ServiceFee = price.GuestServiceFee,
            Tax = price.Tax,
            Promotion = price.Promotion,
            Total = price.Total,
            HostServiceFee = price.HostServiceFee,
            HostPayout = price.HostPayout,
            PriceLinesJson = SerializeLines(price.Lines),
            CancellationTier = listing.CancellationTier,
            GuestName = req.GuestName ?? user.FullName,
            GuestEmail = req.GuestEmail ?? user.Email,
            GuestNote = req.GuestNote,
            // docs/03 §2–§3: instant book takes the dates off the market for 15
            // minutes while the guest pays; a request waits 24 hours on the host
            // and deliberately does not hold the dates at all.
            Status = listing.InstantBook ? BookingStatus.PendingPayment : BookingStatus.PendingHostApproval,
            HoldExpiresAt = listing.InstantBook ? DateTime.UtcNow + BookingLifecycle.PaymentHold : null,
            RequestExpiresAt = listing.InstantBook ? null : DateTime.UtcNow + BookingLifecycle.RequestWindow
        };

        booking.Payment = new Payment
        {
            Reference = "PAY" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(),
            Amount = price.Total,
            Currency = "VND",
            Method = string.IsNullOrWhiteSpace(req.PaymentMethod) ? "card" : req.PaymentMethod,
            CardLast4 = req.CardLast4 ?? "4242",
            Status = PaymentStatus.Authorized,
            PlatformFee = price.GuestServiceFee + price.HostServiceFee,
            HostPayout = price.HostPayout,
            PayoutDueOn = req.CheckIn.AddDays(1)
        };

        db.Bookings.Add(booking);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsOverlapViolation(ex))
        {
            // Two guests reached checkout for the same nights at the same moment.
            // The database exclusion constraint decided; this one lost.
            return Conflict(new
            {
                message = "Khoảng ngày này vừa có người khác đặt xong. Vui lòng chọn ngày khác.",
                reason = nameof(Availability.Reason.DatesTaken)
            });
        }

        db.BookingEvents.Add(BookingLifecycle.Created(booking, $"guest:{user.Id}",
            listing.InstantBook ? "Giữ chỗ 15 phút để thanh toán" : "Gửi yêu cầu đặt"));
        await db.SaveChangesAsync(ct);

        // An instant booking is still unpaid at this point, so nobody is told
        // about it until the money is actually taken.
        if (!listing.InstantBook)
        {
            var hostUser = await db.Users.FirstOrDefaultAsync(u => u.HostProfile!.Id == listing.HostId, ct);
            await notifications.QueueWithEmailAsync(hostUser, NotificationKind.BookingCreated,
                "Có yêu cầu đặt chỗ cần duyệt",
                $"{booking.GuestName} đặt \"{listing.Title}\" từ {booking.CheckIn:dd/MM} đến {booking.CheckOut:dd/MM} " +
                $"({booking.Nights} đêm, {booking.Guests} khách). Bạn có 24 giờ để trả lời.",
                "/hosting", ct);

            await notifications.QueueWithEmailAsync(user, NotificationKind.BookingCreated,
                "Đã gửi yêu cầu đặt chỗ",
                $"Mã đặt chỗ {booking.Reference} · {listing.Title} · {booking.Nights} đêm. " +
                "Yêu cầu đặt không giữ ngày: ai trả tiền xong trước thì được.",
                $"/trips/{booking.Id}", ct);

            await db.SaveChangesAsync(ct);
        }

        return Created($"/api/bookings/{booking.Id}", ToDto(booking));
    }

    /// <summary>
    /// Takes the money for a booking that is holding its dates. docs/01 ĐP-12:
    /// the server prices the stay again here and refuses to charge a number
    /// different from the one the guest agreed to.
    /// </summary>
    [HttpPost("{id:int}/pay")]
    public async Task<ActionResult<BookingDto>> Pay(int id, [FromBody] PayBookingRequest? req, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var booking = await db.Bookings
            .Include(b => b.Payment).Include(b => b.Events)
            .Include(b => b.Listing!).ThenInclude(l => l.Images)
            .FirstOrDefaultAsync(b => b.Id == id && b.GuestUserId == user.Id, ct);

        if (booking is null) return NotFound();

        if (booking.Status != BookingStatus.PendingPayment)
        {
            return BadRequest(new
            {
                message = $"Đơn đang ở trạng thái \"{BookingLifecycle.Label(booking.Status)}\" nên không thanh toán được."
            });
        }

        if (booking.HoldExpiresAt is { } expiry && expiry < DateTime.UtcNow)
        {
            db.BookingEvents.Add(BookingLifecycle.Transition(
                booking, BookingStatus.PaymentFailed, "system", "Hết 15 phút giữ chỗ mà chưa thanh toán xong."));
            await db.SaveChangesAsync(ct);
            return Conflict(new { message = "Đã quá 15 phút giữ chỗ. Vui lòng chọn lại ngày và đặt lại." });
        }

        // ĐP-12 — price it again and stop if anything moved while the guest paid.
        var party = new PartySize(booking.Adults, booking.Children, booking.Infants, booking.Pets);
        var fresh = await catalog.BuildQuoteRequestAsync(booking.ListingId, booking.CheckIn, booking.CheckOut, party, ct);
        var price = Pricing.Quote(fresh!);

        if (price.Total != booking.Total)
        {
            return Conflict(new
            {
                message = $"Giá vừa thay đổi từ {booking.Total:#,##0}₫ thành {price.Total:#,##0}₫. " +
                          "Vui lòng xem lại trước khi thanh toán.",
                newTotal = price.Total,
                oldTotal = booking.Total
            });
        }

        db.BookingEvents.Add(BookingLifecycle.Transition(
            booking, BookingStatus.Confirmed, $"guest:{user.Id}", "Thanh toán thành công."));

        if (booking.Payment is not null)
        {
            booking.Payment.Status = PaymentStatus.Captured;
            booking.Payment.CapturedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(req?.PaymentMethod)) booking.Payment.Method = req.PaymentMethod;
            if (!string.IsNullOrWhiteSpace(req?.CardLast4)) booking.Payment.CardLast4 = req.CardLast4;
        }

        db.LedgerEntries.AddRange(Ledger.CaptureBooking(booking, price, DateTime.UtcNow));
        await db.SaveChangesAsync(ct);

        var listing = booking.Listing!;
        var hostUser = await db.Users.FirstOrDefaultAsync(u => u.HostProfile!.Id == listing.HostId, ct);

        await notifications.QueueWithEmailAsync(hostUser, NotificationKind.BookingConfirmed,
            "Bạn có lượt đặt mới",
            $"{booking.GuestName} đặt \"{listing.Title}\" từ {booking.CheckIn:dd/MM} đến {booking.CheckOut:dd/MM} " +
            $"({booking.Nights} đêm, {booking.Guests} khách).",
            "/hosting", ct);

        await notifications.QueueWithEmailAsync(user, NotificationKind.BookingConfirmed,
            "Đặt chỗ đã được xác nhận",
            $"Mã đặt chỗ {booking.Reference} · {listing.Title} · {booking.Nights} đêm.",
            $"/trips/{booking.Id}", ct);

        await db.SaveChangesAsync(ct);
        return Ok(ToDto(booking));
    }

    /// <summary>The guest walked away from checkout; the dates go back on sale.</summary>
    [HttpPost("{id:int}/release")]
    public async Task<IActionResult> Release(int id, CancellationToken ct)
    {
        var booking = await FindOwnedAsync(id, ct);
        if (booking is null) return NotFound();
        if (booking.Status != BookingStatus.PendingPayment) return NoContent();

        db.BookingEvents.Add(BookingLifecycle.Transition(
            booking, BookingStatus.CancelledByGuest, "guest", "Khách rời bước thanh toán."));
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>What the guest would get back if they cancelled right now.</summary>
    [HttpGet("{id:int}/refund-preview")]
    public async Task<ActionResult<RefundPreviewDto>> RefundPreview(int id, CancellationToken ct)
    {
        var booking = await FindOwnedAsync(id, ct);
        if (booking is null) return NotFound();

        var outcome = Cancellation.Refund(await BuildCancelContextAsync(booking, CancelledBy.Guest, ct));
        return Ok(ToPreview(booking, outcome));
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<RefundPreviewDto>> Cancel(int id, CancellationToken ct)
    {
        var booking = await FindOwnedAsync(id, ct);
        if (booking is null) return NotFound();

        if (!BookingLifecycle.CanTransition(booking.Status, BookingStatus.CancelledByGuest))
        {
            return BadRequest(new
            {
                message = $"Đơn đang ở trạng thái \"{BookingLifecycle.Label(booking.Status)}\" nên không huỷ được."
            });
        }

        var outcome = Cancellation.Refund(await BuildCancelContextAsync(booking, CancelledBy.Guest, ct));
        await ApplyCancellationAsync(booking, outcome, CancelledBy.Guest, "Khách huỷ", ct);

        var listing = await db.Listings.Include(l => l.Host!).ThenInclude(h => h.User)
            .FirstOrDefaultAsync(l => l.Id == booking.ListingId, ct);

        await notifications.QueueWithEmailAsync(listing?.Host?.User, NotificationKind.BookingCancelled,
            "Khách đã huỷ đặt chỗ",
            $"Mã {booking.Reference} · {booking.CheckIn:dd/MM}–{booking.CheckOut:dd/MM} đã được huỷ.",
            "/hosting", ct);

        await db.SaveChangesAsync(ct);
        return Ok(ToPreview(booking, outcome));
    }

    private static RefundPreviewDto ToPreview(Booking b, Cancellation.Outcome o) => new(
        o.Amount, b.Total - o.Amount, b.Total, o.Explanation,
        o.RoomRefund, o.CleaningRefund, o.ServiceFeeRefund, o.TaxRefund, o.GoodwillCredit);

    /// <summary>
    /// docs/03 §4 pre-rule 2 caps service-fee refunds at three a year, so the
    /// guest's recent history is part of the calculation.
    /// </summary>
    private async Task<Cancellation.Context> BuildCancelContextAsync(Booking booking, CancelledBy by, CancellationToken ct)
    {
        var yearAgo = DateTime.UtcNow.AddYears(-1);
        var used = booking.GuestUserId is null
            ? 0
            : await db.Bookings.CountAsync(b =>
                b.GuestUserId == booking.GuestUserId &&
                b.Status == BookingStatus.CancelledByGuest &&
                b.RefundedAmount > 0 &&
                b.CreatedAt >= yearAgo, ct);

        return new Cancellation.Context
        {
            Booking = booking,
            Now = DateTime.UtcNow,
            By = by,
            ServiceFeeRefundsUsed = used
        };
    }

    /// <summary>
    /// Cancels the booking and books the matching double-entry transaction, so
    /// the ledger still balances afterwards (docs/00 §6.1).
    /// </summary>
    internal static void PostCancellation(
        StayHostDbContext db, Booking booking, Cancellation.Outcome outcome, CancelledBy by, string reason)
    {
        var to = by == CancelledBy.Host ? BookingStatus.CancelledByHost : BookingStatus.CancelledByGuest;
        var actor = by switch
        {
            CancelledBy.Host => "host",
            CancelledBy.ForceMajeure => "admin",
            CancelledBy.Platform => "system",
            _ => "guest"
        };

        db.BookingEvents.Add(BookingLifecycle.Transition(booking, to, actor, reason));

        booking.CancellationReason = reason;
        booking.CancelledBy = by;
        booking.RefundedAmount = outcome.Amount;
        booking.GoodwillCredit = outcome.GoodwillCredit;

        if (booking.Payment is not null)
        {
            booking.Payment.Status = outcome.Amount >= booking.Total ? PaymentStatus.Refunded : PaymentStatus.Captured;
            booking.Payment.HostPayout = Math.Max(0m, booking.HostPayout - (outcome.RoomRefund + outcome.CleaningRefund));
            booking.Payment.PayoutStatus = PayoutStatus.OnHold;
        }

        // Nothing was ever captured for a pending request, so there is nothing to reverse.
        var captured = db.LedgerEntries.Local.Any(e => e.BookingId == booking.Id)
                       || db.LedgerEntries.Any(e => e.BookingId == booking.Id && e.TransactionKind == "booking-captured");
        if (!captured) return;

        // The host's fee is returned in proportion to the money leaving their side.
        var hostSideRefund = outcome.RoomRefund + outcome.CleaningRefund;
        var hostFeeReturned = booking.Subtotal > 0
            ? Math.Round(booking.HostServiceFee * hostSideRefund / booking.Subtotal, 0, MidpointRounding.AwayFromZero)
            : 0m;
        hostFeeReturned = Math.Min(hostFeeReturned, hostSideRefund);

        db.LedgerEntries.AddRange(Ledger.RefundBooking(booking, outcome, hostFeeReturned, DateTime.UtcNow));
        if (outcome.Amount > 0)
            db.LedgerEntries.AddRange(Ledger.SettleRefund(booking, outcome.Amount, DateTime.UtcNow));
    }

    private async Task ApplyCancellationAsync(
        Booking booking, Cancellation.Outcome outcome, CancelledBy by, string reason, CancellationToken ct)
    {
        PostCancellation(db, booking, outcome, by, reason);
        await db.SaveChangesAsync(ct);
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

        // docs/03 §7: only a completed stay can be reviewed, and "completed" is
        // the lifecycle's own judgement, made in the listing's time zone.
        if (booking.Status != BookingStatus.Completed)
        {
            return BadRequest(new
            {
                message = BookingLifecycle.IsCancelled(booking.Status)
                    ? "Chuyến đi đã huỷ không thể đánh giá."
                    : "Bạn có thể đánh giá sau khi trả phòng."
            });
        }

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

        var query = db.Bookings.Include(b => b.Payment).Include(b => b.Events).AsQueryable();
        if (includeListing)
        {
            query = query
                .Include(b => b.Listing!).ThenInclude(l => l.Images)
                .Include(b => b.Listing!).ThenInclude(l => l.Host);
        }

        return await query.FirstOrDefaultAsync(b =>
            b.Id == id && (user != null ? b.GuestUserId == user.Id : b.SessionId == sid), ct);
    }

    private static readonly System.Text.Json.JsonSerializerOptions LineJson = new(System.Text.Json.JsonSerializerDefaults.Web);

    private static string SerializeLines(IReadOnlyList<PriceLine> lines) =>
        System.Text.Json.JsonSerializer.Serialize(
            lines.Select(l => new PriceLineDto(l.Key, l.Label, l.Amount)), LineJson);

    private static IReadOnlyList<PriceLineDto> DeserializeLines(string json)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<PriceLineDto>>(json, LineJson) ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    private static BookingDto ToDto(Booking b)
    {
        // "Can review" and "can cancel" are now decisions the lifecycle makes,
        // so this no longer compares dates itself.
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
            b.GoodwillCredit,
            DeserializeLines(b.PriceLinesJson),
            Cancellation.Label(b.CancellationTier),
            Cancellation.Summary(b.CancellationTier),
            b.Status.ToString(),
            BookingLifecycle.Label(b.Status),
            BookingLifecycle.BadgeClass(b.Status),
            b.Payment?.Status.ToString() ?? "Pending",
            b.Payment?.Reference,
            b.Payment?.Method,
            b.Payment?.CardLast4,
            b.HasReview,
            b.Status == BookingStatus.Completed && !b.HasReview,
            BookingLifecycle.CanTransition(b.Status, BookingStatus.CancelledByGuest),
            b.HoldExpiresAt,
            b.RequestExpiresAt,
            b.Events.OrderBy(e => e.CreatedAt).ThenBy(e => e.Id).Select(e => new BookingEventDto(
                e.FromStatus?.ToString(),
                e.FromStatus is null ? "" : BookingLifecycle.Label(e.FromStatus.Value),
                e.ToStatus.ToString(),
                BookingLifecycle.Label(e.ToStatus),
                e.Actor, e.Reason, e.CreatedAt)).ToList(),
            b.GuestNote,
            b.Listing?.Host?.Name ?? "",
            b.CreatedAt);
    }
}
