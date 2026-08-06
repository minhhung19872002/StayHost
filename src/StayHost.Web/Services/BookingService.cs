using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;

namespace StayHost.Web.Services;

/// <summary>
/// The booking rules that need the database: the nine availability checks of
/// docs/03 §2 and the clock transitions of docs/03 §3. Kept out of the
/// controllers so the background worker can run the same code.
/// </summary>
public class BookingService(StayHostDbContext db)
{
    /// <summary>Now, in the listing's own time zone (docs/03 §3).</summary>
    public static DateTime LocalNow(Listing listing, DateTime? utcNow = null)
    {
        var utc = utcNow ?? DateTime.UtcNow;
        try
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById(listing.TimeZoneId));
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // A bad zone id must not stop a booking; fall back to the platform's own.
            return TimeZoneInfo.ConvertTimeFromUtc(utc, VietnamTime);
        }
    }

    private static readonly TimeZoneInfo VietnamTime = ResolveVietnamTime();

    private static TimeZoneInfo ResolveVietnamTime()
    {
        foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { /* try the next spelling */ }
        }
        return TimeZoneInfo.CreateCustomTimeZone("StayHost-ICT", TimeSpan.FromHours(7), "ICT", "ICT");
    }

    /// <summary>
    /// Loads everything the nine checks need and runs them. Returns the first
    /// failure with its own message, exactly as the spec asks.
    /// </summary>
    public async Task<Availability.Result> CheckAsync(
        Listing listing, DateOnly checkIn, DateOnly checkOut, PartySize party, CancellationToken ct,
        int? ignoreBookingId = null)
    {
        // A window either side of the stay, wide enough for the turnover check.
        var from = checkIn.AddDays(-Math.Max(1, listing.TurnoverDays));
        var to = checkOut.AddDays(Math.Max(1, listing.TurnoverDays));

        var stays = await db.Bookings
            .Where(b => b.ListingId == listing.Id
                        && b.Id != (ignoreBookingId ?? 0)
                        && BookingLifecycle.BlocksDates.Contains(b.Status)
                        && b.CheckIn < to && from < b.CheckOut)
            .Select(b => new { b.CheckIn, b.CheckOut })
            .ToListAsync(ct);

        var blocks = await db.CalendarBlocks
            .Where(b => b.ListingId == listing.Id && b.From <= to && from <= b.To)
            .Select(b => new { b.From, b.To })
            .ToListAsync(ct);

        var minNights = await db.PriceRules
            .Where(r => r.ListingId == listing.Id && r.MinNights != null
                        && r.From <= checkOut && checkIn <= r.To)
            .Select(r => new { r.From, r.To, r.MinNights })
            .ToListAsync(ct);

        var byDay = new Dictionary<DateOnly, int>();
        foreach (var rule in minNights)
        {
            for (var d = rule.From; d <= rule.To; d = d.AddDays(1))
                byDay[d] = Math.Max(byDay.GetValueOrDefault(d), rule.MinNights ?? 0);
        }

        return Availability.Check(new Availability.Request
        {
            Listing = listing,
            CheckIn = checkIn,
            CheckOut = checkOut,
            Party = party,
            LocalNow = LocalNow(listing),
            Occupied =
            [
                .. stays.Select(s => new Availability.Occupied(s.CheckIn, s.CheckOut, false)),
                .. blocks.Select(b => new Availability.Occupied(b.From, b.To, true))
            ],
            MinNightsByDay = byDay
        });
    }

    /// <summary>
    /// The timers and clock transitions of docs/03 §3, run by the background
    /// worker. Returns a short summary of what moved.
    /// </summary>
    public async Task<SweepResult> SweepAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var result = new SweepResult();

        // Holds that ran out: the dates go back on the market.
        var staleHolds = await db.Bookings
            .Where(b => b.Status == BookingStatus.PendingPayment && b.HoldExpiresAt != null && b.HoldExpiresAt < now)
            .ToListAsync(ct);

        foreach (var b in staleHolds)
        {
            db.BookingEvents.Add(BookingLifecycle.Transition(
                b, BookingStatus.PaymentFailed, "system", "Hết 15 phút giữ chỗ mà chưa thanh toán xong."));
            result.HoldsExpired++;
        }

        // Requests the host never answered.
        var staleRequests = await db.Bookings
            .Where(b => b.Status == BookingStatus.PendingHostApproval
                        && b.RequestExpiresAt != null && b.RequestExpiresAt < now)
            .ToListAsync(ct);

        foreach (var b in staleRequests)
        {
            db.BookingEvents.Add(BookingLifecycle.Transition(
                b, BookingStatus.Expired, "system", "Chủ nhà không trả lời trong 24 giờ."));
            result.RequestsExpired++;
        }

        // Check-in and check-out roll over in the listing's own time zone, so
        // these two need the listing rather than a single server-side date.
        var movable = await db.Bookings
            .Include(b => b.Listing)
            .Where(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.InProgress)
            .ToListAsync(ct);

        foreach (var b in movable)
        {
            var localToday = DateOnly.FromDateTime(LocalNow(b.Listing!, now));

            if (b.Status == BookingStatus.Confirmed && localToday >= b.CheckIn)
            {
                db.BookingEvents.Add(BookingLifecycle.Transition(
                    b, BookingStatus.InProgress, "system", "Đã tới ngày nhận phòng."));
                result.StartedStays++;
            }

            if (b.Status == BookingStatus.InProgress && localToday >= b.CheckOut)
            {
                db.BookingEvents.Add(BookingLifecycle.Transition(
                    b, BookingStatus.Completed, "system", "Đã tới ngày trả phòng."));
                result.CompletedStays++;

                if (b.Payment is not null || await db.Payments.AnyAsync(p => p.BookingId == b.Id, ct))
                    result.PayoutsDue++;
            }
        }

        if (result.Any) await db.SaveChangesAsync(ct);
        return result;
    }

    public sealed class SweepResult
    {
        public int HoldsExpired { get; set; }
        public int RequestsExpired { get; set; }
        public int StartedStays { get; set; }
        public int CompletedStays { get; set; }
        public int PayoutsDue { get; set; }

        public bool Any => HoldsExpired + RequestsExpired + StartedStays + CompletedStays > 0;

        public override string ToString() =>
            $"{HoldsExpired} giữ chỗ hết hạn, {RequestsExpired} yêu cầu hết hạn, " +
            $"{StartedStays} bắt đầu lưu trú, {CompletedStays} hoàn tất";
    }
}

/// <summary>
/// Runs the sweep every minute. The two timers of docs/03 §2–§3 are minutes and
/// hours, so this is fine-grained enough without polling the database hard.
/// </summary>
public class BookingLifecycleWorker(IServiceProvider services, ILogger<BookingLifecycleWorker> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = services.CreateAsyncScope();
                var bookings = scope.ServiceProvider.GetRequiredService<BookingService>();
                var result = await bookings.SweepAsync(stoppingToken);
                if (result.Any) log.LogInformation("Vòng đời đơn: {Result}.", result);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // One bad sweep must not take the worker down for the rest of the process.
                log.LogError(ex, "Không chạy được vòng quét vòng đời đơn.");
            }
        }
    }
}
