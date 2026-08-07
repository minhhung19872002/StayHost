using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;

namespace StayHost.Web.Services;

/// <summary>
/// docs/07 §5 — "hệ thống phải tự kiểm tra lại kết quả với cổng thanh toán,
/// không tin vào việc khách quay về trang nào."
///
/// The case this exists for is docs/07 §18 scenario 3: the bank took the money
/// and then the guest's connection dropped, so nothing came back to say so. The
/// platform is holding a paid-for booking marked unpaid, and the only party who
/// knows is the gateway. So it asks.
/// </summary>
public class CardAuthSweeper(
    StayHostDbContext db, PaymentGateway gateway, PaymentCompletion completion,
    CatalogService catalog, ILogger<CardAuthSweeper> log)
{
    /// <summary>How long to leave a guest alone before assuming they are not coming back.</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromMinutes(2);

    public sealed record Result(int Checked, int Rescued, int Abandoned)
    {
        public bool Any => Checked > 0;
        public override string ToString() => $"{Checked} kiểm tra, {Rescued} cứu được, {Abandoned} bỏ dở";
    }

    public async Task<Result> SweepAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var cutoff = now - Grace;

        var open = await db.CardAuthentications
            // Unconfirmed is the whole test. A row the bank marked succeeded is
            // exactly the one that needs rescuing: the money moved and the
            // booking never heard.
            .Where(a => a.ConfirmedWithGatewayAt == null && a.StartedAt <= cutoff)
            .Include(a => a.Booking!).ThenInclude(b => b.Payment)
            .Include(a => a.Booking!).ThenInclude(b => b.Listing)
            .Take(100)
            .ToListAsync(ct);

        var rescued = 0;
        var abandoned = 0;

        foreach (var auth in open)
        {
            var booking = auth.Booking;
            if (booking is null) continue;

            // What the browser last told us, against what the gateway knows.
            var settled = CardAuth.Reconcile(auth.Outcome, gateway.Lookup(auth.AttemptKey));

            if (CardAuth.Disagreed(auth.Outcome, gateway.Lookup(auth.AttemptKey)))
            {
                log.LogWarning(
                    "Đơn {Reference}: trình duyệt báo {Browser}, cổng thanh toán báo {Gateway}.",
                    booking.Reference, auth.Outcome, settled);
            }

            auth.ConfirmedWithGatewayAt = now;

            if (settled == AuthOutcome.Succeeded && booking.Status == BookingStatus.PendingPayment)
            {
                await RescueAsync(auth, booking, ct);
                rescued++;
                continue;
            }

            // Nothing was charged. The booking stays as it is until its hold runs
            // out — the guest may still come back, and the lifecycle sweep is
            // what decides when they have not.
            if (settled != AuthOutcome.Succeeded)
            {
                auth.Outcome = AuthOutcome.Abandoned;
                auth.SettledAt ??= now;
                abandoned++;
            }
        }

        if (open.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            log.LogInformation("Đối chiếu xác thực thẻ: {Checked} kiểm tra, {Rescued} cứu được.",
                open.Count, rescued);
        }

        return new Result(open.Count, rescued, abandoned);
    }

    /// <summary>
    /// Money moved and the guest never heard. The booking is confirmed through
    /// exactly the path a successful payment takes, so the ledger, the messages
    /// and the notifications all come out the same.
    /// </summary>
    private async Task RescueAsync(CardAuthentication auth, Booking booking, CancellationToken ct)
    {
        var party = new PartySize(booking.Adults, booking.Children, booking.Infants, booking.Pets);
        var fresh = await catalog.BuildQuoteRequestAsync(
            booking.ListingId, booking.CheckIn, booking.CheckOut, party, ct, booking.Id, booking.RoomTypeId);

        if (fresh is null) return;

        if (booking.CreditUsed > 0)
            fresh = fresh with { PromotionAmount = booking.CreditUsed, PromotionLabel = "Số dư StayHost" };

        var price = Pricing.Quote(fresh);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var partial = auth.Amount < price.Total;

        auth.Outcome = AuthOutcome.Succeeded;
        auth.SettledAt ??= DateTime.UtcNow;

        // The attempt row is what stops the guest being charged twice if they do
        // come back, so it is settled here too.
        var claim = await db.PaymentAttempts.FirstOrDefaultAsync(a => a.Key == auth.AttemptKey, ct);
        if (claim is null)
        {
            claim = new PaymentAttempt
            {
                Key = auth.AttemptKey, BookingId = booking.Id, Amount = auth.Amount,
                Method = auth.Method, CardLast4 = auth.CardLast4
            };
            db.PaymentAttempts.Add(claim);
        }
        claim.Status = PaymentAttemptStatus.Succeeded;
        claim.CompletedAt = DateTime.UtcNow;

        await completion.ConfirmAsync(
            booking, price, auth.Amount, partial, today,
            booking.GuestUserId ?? 0, auth.Method, auth.CardLast4, ct);

        log.LogInformation(
            "Đơn {Reference} đã được xác nhận sau đối chiếu: tiền đã trừ nhưng khách không quay lại.",
            booking.Reference);
    }
}
