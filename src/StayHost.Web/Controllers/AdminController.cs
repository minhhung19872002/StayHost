using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Contracts;
using StayHost.Web.Infrastructure;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

/// <summary>Reports are open to any visitor; everything else requires an admin account.</summary>
[ApiController]
[Route("api")]
public class AdminController(StayHostDbContext db, AuthService auth, NotificationService notifications)
    : ControllerBase
{
    /* ------------------------------------------------------------- reports */

    [HttpPost("reports")]
    public async Task<ActionResult<object>> Report([FromBody] CreateReportRequest req, CancellationToken ct)
    {
        var listing = await db.Listings.FirstOrDefaultAsync(l => l.Id == req.ListingId, ct);
        if (listing is null) return NotFound();

        var reason = (req.Reason ?? "").Trim();
        if (reason.Length == 0) return BadRequest(new { message = "Vui lòng chọn lý do báo cáo." });

        var user = await auth.CurrentUserAsync(ct);

        db.ListingReports.Add(new ListingReport
        {
            ListingId = listing.Id,
            ReporterUserId = user?.Id,
            SessionId = HttpContext.SessionId(),
            Reason = reason,
            Detail = req.Detail?.Trim()
        });

        await db.SaveChangesAsync(ct);
        return Ok(new { message = "Cảm ơn bạn. Chúng tôi sẽ xem xét báo cáo này." });
    }

    /* --------------------------------------------------------------- admin */

    private async Task<User?> RequireAdminAsync(CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        return user?.Role == UserRole.Admin ? user : null;
    }

    [HttpGet("admin/overview")]
    public async Task<ActionResult<AdminOverviewDto>> Overview(CancellationToken ct)
    {
        if (await RequireAdminAsync(ct) is null)
            return StatusCode(403, new { message = "Chỉ quản trị viên mới xem được trang này." });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var payments = await db.Payments
            .Where(p => p.Status != PaymentStatus.Refunded && p.Status != PaymentStatus.Failed)
            .Select(p => new { p.Amount, p.PlatformFee })
            .ToListAsync(ct);

        var recent = await db.Listings
            .Include(l => l.Host)
            .OrderByDescending(l => l.CreatedAt)
            .Take(12)
            .ToListAsync(ct);

        var reports = await LoadReportsAsync(ct);

        return Ok(new AdminOverviewDto(
            await db.Users.CountAsync(ct),
            await db.Hosts.CountAsync(ct),
            await db.Listings.CountAsync(ct),
            await db.Listings.CountAsync(l => l.IsPublished, ct),
            await db.Listings.CountAsync(l => !l.IsPublished, ct),
            await db.Bookings.CountAsync(ct),
            await db.Bookings.CountAsync(b => b.Status != BookingStatus.Cancelled && b.CheckOut >= today, ct),
            payments.Sum(p => p.Amount),
            payments.Sum(p => p.PlatformFee),
            await db.ListingReports.CountAsync(r => r.Status == ReportStatus.Open, ct),
            await db.EmailMessages.CountAsync(e => e.SentAt == null, ct),
            recent.Select(l => new AdminListingDto(
                l.Id, l.Slug, l.Title, l.City, l.Host?.Name ?? "",
                l.IsPublished, Math.Round(l.Rating, 2), l.ReviewCount, l.PricePerNight, l.CreatedAt)).ToList(),
            reports,
            await ReconcileAsync(ct)));
    }

    private static readonly Dictionary<LedgerAccount, string> AccountLabels = new()
    {
        [LedgerAccount.GuestFunds] = "Tiền khách đang giữ",
        [LedgerAccount.HostPayable] = "Phải trả chủ nhà",
        [LedgerAccount.GuestServiceFeeRevenue] = "Doanh thu phí dịch vụ khách",
        [LedgerAccount.HostServiceFeeRevenue] = "Doanh thu phí dịch vụ chủ nhà",
        [LedgerAccount.TaxPayable] = "Thuế phải nộp",
        [LedgerAccount.GuestRefundPayable] = "Phải hoàn cho khách",
        [LedgerAccount.PromotionalCredit] = "Số dư khuyến mãi đã cấp",
        [LedgerAccount.PlatformExpense] = "Chi phí sàn"
    };

    /// <summary>
    /// docs/03 §5: total in must equal total out, checked every day. Anything
    /// other than zero here is the alarm the spec calls for.
    /// </summary>
    private async Task<LedgerReportDto> ReconcileAsync(CancellationToken ct)
    {
        var rows = await db.LedgerEntries
            .GroupBy(e => new { e.Account, e.Direction })
            .Select(g => new { g.Key.Account, g.Key.Direction, Total = g.Sum(x => x.Amount), Count = g.Count() })
            .ToListAsync(ct);

        var accounts = rows
            .GroupBy(r => r.Account)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var debits = g.Where(r => r.Direction == LedgerDirection.Debit).Sum(r => r.Total);
                var credits = g.Where(r => r.Direction == LedgerDirection.Credit).Sum(r => r.Total);
                return new LedgerAccountDto(
                    g.Key.ToString(), AccountLabels.GetValueOrDefault(g.Key, g.Key.ToString()),
                    debits, credits, debits - credits);
            })
            .ToList();

        return new LedgerReportDto(
            accounts.Sum(a => a.Balance),
            rows.Sum(r => r.Count),
            await db.LedgerEntries.Select(e => e.TransactionId).Distinct().CountAsync(ct),
            accounts);
    }

    [HttpPost("admin/listings/{id:int}/publish")]
    public async Task<IActionResult> SetPublished(int id, [FromQuery] bool published, CancellationToken ct)
    {
        if (await RequireAdminAsync(ct) is null)
            return StatusCode(403, new { message = "Chỉ quản trị viên mới thao tác được." });

        var listing = await db.Listings.Include(l => l.Host!).ThenInclude(h => h.User)
            .FirstOrDefaultAsync(l => l.Id == id, ct);
        if (listing is null) return NotFound();

        listing.IsPublished = published;

        await notifications.QueueWithEmailAsync(
            listing.Host?.User,
            published ? NotificationKind.ListingApproved : NotificationKind.ListingRejected,
            published ? "Chỗ nghỉ đã được duyệt" : "Chỗ nghỉ đã bị gỡ hiển thị",
            published
                ? $"\"{listing.Title}\" đã hiển thị công khai và có thể nhận đặt chỗ."
                : $"\"{listing.Title}\" đã được gỡ khỏi kết quả tìm kiếm. Vui lòng liên hệ hỗ trợ để biết thêm.",
            "/hosting", ct);

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("admin/reports/{id:int}/resolve")]
    public async Task<IActionResult> ResolveReport(int id, [FromBody] ResolveReportRequest req, CancellationToken ct)
    {
        if (await RequireAdminAsync(ct) is null)
            return StatusCode(403, new { message = "Chỉ quản trị viên mới thao tác được." });

        var report = await db.ListingReports.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (report is null) return NotFound();

        report.Status = Enum.TryParse<ReportStatus>(req.Status, true, out var status) ? status : ReportStatus.Resolved;
        report.Resolution = req.Resolution?.Trim();
        report.ResolvedAt = report.Status is ReportStatus.Resolved or ReportStatus.Dismissed
            ? DateTime.UtcNow
            : null;

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<List<ReportDto>> LoadReportsAsync(CancellationToken ct)
    {
        var reports = await db.ListingReports
            .Include(r => r.Listing)
            .Include(r => r.ReporterUser)
            .OrderBy(r => r.Status)
            .ThenByDescending(r => r.CreatedAt)
            .Take(40)
            .ToListAsync(ct);

        return reports.Select(r => new ReportDto(
            r.Id, r.ListingId, r.Listing?.Title ?? "", r.Reason, r.Detail,
            r.Status.ToString(), r.Resolution,
            r.ReporterUser?.FullName ?? "Khách ẩn danh", r.CreatedAt)).ToList();
    }
}
