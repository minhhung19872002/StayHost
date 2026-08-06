using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Contracts;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

/// <summary>
/// The running-a-listing half of the host console: the daily board, calendar
/// rules, bulk day edits, income export and payout settings.
/// </summary>
[ApiController]
[Route("api/host")]
public class HostOperationsController(StayHostDbContext db, AuthService auth) : ControllerBase
{
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
        if (profile is null) return Ok(new TodayBoardDto([], [], [], [], 0));

        var listingIds = await db.Listings.Where(l => l.HostId == profile.Id).Select(l => l.Id).ToListAsync(ct);
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
        var listing = await OwnedListingAsync(id, ct);
        if (listing is null) return Forbid();

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
        return listing is null ? Forbid() : Ok(RulesOf(listing));
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
        if (listing is null) return Forbid();

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
        if (profile is null) return Forbid();

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

        var listings = await db.Listings.Where(l => l.HostId == profile.Id)
            .Select(l => new { l.Id, l.Rating, l.ReviewCount })
            .ToListAsync(ct);
        var listingIds = listings.Select(l => l.Id).ToList();

        var yearAgo = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1));

        var stays = await db.Bookings
            .Where(b => listingIds.Contains(b.ListingId) && b.CheckIn >= yearAgo)
            .Select(b => new { b.Status, b.Nights, b.CancelledBy })
            .ToListAsync(ct);

        var completed = stays.Where(s => BookingLifecycle.BlocksDates.Contains(s.Status)).ToList();
        var hostCancels = stays.Count(s => s.CancelledBy == CancelledBy.Host);
        var totalOrders = Math.Max(1, completed.Count + hostCancels);

        var rated = listings.Where(l => l.ReviewCount > 0).ToList();
        var rating = rated.Count == 0 ? 0 : Math.Round(rated.Average(l => l.Rating), 2);

        var responded = ParsePercent(profile.ResponseRate);
        var cancelRate = Math.Round(hostCancels * 100.0 / totalOrders, 2);

        // "10 chuyến trở lên trong năm, hoặc từ 3 chuyến với tổng ≥ 100 đêm."
        var nights = completed.Sum(s => s.Nights);
        var enoughStays = completed.Count >= 10 || (completed.Count >= 3 && nights >= 100);

        var criteria = new List<SuperhostCriterionDto>
        {
            new("rating", "Điểm đánh giá tổng ≥ 4.8", $"{rating:0.00}", "4.80", rating >= 4.8),
            new("stays", "Từ 10 chuyến/năm (hoặc 3 chuyến với ≥ 100 đêm)",
                $"{completed.Count} chuyến · {nights} đêm", "10 chuyến", enoughStays),
            new("response", "Tỉ lệ phản hồi ≥ 90%", $"{responded}%", "90%", responded >= 90),
            new("cancellations", "Tỉ lệ tự huỷ < 1%", $"{cancelRate:0.##}%", "1%", cancelRate < 1)
        };

        return Ok(new SuperhostProgressDto(
            profile.IsSuperhost,
            criteria.All(c => c.Met),
            NextReviewDate(DateOnly.FromDateTime(DateTime.UtcNow)),
            criteria));
    }

    /// <summary>Reviewed on 1 January, 1 April, 1 July and 1 October (docs/03 §8).</summary>
    private static DateOnly NextReviewDate(DateOnly today)
    {
        foreach (var month in new[] { 1, 4, 7, 10 })
        {
            var date = new DateOnly(today.Year, month, 1);
            if (date > today) return date;
        }
        return new DateOnly(today.Year + 1, 1, 1);
    }

    private static int ParsePercent(string? value) =>
        int.TryParse(new string((value ?? "").Where(char.IsDigit).ToArray()), out var n) ? n : 0;

    private async Task<Listing?> OwnedListingAsync(int id, CancellationToken ct)
    {
        var (_, profile) = await ResolveAsync(ct);
        if (profile is null) return null;

        var listing = await db.Listings.FirstOrDefaultAsync(l => l.Id == id, ct);
        return listing?.HostId == profile.Id ? listing : null;
    }
}
