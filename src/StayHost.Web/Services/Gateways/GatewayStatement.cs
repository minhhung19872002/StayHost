using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;

namespace StayHost.Web.Services.Gateways;

/// <summary>
/// docs/07 §7 — the gateway's half of the daily reconciliation: "so danh sách
/// giao dịch của sàn với danh sách của cổng thanh toán."
///
/// The reconciliation screen used to read <c>gateway_charges</c> for both halves.
/// That table is written by this platform, so the comparison was of one of our
/// records against another of our records — it balanced every day and proved
/// nothing. With a licensed gateway holding the money, "their list" has to come
/// from them.
///
/// So every session the platform believes settled that day is put back to the
/// gateway that took it, one <c>querydr</c> each. It is a slow way to build a
/// statement and it is the only honest one available: none of the three
/// publishes a bulk end-of-day file over an API.
/// </summary>
public class GatewayStatement(
    StayHostDbContext db, PspRouter router, PaymentGateway standIn, ILogger<GatewayStatement> log)
{
    /// <summary>
    /// What the gateways say they took on that day, keyed by the same attempt
    /// key the platform's own list uses so the two can be compared at all.
    /// </summary>
    public async Task<IReadOnlyList<Reconciliation.Record>> ForAsync(DateOnly day, CancellationToken ct)
    {
        var from = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = from.AddDays(1);

        var sessions = await db.PaymentSessions
            .Where(s => s.Status == PaymentSessionStatus.Paid
                        && s.CompletedAt >= from && s.CompletedAt < to)
            .OrderBy(s => s.Id)
            .Take(500)
            .ToListAsync(ct);

        // Nothing went through a licensed gateway that day — a demo build, or a
        // day before one was switched on. The stand-in's book is then the only
        // other list there is, and comparing it is at least not misleading.
        if (sessions.Count == 0) return await standIn.StatementAsync(day, ct);

        var theirs = new List<Reconciliation.Record>(sessions.Count);
        var unreachable = 0;

        foreach (var session in sessions)
        {
            var provider = router.ByKey(session.Provider);

            if (provider is null)
            {
                unreachable++;
                continue;
            }

            var verdict = await provider.QueryAsync(session.OrderRef, session.CreatedAt, ct);

            // A gateway that cannot be reached is not a gateway saying "no". Left
            // out of their list it would show as money the platform invented, and
            // an operator would spend the morning on a network blip — so it is
            // counted as agreeing and said out loud in the log instead.
            if (verdict.Status == PaymentSessionStatus.Pending)
            {
                unreachable++;
                theirs.Add(new Reconciliation.Record(session.AttemptKey, session.Amount));
                continue;
            }

            if (verdict.Status != PaymentSessionStatus.Paid) continue;

            // Their number, not ours. A gateway reporting a different amount is
            // exactly the discrepancy this report exists to surface.
            theirs.Add(new Reconciliation.Record(
                session.AttemptKey, verdict.Amount > 0 ? verdict.Amount : session.Amount));
        }

        if (unreachable > 0)
        {
            log.LogWarning(
                "Đối soát {Day}: {Count}/{Total} giao dịch không hỏi lại được cổng, " +
                "tạm coi là khớp.", day, unreachable, sessions.Count);
        }

        return theirs;
    }
}
