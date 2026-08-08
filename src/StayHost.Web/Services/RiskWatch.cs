using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;

namespace StayHost.Web.Services;

/// <summary>
/// docs/01 AT-11 — watches for the patterns the spec names and raises a flag
/// for a person to look at. Nothing here blocks a booking: a false positive
/// would turn away a real guest, and there is always someone to ask.
/// </summary>
public class RiskWatch(StayHostDbContext db, ILogger<RiskWatch> log)
{
    /// <summary>
    /// Runs the checks for one account and records anything new. Returns the
    /// flags raised by this call, which is usually none.
    /// </summary>
    public async Task<IReadOnlyList<RiskFlag>> EvaluateAsync(
        int userId, Booking? booking, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return [];

        var now = DateTime.UtcNow;
        var monthAgo = now.AddDays(-30);
        var quarterAgo = now.AddDays(-90);
        var dayAgo = now.AddDays(-1);

        var cards = await db.Payments
            .Where(p => p.Booking!.GuestUserId == userId && p.CardLast4 != null && p.CreatedAt >= monthAgo)
            .Select(p => p.CardLast4)
            .Distinct()
            .CountAsync(ct);

        // Their own cancellations only: a stay the platform called off is not a
        // fraud signal about the person who was booked into it.
        var cancellations = await db.Bookings.CountAsync(b =>
            b.GuestUserId == userId && b.Status == BookingStatus.CancelledByGuest
            && b.CancelledBy == CancelledBy.Guest && b.CreatedAt >= quarterAgo, ct);

        var today = await db.Bookings.CountAsync(b =>
            b.GuestUserId == userId && b.CreatedAt >= dayAgo
            && BookingLifecycle.BlocksDates.Contains(b.Status), ct);

        var signals = RiskSignals.Check(new RiskSnapshot
        {
            AccountCreatedAt = user.CreatedAt,
            Now = now,
            BookingTotal = booking?.Total ?? 0m,
            DistinctCards = cards,
            RecentCancellations = cancellations,
            BookingsToday = today
        });

        if (signals.Count == 0) return [];

        // An open flag of the same kind is already on someone's desk; raising a
        // second one would only bury the first.
        var open = await db.RiskFlags
            .Where(f => f.UserId == userId && f.Status == RiskFlagStatus.Open)
            .Select(f => f.Kind)
            .ToListAsync(ct);

        var raised = new List<RiskFlag>();

        foreach (var signal in signals.Where(s => !open.Contains(s.Kind)))
        {
            var flag = new RiskFlag
            {
                UserId = userId,
                BookingId = booking?.Id,
                Kind = signal.Kind,
                Severity = signal.Severity,
                Summary = signal.Summary,
                Detail = signal.Detail
            };
            db.RiskFlags.Add(flag);
            raised.Add(flag);
        }

        if (raised.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            log.LogInformation("Raised {Count} risk flags for user {UserId}.", raised.Count, userId);
        }

        return raised;
    }
}
