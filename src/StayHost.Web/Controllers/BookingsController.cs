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
    PaymentGateway gateway, RiskWatch risk, WalletService wallet, PaymentCompletion completion,
    CouponService coupons)
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

        // docs/08 §5.2 — the restriction blocks the behaviour, not the account.
        if (Restrictions.Has(user.RestrictionMask, RestrictionKind.NoNewBookings))
            return StatusCode(403, new { message = Restrictions.Message(RestrictionKind.NoNewBookings) });

        // docs/08 §6 — a suspended account kept open for a dispute is open for
        // the dispute, not for booking holidays.
        if (user.IsSuspended)
            return StatusCode(403, new { message = "Tài khoản đang bị tạm khoá nên không đặt chỗ mới được." });

        // Quoting and booking go through the same builder so the guest is charged
        // exactly what the room page showed them (docs/00 §6.8).
        var quoteRequest = await catalog.BuildQuoteRequestAsync(
            listing.Id, req.CheckIn, req.CheckOut, party, ct, roomTypeId: req.RoomTypeId);

        // docs/01 ĐP-09 — a promo code first, evaluated against the stay's total
        // before any reduction. It is refused loudly rather than silently ignored:
        // a guest who typed a code and saw full price would think the site was
        // broken. Applied before balance so the code spares the balance.
        Coupons.Check couponCheck = new(false);
        if (!string.IsNullOrWhiteSpace(req.CouponCode))
        {
            var dry = Pricing.Quote(quoteRequest!);
            var gross = dry.Subtotal + dry.GuestServiceFee + dry.Tax;
            couponCheck = await coupons.EvaluateAsync(req.CouponCode, user.Id, gross, DateTime.UtcNow, ct: ct);
            if (!couponCheck.Ok)
                return BadRequest(new { message = couponCheck.Error });

            quoteRequest = quoteRequest! with
            {
                CouponAmount = couponCheck.Discount,
                CouponLabel = couponCheck.Label ?? "Mã giảm giá"
            };
        }

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
        var couponId = couponCheck.Ok
            ? (await db.Coupons.FirstAsync(c => c.Code == Coupons.Normalize(req.CouponCode), ct)).Id
            : (int?)null;

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
            CouponId = couponId,
            CouponDiscount = price.Coupon,
            // docs/07 §6 — kept so "the price I was shown" can be settled from
            // the record. Only a rate that could have been real is stored.
            DisplayCurrency = string.IsNullOrWhiteSpace(req.DisplayCurrency) ? null : req.DisplayCurrency.Trim(),
            DisplayRate = req.DisplayRate is > 0 ? req.DisplayRate : null,
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

        // docs/01 TC-09 — the redemption is written once the booking has an id.
        // A held booking counts against the campaign straight away; if it lapses
        // or is cancelled the redemption is released so the limit is not spent on
        // a stay that never happened.
        if (couponId is { } cid && price.Coupon > 0)
            coupons.Redeem(cid, user.Id, booking.Id, price.Coupon);

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
    /// Stands in for the bank's own 3-D Secure page (docs/07 §5). It is a
    /// separate request because in reality it is a separate site: the bank checks
    /// the code and takes the money there, and the platform finds out afterwards
    /// — or, when the guest's connection drops, does not find out at all until it
    /// asks. That is the case docs/07 §18 scenario 3 requires to work, and it
    /// cannot be exercised end to end unless the two halves can come apart.
    /// </summary>
    [HttpPost("{id:int}/bank-otp")]
    public async Task<ActionResult<CardAuthChallengeDto>> BankOtp(
        int id, [FromBody] BankOtpRequest req, CancellationToken ct)
    {
        var booking = await FindOwnedAsync(id, ct);
        if (booking is null) return NotFound();

        var pending = await db.CardAuthentications
            .FirstOrDefaultAsync(a => a.AttemptKey == req.AttemptKey && a.BookingId == booking.Id, ct);

        if (pending is null) return NotFound(new { message = "Không tìm thấy phiên xác thực." });

        if (pending.Outcome == AuthOutcome.Succeeded)
        {
            return Ok(new CardAuthChallengeDto(
                pending.AttemptKey, booking.HoldExpiresAt, pending.CodeAttempts, 0,
                CardAuth.OutcomeMessage(AuthOutcome.Succeeded, pending.CodeAttempts)));
        }

        if (!CardAuth.CanTryCodeAgain(pending.CodeAttempts))
        {
            return BadRequest(new
            {
                message = CardAuth.OutcomeMessage(AuthOutcome.WrongCode, pending.CodeAttempts),
                needsDifferentMethod = true
            });
        }

        pending.CodeAttempts++;

        var authorised = gateway.Authorise(
            pending.AttemptKey, pending.Amount, pending.Method, pending.CardLast4, req.Code);

        pending.Outcome = authorised.Ok ? AuthOutcome.Succeeded : AuthOutcome.WrongCode;
        if (authorised.Ok) pending.SettledAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        if (!authorised.Ok)
        {
            return BadRequest(new
            {
                message = CardAuth.OutcomeMessage(AuthOutcome.WrongCode, pending.CodeAttempts),
                retryable = CardAuth.CanTryCodeAgain(pending.CodeAttempts)
            });
        }

        // The money has moved. Whether the guest makes it back to StayHost is now
        // out of everyone's hands — which is exactly why the sweep exists.
        return Ok(new CardAuthChallengeDto(
            pending.AttemptKey, booking.HoldExpiresAt, pending.CodeAttempts, 0,
            CardAuth.OutcomeMessage(AuthOutcome.Succeeded, pending.CodeAttempts)));
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

        // docs/01 ĐP-09 — the promo code committed at the hold is part of the
        // shown price too, so the re-price carries it exactly as quoted. The
        // discount is re-derived from today's rules rather than trusting the
        // stored figure: a campaign ended between hold and payment should stop the
        // stale figure sailing through, and CouponDiscount was only ever a cache
        // of the number the guest saw.
        if (booking.CouponId is { } couponId)
        {
            var couponEntity = await db.Coupons.FindAsync([couponId], ct);
            var dry = Pricing.Quote(fresh!);
            var gross = dry.Subtotal + dry.GuestServiceFee + dry.Tax;
            var recheck = await coupons.EvaluateAsync(
                couponEntity?.Code, booking.GuestUserId!.Value, gross, DateTime.UtcNow,
                excludeBookingId: booking.Id, ct: ct);

            if (recheck.Ok)
                fresh = fresh! with { CouponAmount = recheck.Discount, CouponLabel = recheck.Label ?? "Mã giảm giá" };
        }

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

        // docs/07 §3 — the balance covering everything does not end the question
        // of where later money comes from. docs/06 §3.3 collects damages through
        // this stored method, so it has to exist before the booking does.
        if (PaymentMethods.NeedsFallbackMethod(charged, method, req?.CardLast4))
            return BadRequest(new { message = PaymentMethods.FallbackNotice(), needsFallbackMethod = true });

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

        // docs/07 §5 — cards that need the bank's OTP go there first. The row is
        // created before the idempotency claim so a guest coming back with their
        // code resumes this attempt rather than being told it already happened.
        if (gateway.NeedsAuthentication(method, req?.CardLast4))
        {
            var challenge = await AuthenticateAsync(booking, key, charged, method, req, ct);
            if (challenge is not null) return challenge;
        }

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

        // The key goes to the gateway too: docs/07 §5's self-check is only
        // possible if the platform can ask "what happened to this attempt".
        var attempt = gateway.Charge(charged, method, req?.CardLast4, key);

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

        // The same steps the self-check of docs/07 §5 runs when it discovers a
        // booking was paid after the guest's connection dropped.
        await completion.ConfirmAsync(
            booking, price, charged, partial, today, user.Id, req?.PaymentMethod, req?.CardLast4, ct);

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
        // A hold never paid for spent no balance, so only the promo code needs
        // handing back (docs/01 TC-09).
        await coupons.ReleaseAsync(booking.Id, ct);
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

    /// <summary>
    /// docs/07 §10 — the guest is told where each part of the money goes and how
    /// long it takes <em>before</em> confirming, not after. Cancellation decides
    /// how much; Refunds decides which pocket.
    /// </summary>
    private static RefundPreviewDto ToPreview(Booking b, Cancellation.Outcome o)
    {
        var split = Refunds.Allocate(SourcesOf(b), o.Amount, b.RefundedAmount);

        return new RefundPreviewDto(
            o.Amount, b.Total - o.Amount, b.Total, o.Explanation,
            o.RoomRefund, o.CleaningRefund, o.ServiceFeeRefund, o.TaxRefund, o.GoodwillCredit,
            split.ToCard, split.ToCredit, Refunds.TimingNotice(split));
    }

    /// <summary>
    /// What the stay was actually paid with.
    ///
    /// Balance is taken off the price before the card is charged (docs/07 §3),
    /// so <see cref="Booking.Total"/> is already net of it — subtracting it a
    /// second time here lost the guest half a million đồng off their own refund.
    /// The card carried the total; the balance is on top of it.
    /// </summary>
    private static Refunds.Sources SourcesOf(Booking b) => new(
        b.BalanceStatus == BalanceStatus.None ? b.Total : b.DepositPaid,
        b.CreditUsed);

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
                // Only what they actually did counts against the three a year.
                b.CancelledBy == CancelledBy.Guest &&
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
        // A cancellation the platform made is not the guest's. Recording it as
        // theirs put five bookings a guest never touched on their record and,
        // worse, spent the three service-fee refunds docs/03 §4 allows them in a
        // year — a cost for somebody else's suspension. The CancelledBy column
        // keeps the truth either way; this is only about which side of the
        // ledger the terminal status sits on.
        var to = by is CancelledBy.Host or CancelledBy.Platform or CancelledBy.ForceMajeure
            ? BookingStatus.CancelledByHost
            : BookingStatus.CancelledByGuest;
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
        // docs/01 TC-09 — a cancelled stay hands its promo code back to the
        // campaign so a limited run is not spent on a booking that did not happen.
        await coupons.ReleaseAsync(booking.Id, ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>A guest may review a stay once, after checkout.</summary>
    [HttpPost("{id:int}/review")]
    public async Task<IActionResult> Review(int id, [FromBody] SubmitReviewRequest req, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập để đánh giá." });

        // docs/08 §5.2 — a review ban stops the writing, not the staying.
        if (Restrictions.Has(user.RestrictionMask, RestrictionKind.NoReviews))
            return StatusCode(403, new { message = Restrictions.Message(RestrictionKind.NoReviews) });

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

    /// <summary>
    /// docs/07 §5 — the trip to the bank's OTP page.
    ///
    /// Returns the response to send when the guest still has authenticating to
    /// do, and null once they are through and the charge may go ahead.
    /// </summary>
    private async Task<ActionResult<BookingDto>?> AuthenticateAsync(
        Booking booking, string key, decimal amount, string method, PayBookingRequest? req, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var auth = await db.CardAuthentications.FirstOrDefaultAsync(a => a.AttemptKey == key, ct);

        if (auth is null)
        {
            auth = new CardAuthentication
            {
                BookingId = booking.Id, AttemptKey = key, Amount = amount,
                Method = method, CardLast4 = req?.CardLast4
            };
            db.CardAuthentications.Add(auth);
        }

        if (auth.Outcome == AuthOutcome.Succeeded) return null;

        // §5 — the guest may have authorised on the bank's page and only now got
        // back. The gateway is asked rather than the browser believed, so the
        // money that already moved is recognised instead of being taken twice.
        if (gateway.Lookup(key) == AuthOutcome.Succeeded)
        {
            auth.Outcome = AuthOutcome.Succeeded;
            auth.SettledAt ??= now;
            auth.ConfirmedWithGatewayAt = now;
            await db.SaveChangesAsync(ct);
            return null;
        }

        // §5.2 — the dates must not fall off the market while the guest is on
        // their bank's page. That would be our timer expiring, not them giving up.
        if (CardAuth.NeedsExtension(booking.HoldExpiresAt, now))
        {
            booking.HoldExpiresAt = CardAuth.ExtendedTo(booking.HoldExpiresAt, now);
            db.BookingEvents.Add(BookingLifecycle.Note(
                booking, "system", "Gia hạn giữ chỗ trong lúc khách xác thực với ngân hàng."));
        }

        // No code yet: this is the guest arriving, or coming back to a tab they
        // closed. Either way they are sent to the same place.
        if (string.IsNullOrWhiteSpace(req?.AuthenticationCode))
        {
            auth.Outcome = AuthOutcome.Pending;
            await db.SaveChangesAsync(ct);

            return Accepted(new CardAuthChallengeDto(
                key, booking.HoldExpiresAt, auth.CodeAttempts,
                CardAuth.MaxCodeAttempts - auth.CodeAttempts,
                CardAuth.OutcomeMessage(AuthOutcome.Pending, auth.CodeAttempts)));
        }

        if (!CardAuth.CanTryCodeAgain(auth.CodeAttempts))
        {
            await db.SaveChangesAsync(ct);
            return BadRequest(new
            {
                message = CardAuth.OutcomeMessage(AuthOutcome.WrongCode, auth.CodeAttempts),
                needsDifferentMethod = true
            });
        }

        // The code goes to the bank, and it is the bank that moves the money —
        // before this platform hears a word about it.
        var authorised = gateway.Authorise(key, amount, method, req.CardLast4, req.AuthenticationCode);

        if (!authorised.Ok)
        {
            auth.CodeAttempts++;
            auth.Outcome = AuthOutcome.WrongCode;
            await db.SaveChangesAsync(ct);

            return BadRequest(new
            {
                message = CardAuth.OutcomeMessage(AuthOutcome.WrongCode, auth.CodeAttempts),
                retryable = CardAuth.CanTryCodeAgain(auth.CodeAttempts),
                needsDifferentMethod = !CardAuth.CanTryCodeAgain(auth.CodeAttempts)
            });
        }

        auth.CodeAttempts++;
        auth.Outcome = AuthOutcome.Succeeded;
        auth.SettledAt = now;
        // The charge below is what the gateway will report; this row is confirmed
        // against it by the sweep rather than by anything the browser said.
        await db.SaveChangesAsync(ct);

        return null;
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
