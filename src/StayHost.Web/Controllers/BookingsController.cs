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
    CatalogService catalog, BookingService rules, ReviewService reviews, ThreadMessenger messenger,
    PaymentGateway gateway, RiskWatch risk, WalletService wallet)
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
        var check = await rules.CheckAsync(
            listing, req.CheckIn, req.CheckOut, party, ct, roomTypeId: req.RoomTypeId);
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
        var quoteRequest = await catalog.BuildQuoteRequestAsync(
            listing.Id, req.CheckIn, req.CheckOut, party, ct, roomTypeId: req.RoomTypeId);

        // Balance comes off the room charge, never off the fees or the tax: it
        // is money towards a stay, not a discount on what is owed elsewhere.
        var creditUsed = 0m;
        if (req.UseCredit)
        {
            var dry = Pricing.Quote(quoteRequest!);
            creditUsed = CreditRules.Spendable(
                await wallet.BalanceAsync(user.Id, ct), dry.RoomBeforeDiscount - dry.RoomDiscount);

            if (creditUsed > 0)
                quoteRequest = quoteRequest! with
                {
                    PromotionAmount = creditUsed,
                    PromotionLabel = "Số dư StayHost"
                };
        }

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
            RoomTypeId = req.RoomTypeId,
            CreditUsed = creditUsed,
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

        // docs/07 §7 — a retry of a request already answered gets that answer
        // back. Checked before the state guard, because by then the first attempt
        // has moved the booking on and the retry would be told the order is in
        // the wrong state — true, and useless to a client whose reply was lost.
        var replayKey = Payments.NamespaceKey(
            booking.Id, req?.IdempotencyKey, 0, req?.PaymentMethod ?? "card");

        if (req?.IdempotencyKey is not null)
        {
            var answered = await db.PaymentAttempts
                .FirstOrDefaultAsync(a => a.BookingId == booking.Id && a.Key == replayKey, ct);

            if (answered is { Status: PaymentAttemptStatus.Succeeded }) return Ok(ToDto(booking));
            if (answered is { Status: PaymentAttemptStatus.Failed })
                return BadRequest(new { message = answered.Message ?? Payments.Message(answered.Reason) });
        }

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
        var fresh = await catalog.BuildQuoteRequestAsync(
            booking.ListingId, booking.CheckIn, booking.CheckOut, party, ct, booking.Id, booking.RoomTypeId);

        // The balance the guest committed at the hold is part of the price they
        // were shown, so the re-price has to include it or every credit booking
        // would fail its own "did the price move" check.
        if (booking.CreditUsed > 0)
            fresh = fresh! with { PromotionAmount = booking.CreditUsed, PromotionLabel = "Số dư StayHost" };

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

        // docs/01 ĐP-06 — a deposit of at least half, with the rest taken 14 days
        // out. Too close to check-in for that and the whole amount is due now.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var partial = req?.PayDeposit == true && PartialPayment.IsAvailable(booking.CheckIn, today);
        var charged = partial ? PartialPayment.Deposit(price.Total, req?.DepositAmount) : price.Total;

        var method = req?.PaymentMethod ?? "card";

        // docs/07 §8 — a card tester gets five goes an hour on one booking.
        var since = DateTime.UtcNow - Payments.FailureWindow;
        var failures = await db.PaymentAttempts.CountAsync(
            a => a.BookingId == booking.Id && a.Status == PaymentAttemptStatus.Failed && a.CreatedAt >= since, ct);

        if (Payments.LockedOut(failures))
            return StatusCode(429, new { message = Payments.LockedOutMessage() });

        // docs/07 §7 — the same request twice must take the money once. The key
        // is claimed before the gateway is called and the unique index is what
        // decides the race, not the order two requests happen to arrive in.
        var key = req?.IdempotencyKey is not null
            ? replayKey
            : Payments.KeyFor(booking.Id, charged, method);

        var settled = await db.PaymentAttempts.FirstOrDefaultAsync(a => a.Key == key, ct);
        if (settled is not null)
        {
            if (settled.Status == PaymentAttemptStatus.Failed)
                return BadRequest(new { message = settled.Message ?? Payments.Message(settled.Reason) });
            return Ok(ToDto(booking));
        }

        var claim = new PaymentAttempt
        {
            Key = key, BookingId = booking.Id, Amount = charged,
            Method = method, CardLast4 = req?.CardLast4
        };
        db.PaymentAttempts.Add(claim);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Another request holds this key. It is charging, or has charged;
            // either way this one must not.
            db.ChangeTracker.Clear();
            return Conflict(new { message = "Yêu cầu thanh toán này đang được xử lý. Vui lòng đợi trong giây lát." });
        }

        var attempt = gateway.Charge(charged, method, req?.CardLast4);

        if (!attempt.Ok)
        {
            claim.Status = PaymentAttemptStatus.Failed;
            claim.Reason = attempt.Decline;
            claim.Message = Payments.Message(attempt.Decline);
            claim.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            return BadRequest(new
            {
                message = claim.Message,
                retryable = Payments.Retryable(attempt.Decline),
                needsDifferentMethod = Payments.NeedsDifferentMethod(attempt.Decline)
            });
        }

        claim.Status = PaymentAttemptStatus.Succeeded;
        claim.CompletedAt = DateTime.UtcNow;

        db.BookingEvents.Add(BookingLifecycle.Transition(
            booking, BookingStatus.Confirmed, $"guest:{user.Id}",
            partial
                ? $"Đã đặt cọc {charged:#,##0}₫ trên tổng {price.Total:#,##0}₫."
                : "Thanh toán thành công."));

        booking.DepositPaid = charged;
        booking.BalanceDue = price.Total - charged;
        booking.BalanceDueOn = partial ? PartialPayment.BalanceDueOn(booking.CheckIn, today) : null;
        booking.BalanceStatus = partial ? BalanceStatus.Scheduled : BalanceStatus.None;

        if (booking.Payment is not null)
        {
            booking.Payment.Status = PaymentStatus.Captured;
            booking.Payment.CapturedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(req?.PaymentMethod)) booking.Payment.Method = req.PaymentMethod;
            if (!string.IsNullOrWhiteSpace(req?.CardLast4)) booking.Payment.CardLast4 = req.CardLast4;
        }

        if (booking.CreditUsed > 0)
        {
            wallet.Add(user.Id, -booking.CreditUsed, CreditReason.Spent,
                $"Dùng cho đơn {booking.Reference}", booking.Id);
        }

        db.LedgerEntries.AddRange(
            Ledger.CaptureBooking(booking, price, DateTime.UtcNow, charged, booking.CreditUsed));
        await db.SaveChangesAsync(ct);

        var listing = booking.Listing!;

        // docs/01 TN-04 — the conversation carries the order's own milestones.
        await messenger.PostAsync(booking,
            $"Đơn {booking.Reference} đã được xác nhận: {booking.CheckIn:dd/MM} – {booking.CheckOut:dd/MM}, " +
            $"{booking.Nights} đêm, {booking.Guests} khách.", ct);

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

        // docs/01 AT-11 — a paid booking is the moment worth looking at the
        // account's pattern; the flag never stands in the guest's way.
        await risk.EvaluateAsync(user.Id, booking, ct);

        await db.SaveChangesAsync(ct);
        return Ok(ToDto(booking));
    }

    /// <summary>
    /// docs/01 ĐP-06 — the rest of a part-paid booking, either because its date
    /// came round or because the guest chose to settle up early.
    /// </summary>
    [HttpPost("{id:int}/balance")]
    public async Task<ActionResult<BookingDto>> PayBalance(int id, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var booking = await db.Bookings
            .Include(b => b.Payment).Include(b => b.Events)
            .Include(b => b.Listing!).ThenInclude(l => l.Images)
            .FirstOrDefaultAsync(b => b.Id == id && b.GuestUserId == user.Id, ct);

        if (booking is null) return NotFound();
        if (booking.BalanceDue <= 0 || booking.BalanceStatus is BalanceStatus.None or BalanceStatus.Paid)
            return BadRequest(new { message = "Đơn này không còn khoản nào phải trả." });

        var attempt = gateway.Charge(booking.BalanceDue, booking.Payment?.Method ?? "card", booking.Payment?.CardLast4);
        booking.BalanceAttempts++;
        booking.BalanceLastAttemptAt = DateTime.UtcNow;

        if (!attempt.Ok)
        {
            booking.BalanceFirstFailedAt ??= DateTime.UtcNow;
            booking.BalanceStatus = BalanceStatus.Retrying;
            await db.SaveChangesAsync(ct);
            return BadRequest(new { message = attempt.Reason });
        }

        db.LedgerEntries.AddRange(Ledger.CollectBalance(booking, booking.BalanceDue, DateTime.UtcNow));
        db.BookingEvents.Add(BookingLifecycle.Note(
            booking, $"guest:{user.Id}", $"Đã thu nốt {booking.BalanceDue:#,##0}₫."));

        booking.DepositPaid += booking.BalanceDue;
        booking.BalanceDue = 0;
        booking.BalanceStatus = BalanceStatus.Paid;
        booking.BalanceFirstFailedAt = null;

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

        await messenger.PostAsync(booking,
            $"Đơn {booking.Reference} đã được khách huỷ. Hoàn lại {outcome.Amount:#,##0}₫.", ct);

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

        // docs/01 ĐP-06 — a guest who only paid a deposit cannot be sent back
        // more than they handed over. What they are owed is set against what
        // they still owe first, and only the cash difference actually moves.
        var paid = booking.BalanceStatus == BalanceStatus.None ? booking.Total : booking.DepositPaid;
        var cashBack = Math.Min(outcome.Amount, paid);
        var netted = Math.Min(outcome.Amount - cashBack, booking.BalanceDue);

        if (cashBack > 0)
            db.LedgerEntries.AddRange(Ledger.SettleRefund(booking, cashBack, DateTime.UtcNow));
        if (netted > 0)
            db.LedgerEntries.AddRange(Ledger.NetRefundAgainstReceivable(booking, netted, DateTime.UtcNow));

        var leftover = booking.BalanceDue - netted;
        if (leftover > 0)
            db.LedgerEntries.AddRange(Ledger.WriteOffReceivable(booking, leftover, DateTime.UtcNow));

        // Balance spent on a booking comes back as balance, not as cash — and the
        // refund payable is cleared against that balance, not against the bank.
        if (booking.CreditUsed > 0 && booking.GuestUserId is { } creditOwner)
        {
            var creditBack = Math.Min(booking.CreditUsed, Math.Max(0m, outcome.Amount - cashBack - netted));
            db.LedgerEntries.AddRange(Ledger.SettleRefundAsCredit(booking, creditBack, DateTime.UtcNow));

            db.CreditEntries.Add(new CreditEntry
            {
                UserId = creditOwner,
                Amount = booking.CreditUsed,
                Reason = CreditReason.Returned,
                Memo = $"Hoàn số dư đơn {booking.Reference}",
                BookingId = booking.Id
            });
            booking.CreditUsed = 0;
        }

        booking.RefundedAmount = cashBack;
        if (booking.BalanceDue > 0)
        {
            booking.BalanceDue = 0;
            booking.BalanceStatus = BalanceStatus.Failed;
        }
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

        // docs/01 ĐG-09 — contact details and abuse are refused, not masked:
        // a review is permanent and public.
        var guard = ContentGuard.CheckReview(text);
        if (!guard.Ok) return BadRequest(new { message = guard.Message });

        if (DateTime.UtcNow > ReviewService.Deadline(booking))
            return BadRequest(new { message = "Đã quá 14 ngày kể từ ngày trả phòng." });

        double Clamp(double v) => Math.Clamp(v, 1, 5);

        var review = new Review
        {
            ListingId = booking.ListingId,
            BookingId = booking.Id,
            AuthorUserId = user.Id,
            // docs/01 TK-04 — a review is public, so it carries the name they
            // chose to be known by, not the one on their account.
            AuthorName = Profiles.DisplayNameOf(user.DisplayName, user.FullName),
            AuthorInitials = Profiles.InitialsOf(Profiles.DisplayNameOf(user.DisplayName, user.FullName)),
            When = Profiles.MonthLabel(DateTime.UtcNow),
            Text = text,
            PrivateNote = string.IsNullOrWhiteSpace(req.PrivateNote) ? null : req.PrivateNote.Trim(),
            EditableUntil = DateTime.UtcNow + ReviewService.EditWindow,
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

        await db.SaveChangesAsync(ct);

        // docs/03 §7 — blind both ways: this only becomes visible once the host
        // has written one too, or the 14-day window closes.
        var published = await reviews.TryPublishAsync(booking.Id, ct);
        if (published) await db.SaveChangesAsync(ct);

        return Ok(new
        {
            published,
            message = published
                ? "Đánh giá của bạn và của chủ nhà đã được công khai."
                : "Đã gửi. Đánh giá sẽ hiện khi chủ nhà cũng gửi, hoặc sau 14 ngày."
        });
    }

    /// <summary>
    /// docs/01 ĐG-08 — the writer may correct a review inside 48 hours, and only
    /// while the other side has not written theirs. Once it is public, it stands.
    /// </summary>
    [HttpPut("{id:int}/review")]
    public async Task<IActionResult> EditReview(int id, [FromBody] SubmitReviewRequest req, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập để sửa đánh giá." });

        var review = await db.Reviews
            .FirstOrDefaultAsync(r => r.BookingId == id && r.AuthorUserId == user.Id, ct);
        if (review is null) return NotFound();

        if (review.PublishedAt is not null)
            return BadRequest(new { message = "Đánh giá đã công khai nên không sửa được nữa." });

        if (review.EditableUntil is { } until && DateTime.UtcNow > until)
            return BadRequest(new { message = "Đã quá 48 giờ kể từ khi gửi nên không sửa được nữa." });

        var text = (req.Text ?? "").Trim();
        if (text.Length < 10) return BadRequest(new { message = "Nội dung đánh giá cần tối thiểu 10 ký tự." });

        var guard = ContentGuard.CheckReview(text);
        if (!guard.Ok) return BadRequest(new { message = guard.Message });

        double Clamp(double v) => Math.Clamp(v, 1, 5);

        review.Text = text;
        review.Rating = Clamp(req.Rating);
        review.Cleanliness = Clamp(req.Cleanliness);
        review.Accuracy = Clamp(req.Accuracy);
        review.CheckIn = Clamp(req.CheckIn);
        review.Communication = Clamp(req.Communication);
        review.Location = Clamp(req.Location);
        review.Value = Clamp(req.Value);
        review.PrivateNote = string.IsNullOrWhiteSpace(req.PrivateNote) ? null : req.PrivateNote.Trim();

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
            b.CreatedAt,
            b.DepositPaid,
            b.BalanceDue,
            b.BalanceDueOn,
            b.BalanceStatus.ToString(),
            PartialPayment.Label(b.BalanceStatus),
            BuildGuide(b));
    }

    /// <summary>
    /// docs/01 CĐ-03 and CĐ-04 — the arrival guide, filtered by docs/03 §10
    /// before it leaves the server. Withheld fields are absent rather than
    /// blank, so a door code the guest may not see yet is never in the response
    /// for them to read out of it.
    /// </summary>
    private static CheckInGuideDto? BuildGuide(Booking b)
    {
        if (b.Listing is not { } l || !CheckInGuide.CanSeeGuide(b.Status)) return null;

        var localNow = BookingService.LocalNow(l);
        var codeReady = CheckInGuide.CanSeeDoorCode(b.Status, b.CheckIn, l.CheckInFrom, localNow);
        var hasCode = !string.IsNullOrWhiteSpace(l.DoorCode);

        return new CheckInGuideDto(
            CheckInGuide.WindowLabel(l.CheckInFrom, l.CheckInTo, l.CheckOutBefore),
            CheckInGuide.MethodLabel(l.CheckInMethod),
            l.AddressLine,
            l.Directions,
            l.WifiName,
            l.WifiPassword,
            CheckInGuide.Lines(l.ApplianceNotes),
            l.HostPhone,
            hasCode && codeReady ? l.DoorCode : null,
            hasCode,
            hasCode && !codeReady ? CheckInGuide.DoorCodeWaitNote(b.CheckIn, l.CheckInFrom) : null);
    }
}
