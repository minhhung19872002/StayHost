using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;

namespace StayHost.Web.Services.Gateways;

/// <summary>
/// docs/07 §13 — the trip out to a licensed gateway and back, for a stay.
///
/// Three things can tell the platform a payment happened: the guest's browser
/// coming back, the gateway's IPN, and the platform asking. All three land here,
/// and the first one to arrive with a real signature wins — the other two find
/// the session already settled and do nothing. That is what makes it safe for
/// them to race, and docs/07 §7 says a double charge is the worst fault in the
/// module.
/// </summary>
public class PspCheckout(
    StayHostDbContext db, PspRouter router, PaymentGateway gateway,
    PaymentCompletion completion, DataSecrets secrets, ILogger<PspCheckout> log)
{
    public sealed record Started(bool Ok, string? PayUrl = null, string? OrderRef = null, string? Error = null);

    /// <summary>
    /// Opens an order at the gateway and hands back the address to send the
    /// guest to. Nothing is charged here and nothing is written to the ledger:
    /// the money moves on somebody else's page.
    /// </summary>
    public async Task<Started> StartAsync(
        Booking booking, string method, decimal amount, bool partial,
        string attemptKey, string clientIp, CancellationToken ct,
        bool saveCard = false, string? cardToken = null)
    {
        var provider = router.For(method);
        if (provider is null) return new Started(false, Error: "Cách thanh toán này chưa nối cổng nào.");

        var now = DateTime.UtcNow;

        // docs/07 §7 — a guest who double-clicks, or whose browser retried the
        // request, must go back to the order that already exists rather than open
        // a second one at the gateway.
        var open = await db.PaymentSessions
            .Where(s => s.AttemptKey == attemptKey && s.Status == PaymentSessionStatus.Pending)
            .OrderByDescending(s => s.Id)
            .FirstOrDefaultAsync(ct);

        if (open is { PayUrl.Length: > 0 } && open.CreatedAt.Add(PaymentSession.Window) > now)
            return new Started(true, open.PayUrl, open.OrderRef);

        var sequence = await db.PaymentSessions.CountAsync(s => s.BookingId == booking.Id, ct);
        var orderRef = Psp.OrderRef(booking.Id, now, sequence);

        var session = new PaymentSession
        {
            OrderRef = orderRef,
            AttemptKey = attemptKey,
            BookingId = booking.Id,
            Provider = provider.Key,
            Method = method,
            Amount = amount,
            Partial = partial
        };

        db.PaymentSessions.Add(session);
        await db.SaveChangesAsync(ct);

        var start = await provider.StartAsync(
            new PspOrder(orderRef, amount, $"StayHost {booking.Reference}", method, clientIp,
                SaveCard: saveCard,
                // The gateway keeps its tokens per user of ours, so it needs a
                // handle on the guest that survives them saving a second card.
                UserRef: booking.GuestUserId is { } id ? Psp.AppUserRef(id) : null,
                Token: cardToken), ct);

        if (!start.Ok || start.PayUrl is null)
        {
            session.Status = PaymentSessionStatus.Failed;
            session.ResponseCode = "start";
            session.CompletedAt = now;
            session.SettledBy = "start";
            await db.SaveChangesAsync(ct);
            return new Started(false, Error: start.Error ?? Payments.Message(DeclineReason.GatewayError));
        }

        session.PayUrl = start.PayUrl;

        // The dates stay off the market for as long as the gateway's own window,
        // the same way docs/07 §2.3 holds them while a transfer is in flight.
        // Without this the 15-minute checkout hold, most of which the guest has
        // already spent reading the page, could lapse while they are typing a
        // card number somewhere else.
        booking.HoldExpiresAt = now.Add(PaymentSession.Window);

        if (booking.Payment is not null)
        {
            booking.Payment.Method = method;
            booking.Payment.Status = PaymentStatus.Pending;
        }

        db.BookingEvents.Add(BookingLifecycle.Note(
            booking, "system", $"Chuyển sang cổng {provider.Key.ToUpperInvariant()} · mã {orderRef}."));

        await db.SaveChangesAsync(ct);

        return new Started(true, start.PayUrl, orderRef);
    }

    public Task<PaymentSession?> FindAsync(string? orderRef, CancellationToken ct)
    {
        var raw = (orderRef ?? "").Trim();
        if (raw.Length == 0) return Task.FromResult<PaymentSession?>(null);

        // ZaloPay hands back the id it was given, date prefix and all.
        var underscore = raw.IndexOf('_');
        if (underscore >= 0) raw = raw[(underscore + 1)..];

        return db.PaymentSessions
            .Include(s => s.Booking!).ThenInclude(b => b.Payment)
            .Include(s => s.Booking!).ThenInclude(b => b.Listing)
            .Include(s => s.Booking!).ThenInclude(b => b.Events)
            .FirstOrDefaultAsync(s => s.OrderRef == raw, ct);
    }

    /// <summary>
    /// Records what a gateway said and, if it said the money moved, confirms the
    /// booking through the same path an ordinary payment takes.
    ///
    /// Safe to call more than once for the same session: the first settlement
    /// wins and the rest are told what it decided.
    /// </summary>
    public async Task<PaymentSessionStatus> SettleAsync(
        PaymentSession session, PspVerdict verdict, string settledBy, CancellationToken ct)
    {
        if (session.Status != PaymentSessionStatus.Pending) return session.Status;

        // An unsigned payload is not news about the payment, whichever way it
        // leans. Anyone can post to the callback routes, so this is the line that
        // stops a stranger writing off somebody else's booking.
        if (verdict.Code == PspVerdict.Signature)
        {
            log.LogWarning("Bỏ qua callback không đúng chữ ký cho đơn {Ref}.", session.OrderRef);
            return PaymentSessionStatus.Pending;
        }

        if (verdict.Status == PaymentSessionStatus.Pending) return PaymentSessionStatus.Pending;

        var now = DateTime.UtcNow;
        session.SettledBy = settledBy;
        session.ResponseCode = verdict.Code;
        session.CompletedAt = now;
        if (!string.IsNullOrWhiteSpace(verdict.TxnId)) session.ProviderTxnId = verdict.TxnId;
        if (!string.IsNullOrWhiteSpace(verdict.PaidAt)) session.ProviderPaidAt = verdict.PaidAt;

        if (verdict.Status != PaymentSessionStatus.Paid)
        {
            session.Status = verdict.Status;
            await FailAttemptAsync(session, verdict.Decline, ct);
            await db.SaveChangesAsync(ct);

            log.LogInformation("Cổng {Provider} trả lời {Status} cho đơn {Ref} (mã {Code}).",
                session.Provider, verdict.Status, session.OrderRef, verdict.Code);

            return session.Status;
        }

        // docs/07 §7 — a gateway reporting a different amount than the booking is
        // for is not a payment to act on. Confirming a stay on it would be the
        // exact fault the module is built around, so it is refused and shouted
        // about rather than quietly accepted.
        if (verdict.Amount > 0 && !Psp.AmountMatches(session.Amount, verdict.Amount))
        {
            session.Status = PaymentSessionStatus.Failed;
            session.ResponseCode = "amount";
            await FailAttemptAsync(session, DeclineReason.GatewayError, ct);
            await db.SaveChangesAsync(ct);

            log.LogError(
                "Cổng {Provider} báo đã thu {Reported} cho đơn {Ref} nhưng đơn là {Expected}. Không xác nhận.",
                session.Provider, verdict.Amount, session.OrderRef, session.Amount);

            return PaymentSessionStatus.Failed;
        }

        session.Status = PaymentSessionStatus.Paid;

        // The gateway's own book, which is one half of the daily reconciliation of
        // docs/07 §7. Written through PaymentGateway so business code still never
        // touches that table directly.
        gateway.RecordExternalCharge(session.AttemptKey, session.Amount, session.Method);

        var claim = await db.PaymentAttempts.FirstOrDefaultAsync(a => a.Key == session.AttemptKey, ct);
        if (claim is null)
        {
            claim = new PaymentAttempt
            {
                Key = session.AttemptKey, BookingId = session.BookingId,
                Amount = session.Amount, Method = session.Method
            };
            db.PaymentAttempts.Add(claim);
        }
        claim.Status = PaymentAttemptStatus.Succeeded;
        claim.CompletedAt = now;
        claim.Message = null;

        await db.SaveChangesAsync(ct);

        var booking = session.Booking ?? await db.Bookings
            .Include(b => b.Payment).Include(b => b.Events).Include(b => b.Listing)
            .FirstOrDefaultAsync(b => b.Id == session.BookingId, ct);

        if (booking is null) return PaymentSessionStatus.Paid;

        // Already confirmed — by the IPN, or by the sweep, or by the guest coming
        // back before either. Nothing left to do, and doing it twice would post
        // the ledger twice.
        if (booking.Status != BookingStatus.PendingPayment) return PaymentSessionStatus.Paid;

        var price = await completion.QuoteFromRecordAsync(booking, ct);
        if (price is null)
        {
            log.LogError("Đơn {Ref} đã thu tiền qua {Provider} nhưng không dựng lại được giá.",
                booking.Reference, session.Provider);
            return PaymentSessionStatus.Paid;
        }

        // docs/07 §4 — VNPay's token API is the only thing that ever tells this
        // platform four digits of a card, so when it does, they are kept: §10's
        // closed-card refund branch and §4's expiry reminder both read that
        // column and have had nothing to read since the card form went away.
        await RememberCardAsync(session, verdict, booking, ct);

        await completion.ConfirmAsync(
            booking, price, session.Amount, session.Partial, DateOnly.FromDateTime(now),
            booking.GuestUserId ?? 0, session.Method, verdict.CardLast4, ct);

        log.LogInformation("Đơn {Ref} đã xác nhận sau khi {Provider} thu {Amount} ({By}).",
            booking.Reference, session.Provider, session.Amount, settledBy);

        return PaymentSessionStatus.Paid;
    }

    /// <summary>
    /// docs/07 §4 — keeps the card the guest asked to keep.
    ///
    /// The number is never here: what arrives is four digits VNPay chose to show
    /// and a token only they can use. Saving the same card twice is a no-op, so a
    /// guest who ticks the box on every booking ends up with one row, not ten.
    /// </summary>
    private async Task RememberCardAsync(
        PaymentSession session, PspVerdict verdict, Booking booking, CancellationToken ct)
    {
        if (booking.GuestUserId is not { } userId) return;
        if (verdict.CardToken is not { Length: > 0 } || verdict.CardLast4 is not { Length: 4 }) return;

        var sealedToken = secrets.Seal(DataSecrets.CardToken, verdict.CardToken);

        // No key, no storing it — the same rule the payout account follows. The
        // last four digits are not a secret and are kept either way.
        if (sealedToken is null)
        {
            log.LogWarning("Chưa có khoá mã hoá nên không lưu được thẻ của người dùng {UserId}.", userId);
            return;
        }

        var brand = Psp.VnPayIsDomesticCard(verdict.CardType) ? CardBrand.Napas : CardBrand.Unknown;

        var already = await db.SavedCards.FirstOrDefaultAsync(
            c => c.UserId == userId && c.Last4 == verdict.CardLast4
                 && c.Provider == session.Provider, ct);

        if (already is not null)
        {
            // The token can be reissued for the same card; the newest one wins.
            already.GatewayTokenSealed = sealedToken;
            already.Brand = brand;
            await db.SaveChangesAsync(ct);
            return;
        }

        var cards = await db.SavedCards.Where(c => c.UserId == userId).ToListAsync(ct);

        var card = new SavedCard
        {
            UserId = userId,
            Brand = brand,
            Last4 = verdict.CardLast4,
            // VNPay's token API returns no expiry date at all — the card's
            // expiry is theirs to know, and SavedCards.ExpiryKnown says so
            // rather than this pretending to a month it was never told.
            ExpiryMonth = 0,
            ExpiryYear = 0,
            Provider = session.Provider,
            GatewayTokenSealed = sealedToken
        };

        db.SavedCards.Add(card);
        await db.SaveChangesAsync(ct);

        // The first card saved is the default, exactly as the typed-in path does.
        cards.Add(card);
        SavedCards.Reseat(cards);
        await db.SaveChangesAsync(ct);

        log.LogInformation("Đã lưu thẻ •••• {Last4} của người dùng {UserId} tại {Provider}.",
            verdict.CardLast4, userId, session.Provider);
    }

    /// <summary>
    /// A refusal is written down, because docs/07 §8 counts refusals: five in an
    /// hour on one booking and the guest is stopped. A guest pressing "huỷ" on
    /// the gateway's own page is not one of those — they did not fail to pay,
    /// they decided not to — so nothing is recorded and the dates stay held for
    /// the second try.
    /// </summary>
    private async Task FailAttemptAsync(PaymentSession session, DeclineReason reason, CancellationToken ct)
    {
        if (session.Status == PaymentSessionStatus.Cancelled) return;

        var claim = await db.PaymentAttempts.FirstOrDefaultAsync(a => a.Key == session.AttemptKey, ct);

        if (claim is null)
        {
            claim = new PaymentAttempt
            {
                Key = session.AttemptKey, BookingId = session.BookingId,
                Amount = session.Amount, Method = session.Method
            };
            db.PaymentAttempts.Add(claim);
        }
        else if (claim.Status == PaymentAttemptStatus.Succeeded) return;

        claim.Status = PaymentAttemptStatus.Failed;
        claim.Reason = reason;
        claim.Message = Payments.Message(reason);
        claim.CompletedAt = DateTime.UtcNow;
    }
}
