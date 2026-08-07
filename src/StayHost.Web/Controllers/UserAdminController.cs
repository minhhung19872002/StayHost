using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Contracts;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

/// <summary>
/// docs/08 — the user-administration console: finding somebody, seeing what the
/// platform knows about them, and the ladder of things that can be done about
/// it.
///
/// Every endpoint goes through <see cref="AdminGate"/>, which is where the four
/// principles of §1 live. Nothing here re-decides who is allowed to do what.
/// </summary>
[ApiController]
[Route("api/admin/users")]
public class UserAdminController(
    StayHostDbContext db, AdminGate gate, AdminAudit audit,
    NotificationService notifications, AuthService auth) : ControllerBase
{
    private ActionResult Refuse(AdminGate.Verdict v) =>
        StatusCode(v.Status ?? 403, new { message = v.Refusal });

    /* ------------------------------------------------------------ QT-U-01 */

    /// <summary>
    /// docs/08 §4 — found by email, phone, name, booking reference, listing id
    /// or payment reference. All six, because support is handed whichever one
    /// the caller happens to have.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminUserRowDto>>> Search(
        [FromQuery] string? q, CancellationToken ct)
    {
        var v = await gate.AllowAsync(AdminAction.FindUser, null, ct);
        if (!v.Ok) return Refuse(v);

        var term = (q ?? "").Trim();
        if (term.Length < 2) return Ok(Array.Empty<AdminUserRowDto>());

        var lower = term.ToLowerInvariant();

        var byBooking = await db.Bookings
            .Where(b => b.Reference.ToLower() == lower
                        || (b.Payment != null && b.Payment.Reference.ToLower() == lower))
            .Select(b => b.GuestUserId)
            .Where(id => id != null)
            .ToListAsync(ct);

        var byListing = int.TryParse(term, out var listingId)
            ? await db.Listings.Where(l => l.Id == listingId)
                .Select(l => l.Host!.UserId).Where(id => id != null).ToListAsync(ct)
            : [];

        var ids = byBooking.Concat(byListing).Where(id => id.HasValue).Select(id => id!.Value).ToList();

        var users = await db.Users
            .Where(u => ids.Contains(u.Id)
                        || u.Email.ToLower().Contains(lower)
                        || (u.Phone != null && u.Phone.Contains(term))
                        || u.FullName.ToLower().Contains(lower))
            .OrderBy(u => u.Id)
            .Take(30)
            .Select(u => new AdminUserRowDto(
                u.Id, u.FullName, u.Email, u.Phone, u.Role.ToString(),
                u.IsBanned ? "Khoá vĩnh viễn" : u.IsSuspended ? "Tạm khoá" : "Bình thường",
                u.IsIdentityVerified, u.CreatedAt))
            .ToListAsync(ct);

        await db.SaveChangesAsync(ct);
        return Ok(users);
    }

    /* ------------------------------------------------- QT-U-02 and QT-U-03 */

    /// <summary>docs/08 §4 — everything the console may show about one person.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<AdminUserDto>> Profile(
        int id, [FromQuery] int? ticketId, CancellationToken ct)
    {
        var v = await gate.AllowAsync(AdminAction.FindUser, null, ct);
        if (!v.Ok) return Refuse(v);

        var user = await db.Users
            .Include(u => u.HostProfile)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null) return NotFound(new { message = "Không tìm thấy người dùng." });

        // docs/08 §10 — the look itself is recorded, ticket or no ticket.
        gate.RecordProfileView(v.Admin!, id, ticketId);

        var listings = user.HostProfile is { } host
            ? await db.Listings.Where(l => l.HostId == host.Id)
                .Select(l => new AdminListingRowDto(l.Id, l.Title, l.City, l.IsPublished, l.Rating, l.ReviewCount))
                .ToListAsync(ct)
            : [];

        var asGuest = await db.Bookings.Where(b => b.GuestUserId == id).ToListAsync(ct);

        var asHost = user.HostProfile is { } h
            ? await db.Bookings.Where(b => b.Listing!.HostId == h.Id).ToListAsync(ct)
            : [];

        var cancelledByThem = asGuest.Count(b => b.Status == BookingStatus.CancelledByGuest)
                              + asHost.Count(b => b.Status == BookingStatus.CancelledByHost);
        var allBookings = asGuest.Count + asHost.Count;

        var sanctions = await db.Sanctions
            .Where(s => s.UserId == id)
            .Include(s => s.DecidedByUser)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SanctionRowDto(
                s.Id, s.Level.ToString(), Sanctions.Label(s.Level),
                s.Restriction == null ? null : Sanctions.RestrictionLabel(s.Restriction.Value),
                s.Policy, s.Reason, s.LiftedWhen,
                s.DecidedByUser!.FullName, s.CreatedAt, s.ExpiresAt,
                s.LiftedAt, s.LiftedReason, s.OverturnedOnAppeal, s.Severe))
            .ToListAsync(ct);

        var reportsAgainst = user.HostProfile is { } hp
            ? await db.ListingReports.CountAsync(r => r.Listing!.HostId == hp.Id, ct)
            : 0;

        var balance = await db.CreditEntries.Where(c => c.UserId == id)
            .SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;

        var cards = await db.SavedCards.Where(c => c.UserId == id)
            .Select(c => SavedCards.BrandLabel(c.Brand) + " ••••" + c.Last4)
            .ToListAsync(ct);

        var sessions = await db.AuthSessions
            .Where(s => s.UserId == id)
            .OrderByDescending(s => s.CreatedAt)
            .Take(10)
            .Select(s => new AdminSessionRowDto(s.UserAgent ?? "", s.CreatedAt, s.RevokedAt == null))
            .ToListAsync(ct);

        // docs/08 §4 — payout account is Finance's to see, and only the tail.
        var maySeePayout = AdminActions.Allows(v.Admin!.AdminScope, AdminAction.ViewPayoutAccount);

        await db.SaveChangesAsync(ct);

        return Ok(new AdminUserDto(
            user.Id, user.FullName, user.DisplayName, user.Email, user.Phone,
            user.Role.ToString(),
            user.IsBanned ? "Khoá vĩnh viễn" : user.IsSuspended ? "Tạm khoá" : "Bình thường",
            user.IsSuspended || user.IsBanned,
            user.SuspendedUntil,
            user.EmailConfirmed, user.PhoneConfirmed, user.IsIdentityVerified,
            user.CreatedAt,
            sessions.FirstOrDefault()?.At,
            user.HostProfile is not null,
            user.HostProfile?.IsSuperhost ?? false,
            listings,
            allBookings, cancelledByThem,
            allBookings == 0 ? 0 : Math.Round(100.0 * cancelledByThem / allBookings, 1),
            await db.Reviews.CountAsync(r => r.AuthorUserId == id, ct),
            reportsAgainst,
            sanctions,
            balance,
            maySeePayout ? user.HostProfile?.PayoutAccountLast4 : null,
            maySeePayout ? user.HostProfile?.PayoutBankName : null,
            cards,
            sessions,
            await RelatedAsync(user, ct),
            AdminActions.All
                .Where(r => AdminActions.Allows(v.Admin.AdminScope, r.Action))
                .Select(r => r.Action.ToString())
                .ToList()));
    }

    /// <summary>
    /// docs/08 §4 and QT-U-03 — accounts that share a phone, a device or the
    /// account the money goes to. The same signal that catches fraud is what
    /// tells an admin they should not be handling this one (§1.3).
    /// </summary>
    private async Task<IReadOnlyList<AdminUserRowDto>> RelatedAsync(User user, CancellationToken ct)
    {
        var devices = await db.AuthSessions
            .Where(s => s.UserId == user.Id && s.UserAgent != null)
            .Select(s => s.UserAgent!)
            .Distinct()
            .ToListAsync(ct);

        var payoutTail = user.HostProfile?.PayoutAccountLast4;

        return await db.Users
            .Where(u => u.Id != user.Id
                        && ((user.Phone != null && u.Phone == user.Phone)
                            || (payoutTail != null && u.HostProfile!.PayoutAccountLast4 == payoutTail)
                            || u.Sessions.Any(s => s.UserAgent != null && devices.Contains(s.UserAgent))))
            .Take(20)
            .Select(u => new AdminUserRowDto(
                u.Id, u.FullName, u.Email, u.Phone, u.Role.ToString(),
                u.IsBanned ? "Khoá vĩnh viễn" : u.IsSuspended ? "Tạm khoá" : "Bình thường",
                u.IsIdentityVerified, u.CreatedAt))
            .ToListAsync(ct);
    }

    /* ------------------------------------------------------------ QT-U-07 */

    /// <summary>
    /// docs/08 §6 and QT-U-07 — what locking this account would cost, shown
    /// before anything is done. "Hệ thống phải hiện cảnh báo trước khi admin bấm
    /// xác nhận."
    /// </summary>
    [HttpGet("{id:int}/lock-preview")]
    public async Task<ActionResult<LockPreviewDto>> LockPreview(
        int id, [FromQuery] bool refundInFull, CancellationToken ct)
    {
        var v = await gate.AllowAsync(AdminAction.FindUser, null, ct);
        if (!v.Ok) return Refuse(v);

        var user = await db.Users.Include(u => u.HostProfile).FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return NotFound();

        var preview = await BuildPreviewAsync(user, refundInFull, ct);

        return Ok(new LockPreviewDto(
            preview.Lines.Select(l => new LockLineDto(
                l.BookingId, l.Reference, l.Action.ToString(), l.Money, l.CounterpartyName, l.Note)).ToList(),
            preview.GuestsStaying, preview.BookingsCancelled,
            preview.MoneyRefunded, preview.PayoutHeld,
            preview.Warning,
            SuspensionImpact.MustKeepAbleToRespond(preview) ? SuspensionImpact.OpenDisputeNotice() : null,
            SuspensionImpact.SafetyRelocationNotice(preview.GuestsStaying),
            user.HostProfile is not null));
    }

    private async Task<SuspensionImpact.Preview> BuildPreviewAsync(
        User user, bool refundInFull, CancellationToken ct)
    {
        var isHost = user.HostProfile is not null;

        var query = isHost
            ? db.Bookings.Where(b => b.Listing!.HostId == user.HostProfile!.Id)
            : db.Bookings.Where(b => b.GuestUserId == user.Id);

        var rows = await query
            .Include(b => b.Payment)
            .Include(b => b.GuestUser)
            .Include(b => b.Listing!).ThenInclude(l => l.Host)
            .Where(b => b.Status != BookingStatus.CancelledByGuest
                        && b.Status != BookingStatus.CancelledByHost
                        && b.Status != BookingStatus.Declined
                        && b.Status != BookingStatus.Expired)
            .ToListAsync(ct);

        var disputed = await db.ResolutionCases
            .Where(c => c.Status == ResolutionStatus.AwaitingResponse || c.Status == ResolutionStatus.Disputed)
            .Select(c => c.BookingId)
            .ToListAsync(ct);

        var bookings = rows.Select(b => new SuspensionImpact.Booking(
            b.Id, b.Reference, b.Status, b.CheckIn, b.CheckOut,
            b.DepositPaid > 0 ? b.DepositPaid : b.Total,
            b.Payment?.HostPayout ?? b.HostPayout,
            isHost ? b.GuestName ?? "Khách" : b.Listing?.Host?.Name ?? "Chủ nhà",
            b.Payment?.PayoutStatus == PayoutStatus.Paid,
            disputed.Contains(b.Id)));

        return isHost
            ? SuspensionImpact.ForHost(bookings)
            : SuspensionImpact.ForGuest(bookings, refundInFull);
    }

    /* -------------------------------------- QT-U-04, QT-U-05 and QT-U-06 */

    /// <summary>
    /// docs/08 §5 — one endpoint for the whole ladder, because the ladder is the
    /// rule: which step is allowed depends on the steps already taken, and
    /// splitting it across four endpoints is how that gets forgotten.
    /// </summary>
    [HttpPost("{id:int}/sanction")]
    public async Task<ActionResult<AdminUserDto>> Sanction(
        int id, [FromBody] SanctionRequest req, CancellationToken ct)
    {
        if (!Enum.TryParse<SanctionLevel>(req.Level, true, out var level))
            return BadRequest(new { message = "Mức xử lý không hợp lệ." });

        var action = level switch
        {
            SanctionLevel.Warning => AdminAction.Warn,
            SanctionLevel.Restriction => AdminAction.Restrict,
            SanctionLevel.Suspension => AdminAction.Suspend,
            _ => AdminAction.Ban
        };

        var v = await gate.AllowAsync(action, req.Reason, ct, id);
        if (!v.Ok) return Refuse(v);

        var user = await db.Users.Include(u => u.HostProfile).FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return NotFound();

        var severe = level >= SanctionLevel.Suspension && Sanctions.IsSevereGround(req.SevereGround);

        // docs/08 §5 — the ladder, unless §5.6 applies.
        var highest = await db.Sanctions
            .Where(s => s.UserId == id && !s.OverturnedOnAppeal)
            .Select(s => (SanctionLevel?)s.Level)
            .OrderByDescending(l => l)
            .FirstOrDefaultAsync(ct);

        if (!Sanctions.IsInOrder(highest, level, severe))
            return BadRequest(new { message = Sanctions.OutOfOrderMessage(highest, level) });

        if (level == SanctionLevel.Ban && !Sanctions.BanGrounds.Contains(req.SevereGround ?? ""))
        {
            return BadRequest(new
            {
                message = "Khoá vĩnh viễn chỉ dùng cho: " + string.Join(", ", Sanctions.BanGrounds).ToLowerInvariant() + ".",
                grounds = Sanctions.BanGrounds
            });
        }

        RestrictionKind? restriction = null;
        if (level == SanctionLevel.Restriction)
        {
            if (!Enum.TryParse<RestrictionKind>(req.Restriction, true, out var kind))
                return BadRequest(new { message = "Chưa chọn loại hạn chế." });
            restriction = kind;
        }

        var now = DateTime.UtcNow;

        var sanction = new Sanction
        {
            UserId = id,
            Level = level,
            Restriction = restriction,
            Policy = (req.Policy ?? "").Trim(),
            Reason = req.Reason!.Trim(),
            LiftedWhen = string.IsNullOrWhiteSpace(req.LiftedWhen) ? null : req.LiftedWhen.Trim(),
            DecidedByUserId = v.Admin!.Id,
            ExpiresAt = req.Days is > 0 ? now.AddDays(req.Days.Value) : null,
            Severe = severe
        };

        db.Sanctions.Add(sanction);

        await ApplyAsync(user, sanction, req.RefundInFull, ct);

        audit.Record(v.Admin, $"user.{level}".ToLowerInvariant(), $"user:{id}",
            user.IsSuspended ? "đã bị khoá" : "bình thường",
            Sanctions.Label(level), req.Reason);

        await db.SaveChangesAsync(ct);

        await notifications.QueueWithEmailAsync(user, NotificationKind.System,
            $"StayHost: {Sanctions.Label(level).ToLowerInvariant()}",
            Sanctions.Notice(sanction) +
            $" Bạn có thể khiếu nại một lần trong {Appeals.WindowDays} ngày.",
            "/account/sanctions", ct);

        return await Profile(id, null, ct);
    }

    /// <summary>docs/08 §5 and §6 — what the sanction actually does to the account.</summary>
    private async Task ApplyAsync(User user, Sanction sanction, bool refundInFull, CancellationToken ct)
    {
        switch (sanction.Level)
        {
            case SanctionLevel.Warning:
                // Nothing is blocked. That is what makes it a warning.
                break;

            case SanctionLevel.Restriction when sanction.Restriction is { } kind:
                user.RestrictionMask |= 1 << (int)kind;
                if (kind == RestrictionKind.ListingsHiddenFromSearch) await HideListingsAsync(user, ct);
                if (kind == RestrictionKind.PayoutsHeld) await HoldPayoutsAsync(user, ct);
                break;

            case SanctionLevel.Suspension:
            case SanctionLevel.Ban:
                user.IsSuspended = true;
                user.SuspendedUntil = sanction.ExpiresAt;
                user.IsBanned = sanction.Level == SanctionLevel.Ban;

                // docs/08 §6 — an open case keeps the door open enough to answer it.
                var preview = await BuildPreviewAsync(user, refundInFull, ct);
                user.MayStillRespondToDisputes = SuspensionImpact.MustKeepAbleToRespond(preview);

                await HideListingsAsync(user, ct);
                await HoldPayoutsAsync(user, ct);

                // Every session ends now; a lock that leaves a live cookie is not a lock.
                foreach (var s in await db.AuthSessions.Where(s => s.UserId == user.Id && s.RevokedAt == null).ToListAsync(ct))
                    s.RevokedAt = DateTime.UtcNow;
                break;
        }
    }

    private async Task HideListingsAsync(User user, CancellationToken ct)
    {
        if (user.HostProfile is not { } host) return;

        foreach (var l in await db.Listings.Where(l => l.HostId == host.Id && l.IsPublished).ToListAsync(ct))
            l.IsPublished = false;
    }

    private async Task HoldPayoutsAsync(User user, CancellationToken ct)
    {
        if (user.HostProfile is not { } host) return;

        var payments = await db.Payments
            .Where(p => p.PayoutStatus != PayoutStatus.Paid && p.Booking!.Listing!.HostId == host.Id)
            .ToListAsync(ct);

        foreach (var p in payments)
        {
            p.PayoutStatus = PayoutStatus.OnHold;
            p.PayoutHoldReason = PayoutHoldReason.Dispute;
        }
    }

    /* ------------------------------------------------------------ QT-U-06 */

    /// <summary>docs/08 §5.5 — lifting it, with a reason of its own.</summary>
    [HttpPost("{id:int}/restore")]
    public async Task<ActionResult<AdminUserDto>> Restore(
        int id, [FromBody] RestoreRequest req, CancellationToken ct)
    {
        var v = await gate.AllowAsync(AdminAction.Restore, req.Reason, ct, id);
        if (!v.Ok) return Refuse(v);

        var user = await db.Users.Include(u => u.HostProfile).FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return NotFound();

        // docs/08 §5.5 — only the top role reopens a permanent ban.
        if (user.IsBanned && !v.Admin!.AdminScope.HasFlag(AdminScope.Super))
            return StatusCode(403, new { message = "Tài khoản bị khoá vĩnh viễn chỉ Quản trị tối cao mở được." });

        var now = DateTime.UtcNow;

        foreach (var s in await db.Sanctions.Where(s => s.UserId == id && s.LiftedAt == null).ToListAsync(ct))
        {
            s.LiftedAt = now;
            s.LiftedByUserId = v.Admin!.Id;
            s.LiftedReason = req.Reason!.Trim();
        }

        user.IsSuspended = false;
        user.IsBanned = false;
        user.SuspendedUntil = null;
        user.RestrictionMask = 0;
        user.MayStillRespondToDisputes = false;

        audit.Record(v.Admin!, "user.restore", $"user:{id}", "đang bị xử lý", "bình thường", req.Reason);
        await db.SaveChangesAsync(ct);

        await notifications.QueueWithEmailAsync(user, NotificationKind.System,
            "Tài khoản StayHost đã được khôi phục",
            $"Các hạn chế trên tài khoản của bạn đã được gỡ. Lý do: {req.Reason!.Trim()}",
            "/", ct);

        return await Profile(id, null, ct);
    }

    /* -------------------------------------------- QT-U-09 and QT-U-10 */

    /// <summary>docs/08 §2 — force a password reset and end every session.</summary>
    [HttpPost("{id:int}/force-password-reset")]
    public async Task<ActionResult> ForcePasswordReset(
        int id, [FromBody] RestoreRequest req, CancellationToken ct)
    {
        var v = await gate.AllowAsync(AdminAction.ForcePasswordReset, req.Reason, ct, id);
        if (!v.Ok) return Refuse(v);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return NotFound();

        foreach (var s in await db.AuthSessions.Where(s => s.UserId == id && s.RevokedAt == null).ToListAsync(ct))
            s.RevokedAt = DateTime.UtcNow;

        var token = await auth.BeginPasswordResetAsync(user.Email, ct);

        audit.Record(v.Admin!, "user.force-password-reset", $"user:{id}", null, "đã huỷ mọi phiên", req.Reason);
        await db.SaveChangesAsync(ct);

        await notifications.QueueWithEmailAsync(user, NotificationKind.System,
            "Cần đặt lại mật khẩu StayHost",
            $"Vì lý do an toàn, mọi phiên đăng nhập của bạn đã bị huỷ và bạn cần đặt lại mật khẩu. " +
            $"Lý do: {req.Reason!.Trim()}",
            $"/reset-password?token={token}", ct);

        return Ok(new { message = "Đã huỷ mọi phiên và gửi yêu cầu đặt lại mật khẩu." });
    }

    /* ------------------------------------------------------------ QT-U-11 */

    /// <summary>
    /// docs/08 §4 — "chỉ vai Kiểm duyệt và Tối cao, mỗi lần xem phải nhập lý do,
    /// mỗi lần xem ghi một dòng nhật ký riêng, ảnh có đóng dấu mờ tên admin đang
    /// xem."
    ///
    /// The watermark is the part that changes behaviour: a screenshot of somebody
    /// else's identity card that leaves this building carries the name of whoever
    /// was looking at it.
    /// </summary>
    [HttpPost("{id:int}/identity")]
    public async Task<ActionResult<IdentityViewDto>> ViewIdentity(
        int id, [FromBody] RestoreRequest req, CancellationToken ct)
    {
        var v = await gate.AllowAsync(AdminAction.ViewIdentityDocuments, req.Reason, ct);
        if (!v.Ok) return Refuse(v);

        var check = await db.IdentityChecks
            .Where(c => c.UserId == id)
            .OrderByDescending(c => c.SubmittedAt)
            .FirstOrDefaultAsync(ct);

        if (check is null) return NotFound(new { message = "Người dùng chưa gửi giấy tờ nào." });

        // docs/08 §4 — its own line, on top of the ordinary audit row.
        gate.RecordSensitiveRead(v.Admin!, AdminAction.ViewIdentityDocuments, $"user:{id}", req.Reason!);
        await db.SaveChangesAsync(ct);

        var stamp = $"{v.Admin!.FullName} · {DateTime.UtcNow:HH:mm dd/MM/yyyy}";

        return Ok(new IdentityViewDto(
            IdentityChecks.DocumentLabel(check.Document),
            check.DocumentLast4,
            check.FrontImageUrl, check.BackImageUrl, check.SelfieImageUrl,
            stamp,
            check.Status.ToString(), check.SubmittedAt));
    }

    /// <summary>docs/08 §2 — send them round the identity check again.</summary>
    [HttpPost("{id:int}/force-identity-recheck")]
    public async Task<ActionResult> ForceIdentityRecheck(
        int id, [FromBody] RestoreRequest req, CancellationToken ct)
    {
        var v = await gate.AllowAsync(AdminAction.ForceIdentityRecheck, req.Reason, ct, id);
        if (!v.Ok) return Refuse(v);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return NotFound();

        user.IsIdentityVerified = false;

        audit.Record(v.Admin!, "user.force-identity-recheck", $"user:{id}", "đã xác minh", "cần xác minh lại", req.Reason);
        await db.SaveChangesAsync(ct);

        await notifications.QueueWithEmailAsync(user, NotificationKind.System,
            "Cần xác minh lại danh tính",
            $"Vui lòng gửi lại giấy tờ tuỳ thân để xác minh. Lý do: {req.Reason!.Trim()}",
            "/account/identity", ct);

        return Ok(new { message = "Đã yêu cầu xác minh lại danh tính." });
    }
}
