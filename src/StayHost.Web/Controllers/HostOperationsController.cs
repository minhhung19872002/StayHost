using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Contracts;
using StayHost.Web.Infrastructure;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

/// <summary>
/// The running-a-listing half of the host console: the daily board, calendar
/// rules, bulk day edits, income export and payout settings.
/// </summary>
[ApiController]
[Route("api/host")]
public class HostOperationsController(
    StayHostDbContext db, AuthService auth, HostAccess access, ShieldService shield,
    BadgeService badges) : ControllerBase
{
    /// <summary>
    /// A host walking away from a confirmed booking. docs/03 §4 gives the guest
    /// everything back plus a credit, and docs/06 §2.1 K1 opens a StayShield
    /// case on their behalf when it happens inside 30 days of check-in — the
    /// guest should not have to notice and file it themselves.
    /// </summary>
    [HttpPost("bookings/{id:int}/cancel")]
    public async Task<IActionResult> CancelBooking(
        int id, [FromBody] HostCancelRequest? req, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var booking = await db.Bookings
            .Include(b => b.Events)
            .Include(b => b.Payment)
            .Include(b => b.Listing)
            .FirstOrDefaultAsync(b => b.Id == id, ct);
        if (booking is null) return NotFound();

        if (await access.ListingAsync(user, booking.ListingId, CoHostScope.Bookings, ct) is null)
            return this.Denied("Bạn không có quyền với đơn này.");

        if (!BookingLifecycle.CanTransition(booking.Status, BookingStatus.CancelledByHost))
            return BadRequest(new
            {
                message = $"Đơn đang ở trạng thái \"{BookingLifecycle.Label(booking.Status)}\" nên không huỷ được."
            });

        var outcome = Cancellation.Refund(new Cancellation.Context
        {
            Booking = booking,
            Now = DateTime.UtcNow,
            By = CancelledBy.Host,
            ServiceFeeRefundsUsed = 0
        });

        BookingsController.PostCancellation(
            db, booking, outcome, CancelledBy.Host,
            (req?.Reason ?? "Chủ nhà huỷ đơn").Trim());

        await db.SaveChangesAsync(ct);

        await shield.OpenHostCancellationAsync(booking, ct);

        return Ok(new
        {
            refunded = outcome.Amount,
            credit = outcome.GoodwillCredit,
            message = "Đã huỷ đơn và hoàn tiền cho khách."
        });
    }

    private async Task<(User? User, HostProfile? Profile)> ResolveAsync(CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return (null, null);
        return (user, await db.Hosts.FirstOrDefaultAsync(h => h.UserId == user.Id, ct));
    }

    /* ------------------------------------------------------------- QL-01 */

    /// <summary>
    /// docs/01 QL-01 — what needs doing today: guests arriving, guests in the
    /// house, guests leaving, and requests still waiting on an answer.
    /// </summary>
    [HttpGet("today")]
    public async Task<ActionResult<TodayBoardDto>> Today(CancellationToken ct)
    {
        var (user, profile) = await ResolveAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });
        // A co-host with the bookings scope sees the same board for the places
        // they help run (docs/01 QL-19).
        var listingIds = await access.ListingIdsAsync(user, CoHostScope.Bookings, ct);
        if (listingIds.Count == 0) return Ok(new TodayBoardDto([], [], [], [], 0));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizon = today.AddDays(7);

        var bookings = await db.Bookings
            .Where(b => listingIds.Contains(b.ListingId) && b.CheckOut >= today && b.CheckIn <= horizon)
            .Include(b => b.Listing)
            .Include(b => b.GuestUser)
            .OrderBy(b => b.CheckIn)
            .ToListAsync(ct);

        TodayItemDto Row(Booking b, string what) => new(
            b.Id, b.Reference, b.Listing?.Title ?? "",
            b.GuestUser?.FullName ?? b.GuestName ?? "Khách",
            b.CheckIn, b.CheckOut, b.Nights, b.Guests, what,
            BookingLifecycle.Label(b.Status), BookingLifecycle.BadgeClass(b.Status));

        var live = bookings.Where(b => BookingLifecycle.BlocksDates.Contains(b.Status)).ToList();

        var arriving = live
            .Where(b => b.CheckIn >= today && b.CheckIn <= horizon)
            .Select(b => Row(b, b.CheckIn == today ? "Nhận phòng hôm nay" : $"Nhận phòng {b.CheckIn:dd/MM}"))
            .ToList();

        var inHouse = live
            .Where(b => b.CheckIn <= today && today < b.CheckOut)
            .Select(b => Row(b, "Đang lưu trú"))
            .ToList();

        var leaving = live
            .Where(b => b.CheckOut >= today && b.CheckOut <= horizon)
            .Select(b => Row(b, b.CheckOut == today ? "Trả phòng hôm nay" : $"Trả phòng {b.CheckOut:dd/MM}"))
            .ToList();

        var waiting = await db.Bookings
            .Where(b => listingIds.Contains(b.ListingId) && b.Status == BookingStatus.PendingHostApproval)
            .Include(b => b.Listing).Include(b => b.GuestUser)
            .OrderBy(b => b.RequestExpiresAt)
            .ToListAsync(ct);

        return Ok(new TodayBoardDto(
            arriving, inHouse, leaving,
            waiting.Select(b => Row(b, "Cần trả lời trong 24 giờ")).ToList(),
            waiting.Count));
    }

    /* ------------------------------------------------------------- QL-04 */

    /// <summary>
    /// docs/01 QL-04 — every listing's availability side by side for one date
    /// range, so a host with several places can see the whole month at once.
    /// </summary>
    [HttpGet("calendar")]
    public async Task<ActionResult<MultiCalendarDto>> MultiCalendar(
        [FromQuery] DateOnly? from, [FromQuery] int days = 30, CancellationToken ct = default)
    {
        var (user, profile) = await ResolveAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });
        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var span = Math.Clamp(days, 7, 90);
        var end = start.AddDays(span);

        var mine = await access.ListingIdsAsync(user, CoHostScope.Calendar, ct);
        var listings = await db.Listings
            .Where(l => mine.Contains(l.Id))
            .OrderBy(l => l.Title)
            .ToListAsync(ct);
        var ids = listings.Select(l => l.Id).ToList();

        var stays = await db.Bookings
            .Where(b => ids.Contains(b.ListingId)
                        && BookingLifecycle.BlocksDates.Contains(b.Status)
                        && b.CheckIn < end && start < b.CheckOut)
            .Select(b => new { b.ListingId, b.CheckIn, b.CheckOut, b.Reference, b.Guests })
            .ToListAsync(ct);

        var blocks = await db.CalendarBlocks
            .Where(b => ids.Contains(b.ListingId) && b.From <= end && start <= b.To)
            .Select(b => new { b.ListingId, b.From, b.To })
            .ToListAsync(ct);

        var rules = await db.PriceRules
            .Where(r => ids.Contains(r.ListingId) && r.From <= end && start <= r.To)
            .ToListAsync(ct);

        var rows = new List<MultiCalendarRowDto>();
        foreach (var listing in listings)
        {
            var booked = new Dictionary<DateOnly, string>();
            foreach (var s in stays.Where(x => x.ListingId == listing.Id))
                for (var d = s.CheckIn; d < s.CheckOut; d = d.AddDays(1)) booked[d] = s.Reference;

            var blocked = new HashSet<DateOnly>();
            foreach (var b in blocks.Where(x => x.ListingId == listing.Id))
                for (var d = b.From; d <= b.To; d = d.AddDays(1)) blocked.Add(d);

            var listingRules = rules.Where(r => r.ListingId == listing.Id).ToList();
            var cells = new List<MultiCalendarCellDto>(span);

            for (var d = start; d < end; d = d.AddDays(1))
            {
                var rate = Pricing.RateFor(listing, d, listingRules);
                var state = booked.ContainsKey(d) ? "booked" : blocked.Contains(d) ? "blocked" : "open";
                cells.Add(new MultiCalendarCellDto(d, rate.Rate, rate.Source, state, booked.GetValueOrDefault(d)));
            }

            rows.Add(new MultiCalendarRowDto(listing.Id, listing.Title, listing.IsPublished, cells));
        }

        return Ok(new MultiCalendarDto(start, span, rows));
    }

    /* --------------------------------------------------------- QL-06/07 */

    /// <summary>
    /// docs/01 QL-06 and QL-07 — the calendar rules that decide who can book:
    /// night limits, notice, turnover, how far ahead the calendar opens, and
    /// which weekdays are closed to arrivals or departures.
    /// </summary>
    [HttpPut("listings/{id:int}/rules")]
    public async Task<ActionResult<CalendarRulesDto>> SaveRules(
        int id, [FromBody] CalendarRulesDto req, CancellationToken ct)
    {
        var listing = await OwnedListingAsync(id, ct, CoHostScope.Pricing);
        if (listing is null) return this.Denied();

        listing.MinNights = Math.Clamp(req.MinNights, 1, 365);
        listing.MaxNights = Math.Clamp(req.MaxNights, 0, 365);
        listing.AdvanceNoticeHours = Math.Clamp(req.AdvanceNoticeHours, 0, 24 * 30);
        listing.SameDayCutoffHour = req.SameDayCutoffHour is int h ? Math.Clamp(h, 0, 23) : null;
        listing.CalendarVisibilityMonths = Math.Clamp(req.CalendarVisibilityMonths, 0, 24);
        listing.TurnoverDays = Math.Clamp(req.TurnoverDays, 0, 14);
        listing.BlockedCheckInDays = req.BlockedCheckInDays & 0b1111111;
        listing.BlockedCheckOutDays = req.BlockedCheckOutDays & 0b1111111;
        if (!string.IsNullOrWhiteSpace(req.TimeZoneId)) listing.TimeZoneId = req.TimeZoneId.Trim();

        // A maximum below the minimum would make the listing unbookable outright.
        if (listing.MaxNights > 0 && listing.MaxNights < listing.MinNights)
            return BadRequest(new { message = "Số đêm tối đa phải lớn hơn hoặc bằng số đêm tối thiểu." });

        await db.SaveChangesAsync(ct);
        return Ok(RulesOf(listing));
    }

    [HttpGet("listings/{id:int}/rules")]
    public async Task<ActionResult<CalendarRulesDto>> GetRules(int id, CancellationToken ct)
    {
        var listing = await OwnedListingAsync(id, ct);
        return listing is null ? this.Denied() : Ok(RulesOf(listing));
    }

    private static CalendarRulesDto RulesOf(Listing l) => new(
        l.MinNights, l.MaxNights, l.AdvanceNoticeHours, l.SameDayCutoffHour,
        l.CalendarVisibilityMonths, l.TurnoverDays,
        l.BlockedCheckInDays, l.BlockedCheckOutDays, l.TimeZoneId);

    /* ------------------------------------------------------------- QL-05 */

    /// <summary>
    /// docs/01 QL-05 — one action over a set of days: set a price, block or
    /// unblock them, or change the minimum stay that starts on them.
    /// </summary>
    [HttpPost("listings/{id:int}/days")]
    public async Task<IActionResult> EditDays(int id, [FromBody] BulkDayEditRequest req, CancellationToken ct)
    {
        var listing = await OwnedListingAsync(id, ct);
        if (listing is null) return this.Denied();

        if (req.To < req.From) return BadRequest(new { message = "Ngày kết thúc phải sau ngày bắt đầu." });
        if (req.To.DayNumber - req.From.DayNumber > 365)
            return BadRequest(new { message = "Chỉ sửa được tối đa 365 ngày một lần." });

        // Day overrides beat seasons, so a bulk edit replaces any earlier
        // override on the same days rather than stacking on top of it.
        var stale = await db.PriceRules
            .Where(r => r.ListingId == id && r.Kind == PriceRuleKind.DayOverride
                        && r.From <= req.To && req.From <= r.To)
            .ToListAsync(ct);

        if (req.NightlyRate is not null || req.MinNights is not null)
        {
            db.PriceRules.RemoveRange(stale);
            db.PriceRules.Add(new PriceRule
            {
                ListingId = id,
                Kind = PriceRuleKind.DayOverride,
                Name = req.Label ?? "Giá theo ngày",
                From = req.From,
                To = req.To,
                NightlyRate = req.NightlyRate ?? listing.PricePerNight,
                MinNights = req.MinNights
            });
        }

        if (req.Blocked == true)
        {
            db.CalendarBlocks.Add(new CalendarBlock
            {
                ListingId = id, From = req.From, To = req.To, Note = req.Label ?? "Chủ nhà khoá"
            });
        }
        else if (req.Blocked == false)
        {
            var overlapping = await db.CalendarBlocks
                .Where(b => b.ListingId == id && b.From <= req.To && req.From <= b.To)
                .ToListAsync(ct);
            db.CalendarBlocks.RemoveRange(overlapping);
        }

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /* ------------------------------------------------------------- QL-15 */

    /// <summary>docs/01 QL-15 — the income report, as a file the host can keep.</summary>
    [HttpGet("earnings.csv")]
    public async Task<IActionResult> EarningsCsv(CancellationToken ct)
    {
        var (user, profile) = await ResolveAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });
        if (profile is null) return NotFound();

        var listingIds = await db.Listings.Where(l => l.HostId == profile.Id).Select(l => l.Id).ToListAsync(ct);

        var rows = await db.Bookings
            .Where(b => listingIds.Contains(b.ListingId) && BookingLifecycle.BlocksDates.Contains(b.Status))
            .Include(b => b.Listing)
            .Include(b => b.Payment)
            .OrderBy(b => b.CheckIn)
            .ToListAsync(ct);

        var vn = CultureInfo.GetCultureInfo("vi-VN");
        var csv = new StringBuilder();
        csv.AppendLine("Mã đơn;Chỗ nghỉ;Nhận phòng;Trả phòng;Số đêm;Khách;Khách trả;Phí dịch vụ chủ nhà;Bạn nhận;Trạng thái;Ngày trả tiền");

        foreach (var b in rows)
        {
            csv.Append(b.Reference).Append(';')
               .Append(Escape(b.Listing?.Title ?? "")).Append(';')
               .Append(b.CheckIn.ToString("dd/MM/yyyy")).Append(';')
               .Append(b.CheckOut.ToString("dd/MM/yyyy")).Append(';')
               .Append(b.Nights).Append(';')
               .Append(b.Guests).Append(';')
               .Append(b.Total.ToString("0", vn)).Append(';')
               .Append(b.HostServiceFee.ToString("0", vn)).Append(';')
               .Append((b.Payment?.HostPayout ?? b.HostPayout).ToString("0", vn)).Append(';')
               .Append(BookingLifecycle.Label(b.Status)).Append(';')
               .Append(b.Payment?.PayoutDueOn?.ToString("dd/MM/yyyy") ?? "")
               .AppendLine();
        }

        // The BOM is what makes Excel open a semicolon-separated UTF-8 file correctly.
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv", $"stayhost-doanh-thu-{DateTime.UtcNow:yyyy-MM-dd}.csv");

        static string Escape(string v) => v.Replace(';', ',').Replace('\n', ' ');
    }

    /* ------------------------------------------------------------- QL-20 */

    [HttpGet("payout")]
    public async Task<ActionResult<PayoutSettingsDto>> GetPayout(CancellationToken ct)
    {
        var (user, profile) = await ResolveAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });
        if (profile is null) return Ok(new PayoutSettingsDto(null, null, null, nameof(PayoutSchedule.PerBooking), []));

        return Ok(await PayoutOf(profile, ct));
    }

    [HttpPut("payout")]
    public async Task<ActionResult<PayoutSettingsDto>> SavePayout(
        [FromBody] SavePayoutRequest req, CancellationToken ct)
    {
        var (user, profile) = await ResolveAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });
        if (profile is null) return this.Denied();

        var digits = new string((req.AccountNumber ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length is > 0 and < 6)
            return BadRequest(new { message = "Số tài khoản không hợp lệ." });

        profile.PayoutBankName = req.BankName?.Trim();
        profile.PayoutAccountName = req.AccountName?.Trim();
        // Only the tail is ever stored: the full number is not ours to keep.
        if (digits.Length >= 6) profile.PayoutAccountLast4 = digits[^4..];
        profile.PayoutSchedule = Enum.TryParse<PayoutSchedule>(req.Schedule, true, out var s)
            ? s
            : PayoutSchedule.PerBooking;

        await db.SaveChangesAsync(ct);
        return Ok(await PayoutOf(profile, ct));
    }

    /// <summary>
    /// docs/03 §5 — money reaches the host 24 hours after the guest checks in,
    /// so the schedule is derived from live bookings rather than a stored plan.
    /// </summary>
    private async Task<PayoutSettingsDto> PayoutOf(HostProfile profile, CancellationToken ct)
    {
        var listingIds = await db.Listings.Where(l => l.HostId == profile.Id).Select(l => l.Id).ToListAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var upcoming = await db.Bookings
            .Where(b => listingIds.Contains(b.ListingId) && BookingLifecycle.BlocksDates.Contains(b.Status))
            .Include(b => b.Payment)
            .Include(b => b.Listing)
            .OrderBy(b => b.CheckIn)
            .Take(30)
            .ToListAsync(ct);

        var schedule = upcoming
            .Select(b => new PayoutRowDto(
                b.Reference,
                b.Listing?.Title ?? "",
                b.Payment?.PayoutDueOn ?? b.CheckIn.AddDays(1),
                b.Payment?.HostPayout ?? b.HostPayout,
                (b.Payment?.PayoutDueOn ?? b.CheckIn.AddDays(1)) <= today ? "Đã chuyển" : "Chờ chuyển"))
            .ToList();

        return new PayoutSettingsDto(
            profile.PayoutBankName, profile.PayoutAccountName, profile.PayoutAccountLast4,
            profile.PayoutSchedule.ToString(), schedule);
    }

    /* ------------------------------------------------------------- QL-17 */

    /// <summary>
    /// docs/03 §8 — the four criteria for Chủ nhà Ưu tú, each with where the
    /// host currently stands, so progress is visible before the quarterly review.
    /// </summary>
    [HttpGet("superhost")]
    public async Task<ActionResult<SuperhostProgressDto>> Superhost(CancellationToken ct)
    {
        var (user, profile) = await ResolveAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });
        if (profile is null) return NotFound();

        // docs/03 §8 — the same numbers and the same thresholds the quarterly
        // sweep decides on, so this screen cannot promise a title the job then
        // refuses. BadgeService owns the counting; Badges owns the rule.
        var stats = await badges.ProgressStatsAsync(profile, ct);
        var criteria = Badges.SuperhostCriteria(stats);

        return Ok(new SuperhostProgressDto(
            profile.IsSuperhost,
            criteria.All(c => c.Met),
            Badges.NextSuperhostReview(DateOnly.FromDateTime(DateTime.UtcNow)),
            criteria.Select(c => new SuperhostCriterionDto(c.Key, c.Label, c.Current, c.Target, c.Met)).ToList()));
    }

    /// <summary>
    /// The owner, or a co-host the owner gave this much rope (docs/01 QL-19).
    /// </summary>
    private async Task<Listing?> OwnedListingAsync(int id, CancellationToken ct, CoHostScope scope = CoHostScope.Calendar)
    {
        var user = await auth.CurrentUserAsync(ct);
        return user is null ? null : await access.ListingAsync(user, id, scope, ct);
    }
}
