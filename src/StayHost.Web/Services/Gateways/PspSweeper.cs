using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;

namespace StayHost.Web.Services.Gateways;

/// <summary>
/// docs/07 §5, TC-P-05 — "hệ thống phải tự kiểm tra lại kết quả với cổng thanh
/// toán, không tin vào việc khách quay về trang nào."
///
/// <see cref="CardAuthSweeper"/> does this for the stand-in gateway. This does it
/// for the real ones, and here it is not a nicety: a laptop's return URL is
/// reachable by the browser but a gateway's IPN never arrives at localhost, so on
/// a development machine this sweep is the <em>only</em> thing that settles a
/// payment whose guest closed the tab. In production it is the safety net under
/// a missed webhook.
///
/// It runs before the lifecycle sweep for the same reason the card one does: that
/// sweep expires unpaid holds, and a booking that was paid has to be recognised
/// before it is failed.
/// </summary>
public class PspSweeper(
    StayHostDbContext db, PspRouter router, PspCheckout checkout, ILogger<PspSweeper> log)
{
    /// <summary>How long to leave a guest on the gateway's page before asking about them.</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromMinutes(1);

    public sealed record Result(int Checked, int Settled, int Expired)
    {
        public bool Any => Checked > 0;
        public override string ToString() => $"{Checked} kiểm tra, {Settled} chốt được, {Expired} quá hạn";
    }

    public async Task<Result> SweepAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var cutoff = now - Grace;

        var open = await db.PaymentSessions
            .Where(s => s.Status == PaymentSessionStatus.Pending && s.CreatedAt <= cutoff)
            .Include(s => s.Booking!).ThenInclude(b => b.Payment)
            .Include(s => s.Booking!).ThenInclude(b => b.Listing)
            .Include(s => s.Booking!).ThenInclude(b => b.Events)
            .OrderBy(s => s.Id)
            .Take(50)
            .ToListAsync(ct);

        var settled = 0;
        var expired = 0;

        foreach (var session in open)
        {
            var provider = router.ByKey(session.Provider);

            // The gateway was switched off since the guest left for it. Nothing
            // can be asked, so the session is left alone rather than guessed at —
            // the booking's own hold is what ends it.
            if (provider is null) continue;

            var verdict = await provider.QueryAsync(session.OrderRef, session.CreatedAt, ct);

            if (verdict.Status != PaymentSessionStatus.Pending)
            {
                var outcome = await checkout.SettleAsync(session, verdict, "sweep", ct);
                if (outcome == PaymentSessionStatus.Paid) settled++;
                continue;
            }

            // Still unknown after the gateway's own window has closed. Nobody is
            // coming back, and leaving it Pending would mean asking about it for
            // ever.
            if (session.CreatedAt.Add(PaymentSession.Window) < now)
            {
                session.Status = PaymentSessionStatus.Expired;
                session.CompletedAt = now;
                session.SettledBy = "sweep";
                expired++;
            }
        }

        if (open.Count > 0)
        {
            await db.SaveChangesAsync(ct);

            if (settled > 0 || expired > 0)
                log.LogInformation("Đối chiếu cổng thanh toán: {Checked} kiểm tra, {Settled} chốt, {Expired} quá hạn.",
                    open.Count, settled, expired);
        }

        return new Result(open.Count, settled, expired);
    }
}
