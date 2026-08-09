using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Contracts;
using StayHost.Web.Infrastructure;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

/// <summary>
/// docs/08 §7, §8, §9 and §10 — impersonation, appeals, data requests, and the
/// watching of the watchers.
/// </summary>
[ApiController]
[Route("api/admin")]
public class AdminOversightController(
    StayHostDbContext db, AdminGate gate, AdminAudit audit,
    AuthService auth, NotificationService notifications) : ControllerBase
{
    private ActionResult Refuse(AdminGate.Verdict v) =>
        StatusCode(v.Status ?? 403, new { message = v.Refusal });

    /* -------------------------------------------------- QT-U-12, §7 */

    /// <summary>
    /// docs/08 §7 — enter somebody's account to see what they see.
    ///
    /// §7.1 is the load-bearing constraint: it opens from a case, never from a
    /// profile page. Without something to justify it against, "I needed to look"
    /// is unfalsifiable.
    /// </summary>
    [HttpPost("impersonate")]
    public async Task<ActionResult<ImpersonationDto>> Impersonate(
        [FromBody] ImpersonateRequest req, CancellationToken ct)
    {
        var v = await gate.AllowAsync(AdminAction.Impersonate, req.Reason, ct, req.UserId);
        if (!v.Ok) return Refuse(v);

        var target = await db.Users.FirstOrDefaultAsync(u => u.Id == req.UserId, ct);
        if (target is null) return NotFound(new { message = "Không tìm thấy người dùng." });

        // docs/08 §7.1 — the case has to exist, be open, and be theirs.
        var ticket = await db.ResolutionCases
            .Include(c => c.Booking!).ThenInclude(b => b.Listing!).ThenInclude(l => l.Host)
            .FirstOrDefaultAsync(c => c.Id == req.TicketId, ct);

        if (ticket is null)
            return BadRequest(new { message = "Cần một hồ sơ hỗ trợ đang mở. Không mở tự do từ trang hồ sơ được." });

        var stillOpen = ticket.Status is ResolutionStatus.AwaitingResponse or ResolutionStatus.Disputed;
        if (!stillOpen)
            return BadRequest(new { message = "Hồ sơ này đã đóng, không dùng để đăng nhập thay mặt được." });

        var involvesThem = ticket.OpenedByUserId == target.Id
                           || ticket.Booking?.GuestUserId == target.Id
                           || ticket.Booking?.Listing?.Host?.UserId == target.Id;

        if (!involvesThem)
            return BadRequest(new { message = "Hồ sơ này không liên quan tới người dùng đó." });

        var now = DateTime.UtcNow;

        var session = new ImpersonationSession
        {
            AdminUserId = v.Admin!.Id,
            TargetUserId = target.Id,
            TicketId = ticket.Id,
            Reason = req.Reason!.Trim(),
            StartedAt = now,
            ExpiresAt = Impersonation.ExpiryFor(now)
        };

        db.ImpersonationSessions.Add(session);

        gate.RecordSensitiveRead(v.Admin, AdminAction.Impersonate, $"user:{target.Id}", req.Reason!);
        await db.SaveChangesAsync(ct);

        // docs/08 §7.4 — the person is told, unless a fraud investigation was
        // separately approved to stay quiet. "Separately" is the load-bearing
        // word: the approval must come from a different Super, by name, and that
        // person is told they are on the hook for it.
        var silenced = false;
        if (req.SilentFraudInvestigation)
        {
            var approver = req.SilenceApprovedByUserId is { } approverId
                ? await db.Users.FirstOrDefaultAsync(
                    u => u.Id == approverId && u.Role == UserRole.Admin
                         && u.AdminScope.HasFlag(AdminScope.Super), ct)
                : null;

            if (approver is null || approver.Id == v.Admin.Id)
            {
                return BadRequest(new
                {
                    message = "Điều tra gian lận cần một Quản trị tối cao KHÁC phê duyệt việc không báo " +
                              "cho người dùng. Hãy nhập đúng người đã phê duyệt."
                });
            }

            session.SilenceApprovedByUserId = approver.Id;
            silenced = true;

            await notifications.QueueWithEmailAsync(approver, NotificationKind.System,
                "Bạn đứng tên phê duyệt một phiên thay mặt không báo người dùng",
                $"{v.Admin.FullName} vừa vào tài khoản người dùng #{target.Id} theo hồ sơ #{ticket.Id} " +
                $"trong chế độ điều tra gian lận, ghi bạn là người phê duyệt. " +
                "Nếu bạn không phê duyệt việc này, hãy báo ngay.",
                "/admin", ct);
        }

        if (!silenced)
        {
            await notifications.QueueWithEmailAsync(target, NotificationKind.System,
                "Nhân viên hỗ trợ đã truy cập tài khoản của bạn",
                Impersonation.TargetNotice(v.Admin.FullName, now, session.Reason), "/", ct);
            session.NotifiedTarget = true;
        }

        await db.SaveChangesAsync(ct);

        return Ok(ToDto(session, v.Admin.FullName, target.FullName, now));
    }

    /// <summary>Where the browser asks whether it is still inside somebody's account.</summary>
    [HttpGet("impersonate/current")]
    public async Task<ActionResult<ImpersonationDto>> CurrentImpersonation(CancellationToken ct)
    {
        var admin = await auth.CurrentUserAsync(ct);
        if (admin is null || admin.Role != UserRole.Admin) return NoContent();

        var now = DateTime.UtcNow;

        var session = await db.ImpersonationSessions
            .Include(s => s.TargetUser).Include(s => s.AdminUser)
            .Where(s => s.AdminUserId == admin.Id && s.EndedAt == null)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(ct);

        if (session is null) return NoContent();

        // docs/08 §7.3 — it ends by itself, whether or not anybody closed the tab.
        if (Impersonation.Expired(session, now))
        {
            session.EndedAt = session.ExpiresAt;
            await db.SaveChangesAsync(ct);
            return NoContent();
        }

        return Ok(ToDto(session, session.AdminUser!.FullName, session.TargetUser!.FullName, now));
    }

    [HttpPost("impersonate/end")]
    public async Task<ActionResult> EndImpersonation(CancellationToken ct)
    {
        var admin = await auth.CurrentUserAsync(ct);
        if (admin is null) return Unauthorized();

        var open = await db.ImpersonationSessions
            .Where(s => s.AdminUserId == admin.Id && s.EndedAt == null)
            .ToListAsync(ct);

        foreach (var s in open) s.EndedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(new { message = "Đã thoát chế độ thay mặt." });
    }

    private static ImpersonationDto ToDto(
        ImpersonationSession s, string adminName, string targetName, DateTime now)
    {
        var left = Impersonation.Remaining(s, now);

        return new ImpersonationDto(
            s.Id, s.TargetUserId, targetName, adminName, s.TicketId, s.Reason,
            s.ExpiresAt, (int)left.TotalSeconds,
            Impersonation.BannerText(adminName, targetName, left),
            Impersonation.ForbiddenLabels,
            s.NotifiedTarget);
    }

    /* ------------------------------------------------------- QT-U-16, §8 */

    [HttpGet("appeals")]
    public async Task<ActionResult<IReadOnlyList<AppealDto>>> Appeals(CancellationToken ct)
    {
        var v = await gate.AllowAsync(AdminAction.FindUser, null, ct);
        if (!v.Ok) return Refuse(v);

        var now = DateTime.UtcNow;

        var rows = await db.Appeals
            .Include(a => a.User)
            .Include(a => a.Sanction)
            .Include(a => a.ReviewedByUser)
            .OrderBy(a => a.Status).ThenBy(a => a.DueBy)
            .Take(60)
            .ToListAsync(ct);

        return Ok(rows.Select(a => new AppealDto(
            a.Id, a.UserId, a.User!.FullName,
            a.SanctionId,
            Sanctions.Label(a.Sanction!.Level),
            a.Sanction.Reason,
            a.Argument,
            a.Status.ToString(), Domain.Appeals.StatusLabel(a.Status),
            a.CreatedAt, a.DueBy, Domain.Appeals.Overdue(a, now),
            a.ReviewedByUser?.FullName, a.ReviewedAt, a.Outcome,
            // docs/08 §8 — whoever made the call cannot be the one to review it.
            Domain.Appeals.MayReview(v.Admin!.Id, a.Sanction.DecidedByUserId))).ToList());
    }

    /// <summary>docs/08 §8 — the answer, by somebody who did not make the call.</summary>
    [HttpPost("appeals/{id:int}/decide")]
    public async Task<ActionResult<IReadOnlyList<AppealDto>>> DecideAppeal(
        int id, [FromBody] DecideAppealRequest req, CancellationToken ct)
    {
        var appeal = await db.Appeals
            .Include(a => a.Sanction)
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (appeal is null) return NotFound();

        // Reviewing an appeal is deciding the original question again, so it
        // needs the same permission the original decision needed.
        var action = appeal.Sanction!.Level switch
        {
            SanctionLevel.Warning => AdminAction.Warn,
            SanctionLevel.Restriction => AdminAction.Restrict,
            SanctionLevel.Suspension => AdminAction.Suspend,
            _ => AdminAction.Ban
        };

        var v = await gate.AllowAsync(action, req.Outcome, ct, appeal.UserId);
        if (!v.Ok) return Refuse(v);

        if (appeal.Status != AppealStatus.Open)
            return BadRequest(new { message = "Khiếu nại này đã có kết quả." });

        // docs/08 §13 scenario 7.
        if (!Domain.Appeals.MayReview(v.Admin!.Id, appeal.Sanction.DecidedByUserId))
            return StatusCode(403, new { message = Domain.Appeals.SameReviewerMessage() });

        if (!Domain.Appeals.OutcomeIsUsable(req.Outcome))
            return BadRequest(new { message = Domain.Appeals.CurtOutcomeMessage() });

        if (!Enum.TryParse<AppealStatus>(req.Result, true, out var result) || result == AppealStatus.Open)
            return BadRequest(new { message = "Kết quả không hợp lệ." });

        appeal.Status = result;
        appeal.ReviewedByUserId = v.Admin.Id;
        appeal.ReviewedAt = DateTime.UtcNow;
        appeal.Outcome = req.Outcome!.Trim();

        if (result == AppealStatus.Overturned)
        {
            // docs/08 §8 — off the record entirely, which matters because §5's
            // ladder counts what came before.
            appeal.Sanction.OverturnedOnAppeal = true;
            appeal.Sanction.LiftedAt = DateTime.UtcNow;
            appeal.Sanction.LiftedByUserId = v.Admin.Id;
            appeal.Sanction.LiftedReason = "Gỡ bỏ theo kết quả khiếu nại";
            await UnlockAsync(appeal.UserId, ct);
        }
        else if (result == AppealStatus.Reduced
                 && Enum.TryParse<SanctionLevel>(req.ReducedTo, true, out var lower)
                 && lower < appeal.Sanction.Level)
        {
            appeal.ReducedTo = lower;
            appeal.Sanction.Level = lower;
            if (lower < SanctionLevel.Suspension) await UnlockAsync(appeal.UserId, ct);
        }

        audit.Record(v.Admin, "appeal.decide", $"appeal:{id}",
            "Đang xét", Domain.Appeals.StatusLabel(result), req.Outcome);

        await db.SaveChangesAsync(ct);

        await notifications.QueueWithEmailAsync(appeal.User, NotificationKind.System,
            $"Kết quả khiếu nại: {Domain.Appeals.StatusLabel(result).ToLowerInvariant()}",
            appeal.Outcome!, "/account/sanctions", ct);

        return await Appeals(ct);
    }

    private async Task UnlockAsync(int userId, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return;

        user.IsSuspended = false;
        user.IsBanned = false;
        user.SuspendedUntil = null;
        user.RestrictionMask = 0;
    }

    /* ------------------------------------------------------- QT-U-14, §9 */

    [HttpGet("data-requests")]
    public async Task<ActionResult<IReadOnlyList<DataRequestDto>>> DataRequests(CancellationToken ct)
    {
        var v = await gate.AllowAsync(AdminAction.FindUser, null, ct);
        if (!v.Ok) return Refuse(v);

        var now = DateTime.UtcNow;

        var rows = await db.DataRequests
            .Include(r => r.User)
            .OrderBy(r => r.Status).ThenBy(r => r.DueBy)
            .Take(60)
            .ToListAsync(ct);

        var result = new List<DataRequestDto>();

        foreach (var r in rows)
        {
            var blockers = r.Kind == DataRequestKind.Erase
                ? await BlockersAsync(r.UserId, ct)
                : new Domain.DataRequests.Blockers(false, false, false, false);

            result.Add(new DataRequestDto(
                r.Id, r.UserId, r.User!.FullName, r.User.Email,
                r.Kind.ToString(), Domain.DataRequests.KindLabel(r.Kind),
                r.Status.ToString(), Domain.DataRequests.StatusLabel(r.Status),
                r.CreatedAt, r.DueBy, Domain.DataRequests.Overdue(r, now),
                r.Note,
                Domain.DataRequests.Reasons(blockers),
                Domain.DataRequests.MayErase(blockers)));
        }

        return Ok(result);
    }

    /// <summary>
    /// docs/08 §9 — fulfilling an export: mint the time-limited link and tell the
    /// person it is waiting. The file itself is built on download rather than
    /// stored, so a copy of somebody's whole life is never sitting on a disk.
    /// </summary>
    [HttpPost("data-requests/{id:int}/export")]
    public async Task<ActionResult<IReadOnlyList<DataRequestDto>>> FulfilExport(
        int id, [FromBody] RestoreRequest req, CancellationToken ct)
    {
        var request = await db.DataRequests.Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == id && r.Kind == DataRequestKind.Export, ct);

        if (request is null) return NotFound(new { message = "Không tìm thấy yêu cầu xuất dữ liệu này." });

        var v = await gate.AllowAsync(AdminAction.EraseData, req.Reason, ct, request.UserId);
        if (!v.Ok) return Refuse(v);

        var now = DateTime.UtcNow;

        request.LinkToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        request.LinkExpiresAt = now + Domain.DataRequests.LinkLifetime;
        request.Status = DataRequestStatus.Done;
        request.CompletedAt = now;
        request.HandledByUserId = v.Admin!.Id;
        request.Note = Domain.DataRequests.ExportReadyNotice(request.LinkExpiresAt.Value);

        audit.Record(v.Admin, "user.data-export", $"user:{request.UserId}",
            null, "đã cấp liên kết tải", req.Reason);

        await db.SaveChangesAsync(ct);

        await notifications.QueueWithEmailAsync(request.User, NotificationKind.System,
            "Dữ liệu cá nhân của bạn đã sẵn sàng để tải",
            request.Note, $"/api/account/data/download/{request.LinkToken}", ct);

        return await DataRequests(ct);
    }

    /// <summary>
    /// docs/08 §9 and §13 scenario 9 — the person disappears and the money does
    /// not. Nothing is deleted here: the account is anonymised in place, so every
    /// booking, payment and ledger line keeps pointing at a row that still exists.
    /// </summary>
    [HttpPost("data-requests/{id:int}/erase")]
    public async Task<ActionResult<IReadOnlyList<DataRequestDto>>> Erase(
        int id, [FromBody] RestoreRequest req, CancellationToken ct)
    {
        var request = await db.DataRequests.Include(r => r.User).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (request is null) return NotFound();

        var v = await gate.AllowAsync(AdminAction.EraseData, req.Reason, ct, request.UserId);
        if (!v.Ok) return Refuse(v);

        var blockers = await BlockersAsync(request.UserId, ct);
        if (!Domain.DataRequests.MayErase(blockers))
        {
            request.Status = DataRequestStatus.Blocked;
            request.Note = Domain.DataRequests.BlockedMessage(blockers);
            await db.SaveChangesAsync(ct);
            return BadRequest(new { message = request.Note });
        }

        var user = request.User!;
        var before = $"{user.FullName} <{user.Email}>";

        user.FullName = Domain.DataRequests.AnonymisedName(user.Id);
        user.DisplayName = null;
        user.Initials = "??";
        user.Email = Domain.DataRequests.AnonymisedEmail(user.Id);
        user.Phone = null;
        user.AvatarUrl = null;
        user.Bio = null;
        user.Location = null;
        user.Occupation = null;
        user.Interests = null;
        user.IsIdentityVerified = false;
        user.EmailConfirmed = false;
        user.PhoneConfirmed = false;
        user.ErasedAt = DateTime.UtcNow;

        // docs/08 §9 — reviews stay ("đánh giá thuộc về cộng đồng") but the
        // author's name is copied onto every review row, so it goes here too.
        await AnonymiseReviewsAsync(user.Id, ct);

        // The identity images are the one thing that is genuinely destroyed:
        // they are copies of a government document and nothing needs them.
        foreach (var check in await db.IdentityChecks.Where(c => c.UserId == user.Id).ToListAsync(ct))
            db.IdentityChecks.Remove(check);

        foreach (var card in await db.SavedCards.Where(c => c.UserId == user.Id).ToListAsync(ct))
            db.SavedCards.Remove(card);

        foreach (var s in await db.AuthSessions.Where(s => s.UserId == user.Id && s.RevokedAt == null).ToListAsync(ct))
            s.RevokedAt = DateTime.UtcNow;

        request.Status = DataRequestStatus.Done;
        request.CompletedAt = DateTime.UtcNow;
        request.HandledByUserId = v.Admin!.Id;
        request.Note = Domain.DataRequests.ErasureSummary();

        audit.Record(v.Admin, "user.erase", $"user:{user.Id}", before, "đã ẩn danh", req.Reason);
        await db.SaveChangesAsync(ct);

        return await DataRequests(ct);
    }

    /// <summary>
    /// docs/08 §9 — "Chỉ ẩn tên người viết." The name is copied onto every review
    /// row when it is written, so anonymising the account row alone leaves the
    /// real name on public pages. Host-side names live on HostProfile and are
    /// wiped with the same brush.
    /// </summary>
    private async Task AnonymiseReviewsAsync(int userId, CancellationToken ct)
    {
        var ghost = Domain.DataRequests.AnonymousReviewerName();

        foreach (var r in await db.Reviews.Where(r => r.AuthorUserId == userId).ToListAsync(ct))
        {
            r.AuthorName = ghost;
            r.AuthorInitials = "•";
            r.AuthorLocation = null;
        }

        var hostProfileId = await db.Users
            .Where(u => u.Id == userId)
            .Select(u => u.HostProfileId)
            .FirstOrDefaultAsync(ct);

        if (hostProfileId is { } hostId
            && await db.Hosts.FirstOrDefaultAsync(h => h.Id == hostId, ct) is { } host)
        {
            host.Name = ghost;
            host.Initials = "•";
            host.AvatarUrl = null;
            host.Bio = null;
        }
    }

    private async Task<Domain.DataRequests.Blockers> BlockersAsync(int userId, CancellationToken ct)
    {
        // docs/08 §9 — "còn đơn chưa hoàn tất". A finished stay is finished:
        // BlocksDates includes Completed because a past stay still owns its
        // nights, which is a different question from whether anything is
        // outstanding. Using it here would make erasure impossible forever for
        // anybody who has ever travelled.
        var unfinished = await db.Bookings.AnyAsync(
            b => b.GuestUserId == userId
                 && (b.Status == BookingStatus.PendingPayment
                     || b.Status == BookingStatus.PendingHostApproval
                     || b.Status == BookingStatus.Confirmed
                     || b.Status == BookingStatus.InProgress), ct);

        var disputed = await db.ResolutionCases.AnyAsync(
            c => c.OpenedByUserId == userId
                 && (c.Status == ResolutionStatus.AwaitingResponse || c.Status == ResolutionStatus.Disputed), ct);

        // Host-side debt is a column; guest-side debt is a credit ledger that
        // went below zero. Either one blocks erasure the same way.
        var hostOwes = await db.Users
            .Where(u => u.Id == userId && u.HostProfile != null)
            .Select(u => u.HostProfile!.OwedToPlatform)
            .FirstOrDefaultAsync(ct) > 0;

        // docs/01 TC-07 — what they can actually spend, so lapsed balance is not
        // read as money this person still holds.
        var creditBalance = CreditLedger.Available(
            await db.CreditEntries.Where(c => c.UserId == userId).ToListAsync(ct), DateTime.UtcNow);

        var owes = hostOwes || creditBalance < 0;

        var investigated = await db.Users
            .AnyAsync(u => u.Id == userId && (u.IsSuspended || u.IsBanned), ct);

        return new Domain.DataRequests.Blockers(unfinished, disputed, owes, investigated);
    }

    /* ------------------------------------------------------ QT-A-05, §10 */

    /// <summary>docs/08 §10 — the scoreboard and the alarms that read off it.</summary>
    [HttpGet("oversight")]
    public async Task<ActionResult<OversightDto>> Oversight(CancellationToken ct)
    {
        var v = await gate.AllowAsync(AdminAction.ViewOtherAdminLog, null, ct);
        if (!v.Ok) return Refuse(v);

        var now = DateTime.UtcNow;
        var monthAgo = now.AddDays(-30);

        var admins = await db.Users
            .Where(u => u.Role == UserRole.Admin)
            .Select(u => new { u.Id, u.FullName, u.AdminScope, u.AdminLastActiveAt, u.AdminAccessReviewedOn, u.TwoFactorEnabled })
            .ToListAsync(ct);

        var cards = new List<ScorecardDto>();
        var flags = new List<OversightFlagDto>();

        foreach (var a in admins)
        {
            var viewed = await db.AdminProfileViews
                .CountAsync(p => p.AdminUserId == a.Id && p.CreatedAt >= monthAgo, ct);

            var decisions = await db.Sanctions
                .CountAsync(s => s.DecidedByUserId == a.Id && s.CreatedAt >= monthAgo, ct);

            var appealed = await db.Appeals
                .CountAsync(x => x.Sanction!.DecidedByUserId == a.Id && x.Status != AppealStatus.Open, ct);

            var lost = await db.Appeals
                .CountAsync(x => x.Sanction!.DecidedByUserId == a.Id
                                 && (x.Status == AppealStatus.Overturned || x.Status == AppealStatus.Reduced), ct);

            var card = new AdminOversight.Scorecard(a.Id, a.FullName, viewed, decisions, appealed, lost);

            cards.Add(new ScorecardDto(
                a.Id, a.FullName, viewed, decisions, appealed, lost,
                Math.Round(card.OverturnRate * 100, 1),
                AdminOversight.LooksUnreliable(card),
                a.AdminScope.ToString(),
                a.AdminLastActiveAt,
                AdminActions.ScopeLooksUnused(a.AdminLastActiveAt, now),
                AdminActions.AccessReviewDue(a.AdminAccessReviewedOn, DateOnly.FromDateTime(now)),
                a.TwoFactorEnabled));

            if (AdminOversight.LooksUnreliable(card))
            {
                flags.Add(new OversightFlagDto(
                    a.FullName, OversightFlag.AppealsUpheldAgainst.ToString(),
                    AdminOversight.FlagLabel(OversightFlag.AppealsUpheldAgainst),
                    $"{lost}/{appealed} khiếu nại thành công", now));
            }

            // §3 and §10 — looks with no case behind them.
            var untied = await db.AdminProfileViews
                .CountAsync(p => p.AdminUserId == a.Id && p.TicketId == null && p.CreatedAt >= monthAgo, ct);

            if (untied >= AdminOversight.UntiedMonthlyThreshold)
            {
                flags.Add(new OversightFlagDto(
                    a.FullName, OversightFlag.NoTicket.ToString(),
                    AdminOversight.FlagLabel(OversightFlag.NoTicket),
                    $"{untied} lượt xem không gắn hồ sơ nào trong 30 ngày", now));
            }

            // §3 — a lot of profiles in a single hour is browsing, not working.
            var lastHour = await db.AdminProfileViews
                .CountAsync(p => p.AdminUserId == a.Id && p.CreatedAt >= now - AdminOversight.BrowsingWindow, ct);

            if (lastHour >= AdminOversight.BrowsingThreshold)
            {
                flags.Add(new OversightFlagDto(
                    a.FullName, OversightFlag.BrowsingProfiles.ToString(),
                    AdminOversight.FlagLabel(OversightFlag.BrowsingProfiles),
                    $"{lastHour} hồ sơ trong một giờ qua", now));
            }

            // §3 — the same person looked up again and again inside a week.
            var repeat = await db.AdminProfileViews
                .Where(p => p.AdminUserId == a.Id && p.CreatedAt >= now - AdminOversight.RepeatWindow)
                .GroupBy(p => p.TargetUserId)
                .Select(g => new { g.Key, Count = g.Count() })
                .Where(g => g.Count >= AdminOversight.RepeatLookupThreshold)
                .OrderByDescending(g => g.Count)
                .FirstOrDefaultAsync(ct);

            if (repeat is not null)
            {
                flags.Add(new OversightFlagDto(
                    a.FullName, OversightFlag.RepeatLookups.ToString(),
                    AdminOversight.FlagLabel(OversightFlag.RepeatLookups),
                    $"tra cứu người dùng #{repeat.Key} {repeat.Count} lần trong 7 ngày", now));
            }

            // §3 — actions landing outside Vietnamese working hours. Read from
            // the audit trail, because that is where actions live.
            var weekAgo = now.AddDays(-7);
            var nightActions = (await db.AdminAudit
                    .Where(x => x.ActorUserId == a.Id && x.CreatedAt >= weekAgo)
                    .Select(x => x.CreatedAt)
                    .ToListAsync(ct))
                .Count(AdminOversight.IsOutOfHoursUtc);

            if (nightActions > 0)
            {
                flags.Add(new OversightFlagDto(
                    a.FullName, OversightFlag.OutOfHours.ToString(),
                    AdminOversight.FlagLabel(OversightFlag.OutOfHours),
                    $"{nightActions} thao tác ngoài giờ (7:00–20:00, trừ Chủ nhật) trong 7 ngày", now));
            }
        }

        var pending = await db.MoneyApprovals
            .Include(m => m.RequestedByUser)
            .Where(m => m.ApprovedAt == null && m.RejectedAt == null)
            .OrderBy(m => m.RequestedAt)
            .Select(m => new MoneyApprovalDto(
                m.Id, m.Action, m.Target, m.Amount, m.Reason,
                m.RequestedByUser!.FullName, m.RequestedByUserId, m.RequestedAt,
                AdminOversight.MayApprove(v.Admin!.Id, m.RequestedByUserId)))
            .ToListAsync(ct);

        // §10 — the month's random sample, drawn from the decision's own id so it
        // is the same list every time somebody opens the page.
        var sampled = await db.Sanctions
            .Include(s => s.User).Include(s => s.DecidedByUser)
            .Where(s => s.CreatedAt >= monthAgo)
            .OrderByDescending(s => s.CreatedAt)
            .Take(400)
            .ToListAsync(ct);

        var sample = sampled
            .Where(s => AdminOversight.InRandomSample(s.Id))
            .Select(s => new SampledDecisionDto(
                s.Id, s.User!.FullName, Sanctions.Label(s.Level), s.Reason,
                s.DecidedByUser!.FullName, s.CreatedAt))
            .ToList();

        // §5.6 — severe jumps a Super has not yet looked at, 24-hour clock running.
        var severeQueue = (await db.Sanctions
                .Include(s => s.User).Include(s => s.DecidedByUser)
                .Where(s => s.Severe && s.SevereReviewedAt == null)
                .OrderBy(s => s.CreatedAt)
                .ToListAsync(ct))
            .Select(s => new SevereReviewDto(
                s.Id, s.User!.FullName, Sanctions.Label(s.Level), s.Policy, s.Reason,
                s.DecidedByUser!.FullName, s.CreatedAt,
                Sanctions.SevereReviewDueBy(s.CreatedAt),
                now > Sanctions.SevereReviewDueBy(s.CreatedAt)))
            .ToList();

        return Ok(new OversightDto(
            cards, flags, pending, sample,
            AdminOversight.TwoPersonThreshold, AdminOversight.RandomReviewPercent,
            severeQueue));
    }

    /// <summary>
    /// docs/08 §5.6 — "chuyển hồ sơ cho Quản trị tối cao xem lại trong 24 giờ."
    /// The look itself is the deliverable: a Super read the file and signed it.
    /// </summary>
    [HttpPost("sanctions/{id:int}/severe-review")]
    public async Task<ActionResult> SevereReview(int id, [FromBody] RestoreRequest req, CancellationToken ct)
    {
        var admin = await audit.RequireAsync(AdminScope.Super, ct);
        if (admin is null) return this.Denied("Chỉ Quản trị tối cao xem lại hồ sơ §5.6 được.");

        var sanction = await db.Sanctions.FirstOrDefaultAsync(s => s.Id == id && s.Severe, ct);
        if (sanction is null) return NotFound(new { message = "Không tìm thấy hồ sơ nghiêm trọng này." });

        if (sanction.SevereReviewedAt is not null)
            return BadRequest(new { message = "Hồ sơ này đã được xem lại rồi." });

        sanction.SevereReviewedAt = DateTime.UtcNow;
        sanction.SevereReviewedByUserId = admin.Id;

        audit.Record(admin, "sanction.severe-review", $"sanction:{id}",
            "chưa xem lại", "đã xem lại", req.Reason);

        await db.SaveChangesAsync(ct);
        return Ok(new { message = "Đã ghi nhận: hồ sơ nghiêm trọng này đã được Quản trị tối cao xem lại." });
    }

    /* ------------------------------------------------------------ QT-U-13 */

    /// <summary>
    /// docs/08 §11 QT-U-13 — two accounts that turned out to be the same person.
    ///
    /// Nothing is deleted. The one being folded in keeps its row, because every
    /// booking and ledger line points at it and docs/08 §9 is explicit that those
    /// survive. What moves is what belongs to the person rather than to the
    /// history: their stays, their listings, their balance and their saved cards.
    /// The old account is then anonymised and closed, so it cannot be signed into
    /// and cannot be mistaken for a live one.
    /// </summary>
    [HttpPost("users/merge")]
    public async Task<ActionResult<MergeResultDto>> Merge(
        [FromBody] MergeAccountsRequest req, CancellationToken ct)
    {
        var v = await gate.AllowAsync(AdminAction.MergeAccounts, req.Reason, ct, req.FromUserId);
        if (!v.Ok) return Refuse(v);

        if (req.FromUserId == req.IntoUserId)
            return BadRequest(new { message = "Hai tài khoản phải khác nhau." });

        // §1.3 covers both halves of a merge: an admin related to either side
        // must not be the one welding them together.
        var intoConflict = await gate.ConflictAsync(v.Admin!, req.IntoUserId, ct);
        if (AdminConflict.Blocks(intoConflict))
            return StatusCode(403, new { message = AdminConflict.Message(intoConflict) });

        var from = await db.Users.Include(u => u.HostProfile)
            .FirstOrDefaultAsync(u => u.Id == req.FromUserId, ct);
        var into = await db.Users.Include(u => u.HostProfile)
            .FirstOrDefaultAsync(u => u.Id == req.IntoUserId, ct);

        if (from is null || into is null) return NotFound(new { message = "Không tìm thấy tài khoản." });
        if (from.ErasedAt is not null) return BadRequest(new { message = "Tài khoản nguồn đã bị ẩn danh." });

        // Two host profiles cannot become one without deciding whose listings and
        // whose payout account win, which is a business call, not a merge.
        if (from.HostProfile is not null && into.HostProfile is not null)
        {
            return BadRequest(new
            {
                message = "Cả hai tài khoản đều là chủ nhà. Hãy chuyển tin đăng thủ công trước khi hợp nhất."
            });
        }

        var moved = new List<string>();

        var bookings = await db.Bookings.Where(b => b.GuestUserId == from.Id).ToListAsync(ct);
        foreach (var b in bookings) b.GuestUserId = into.Id;
        if (bookings.Count > 0) moved.Add($"{bookings.Count} đơn đặt");

        var credits = await db.CreditEntries.Where(c => c.UserId == from.Id).ToListAsync(ct);
        foreach (var c in credits) c.UserId = into.Id;
        if (credits.Count > 0) moved.Add($"{credits.Count} dòng số dư");

        var cards = await db.SavedCards.Where(c => c.UserId == from.Id).ToListAsync(ct);
        foreach (var c in cards)
        {
            // The receiving account may already have the same card on file.
            var clash = await db.SavedCards.AnyAsync(
                x => x.UserId == into.Id && x.Last4 == c.Last4 && x.Brand == c.Brand
                     && x.ExpiryMonth == c.ExpiryMonth && x.ExpiryYear == c.ExpiryYear, ct);

            if (clash) db.SavedCards.Remove(c);
            else { c.UserId = into.Id; c.IsDefault = false; }
        }

        if (from.HostProfile is { } host)
        {
            host.UserId = into.Id;
            into.HostProfileId = host.Id;
            from.HostProfileId = null;
            into.Role = UserRole.Host;
            moved.Add("hồ sơ chủ nhà và toàn bộ tin đăng");
        }

        var reviews = await db.Reviews.Where(r => r.AuthorUserId == from.Id).ToListAsync(ct);
        foreach (var r in reviews)
        {
            r.AuthorUserId = into.Id;
            // The name is denormalised onto the row; left alone it would keep
            // showing the husk's name on public pages after the husk is wiped.
            r.AuthorName = into.DisplayName ?? into.FullName;
            r.AuthorInitials = into.Initials;
        }
        if (reviews.Count > 0) moved.Add($"{reviews.Count} đánh giá");

        var sanctions = await db.Sanctions.Where(x => x.UserId == from.Id).ToListAsync(ct);
        foreach (var x in sanctions) x.UserId = into.Id;
        if (sanctions.Count > 0) moved.Add($"{sanctions.Count} mục hồ sơ vi phạm");

        // The husk: unreachable, unusable, and still there for the history.
        var before = $"{from.FullName} <{from.Email}>";
        from.FullName = Domain.DataRequests.AnonymisedName(from.Id);
        from.Email = Domain.DataRequests.AnonymisedEmail(from.Id);
        from.Phone = null;
        from.DisplayName = null;
        from.AvatarUrl = null;
        from.IsSuspended = true;
        from.ErasedAt = DateTime.UtcNow;

        foreach (var open in await db.AuthSessions
                     .Where(x => x.UserId == from.Id && x.RevokedAt == null).ToListAsync(ct))
            open.RevokedAt = DateTime.UtcNow;

        audit.Record(v.Admin!, "user.merge", $"user:{from.Id}",
            before, $"đã gộp vào user:{into.Id}", req.Reason);

        await db.SaveChangesAsync(ct);

        await notifications.QueueWithEmailAsync(into, NotificationKind.System,
            "Hai tài khoản StayHost của bạn đã được gộp",
            moved.Count > 0
                ? $"Chúng tôi đã chuyển {string.Join(", ", moved)} sang tài khoản này."
                : "Tài khoản trùng đã được đóng lại; không có dữ liệu nào cần chuyển.",
            "/", ct);

        return Ok(new MergeResultDto(from.Id, into.Id, moved));
    }

    /* ------------------------------------------------------------ QT-A-01 */

    /// <summary>
    /// docs/08 §3 — "Chỉ Quản trị tối cao tạo được tài khoản admin. Không cho tự
    /// đăng ký." The account has to exist already: this grants scopes to a
    /// person, it does not conjure one.
    /// </summary>
    [HttpPost("admins")]
    public async Task<ActionResult<OversightDto>> GrantScopes(
        [FromBody] GrantAdminRequest req, CancellationToken ct)
    {
        var v = await gate.AllowAsync(AdminAction.ManageAdmins, req.Reason, ct, req.UserId);
        if (!v.Ok) return Refuse(v);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == req.UserId, ct);
        if (user is null) return NotFound(new { message = "Không tìm thấy tài khoản." });

        var scope = AdminScope.None;
        foreach (var name in req.Scopes ?? [])
            if (Enum.TryParse<AdminScope>(name, true, out var one)) scope |= one;

        var before = $"{user.Role}/{user.AdminScope}";

        if (scope == AdminScope.None)
        {
            // docs/08 §3 — "Admin nghỉ việc → thu hồi quyền ngay lập tức, huỷ mọi
            // phiên đang mở, không xoá tài khoản (để giữ nhật ký)."
            user.Role = user.HostProfileId is not null ? UserRole.Host : UserRole.Guest;
            user.AdminScope = AdminScope.None;

            foreach (var open in await db.AuthSessions
                         .Where(x => x.UserId == user.Id && x.RevokedAt == null).ToListAsync(ct))
                open.RevokedAt = DateTime.UtcNow;
        }
        else
        {
            user.Role = UserRole.Admin;
            user.AdminScope = scope;
            user.AdminAccessReviewedOn = DateOnly.FromDateTime(DateTime.UtcNow);
        }

        audit.Record(v.Admin!, "admin.scopes", $"user:{user.Id}",
            before, $"{user.Role}/{user.AdminScope}", req.Reason);

        await db.SaveChangesAsync(ct);

        // docs/08 §3 — two-factor is compulsory, so somebody handed a role who has
        // not switched it on is told before they discover it at the sign-in screen.
        if (scope != AdminScope.None && !user.TwoFactorEnabled)
        {
            await notifications.QueueWithEmailAsync(user, NotificationKind.System,
                "Bạn được cấp quyền quản trị StayHost",
                AdminActions.TwoFactorRequiredMessage(), "/account/security", ct);
        }

        return await Oversight(ct);
    }

    /// <summary>docs/08 §3 — the quarterly review, marked as done for one admin.</summary>
    [HttpPost("admins/{id:int}/reviewed")]
    public async Task<ActionResult<OversightDto>> MarkReviewed(
        int id, [FromBody] RestoreRequest req, CancellationToken ct)
    {
        var v = await gate.AllowAsync(AdminAction.ManageAdmins, req.Reason, ct, id);
        if (!v.Ok) return Refuse(v);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return NotFound();

        user.AdminAccessReviewedOn = DateOnly.FromDateTime(DateTime.UtcNow);

        audit.Record(v.Admin!, "admin.access-reviewed", $"user:{id}", null, "đã rà soát", req.Reason);
        await db.SaveChangesAsync(ct);

        return await Oversight(ct);
    }

    /// <summary>docs/08 §10 and §13 scenario 6 — the second signature.</summary>
    [HttpPost("approvals/{id:int}/decide")]
    public async Task<ActionResult> DecideApproval(
        int id, [FromBody] DecideApprovalRequest req, CancellationToken ct)
    {
        var v = await gate.AllowAsync(AdminAction.ManualRefund, req.Reason, ct);
        if (!v.Ok) return Refuse(v);

        var approval = await db.MoneyApprovals.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (approval is null) return NotFound();
        if (!approval.IsOpen) return BadRequest(new { message = "Khoản này đã được xử lý." });

        if (!AdminOversight.MayApprove(v.Admin!.Id, approval.RequestedByUserId))
            return StatusCode(403, new { message = AdminOversight.SelfApprovalMessage() });

        // §1.3 — the second signature is a decision about this money too, so the
        // approver must be a stranger to both sides of the booking it belongs to.
        if (approval.Target.StartsWith("booking:"))
        {
            var reference = approval.Target["booking:".Length..];
            var parties = await db.Bookings
                .Where(b => b.Reference == reference)
                .Select(b => new { b.GuestUserId, HostUserId = (int?)b.Listing!.Host!.UserId })
                .FirstOrDefaultAsync(ct);

            foreach (var party in new[] { parties?.GuestUserId, parties?.HostUserId })
            {
                if (party is not { } partyId) continue;
                var conflict = await gate.ConflictAsync(v.Admin, partyId, ct);
                if (AdminConflict.Blocks(conflict))
                    return StatusCode(403, new { message = AdminConflict.Message(conflict) });
            }
        }

        if (req.Approve)
        {
            approval.ApprovedByUserId = v.Admin.Id;
            approval.ApprovedAt = DateTime.UtcNow;
        }
        else
        {
            approval.RejectedAt = DateTime.UtcNow;
            approval.RejectedReason = req.Reason!.Trim();
        }

        audit.Record(v.Admin, "approval.decide", approval.Target,
            $"{approval.Amount:#,##0}₫ chờ duyệt", req.Approve ? "đã duyệt" : "đã từ chối", req.Reason);

        await db.SaveChangesAsync(ct);

        return Ok(new { message = req.Approve ? "Đã duyệt." : "Đã từ chối." });
    }
}
