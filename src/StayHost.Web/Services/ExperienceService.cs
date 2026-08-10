using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Contracts;

namespace StayHost.Web.Services;

/// <summary>
/// docs/01 MR-01 → MR-04. Everything a session does: quoting a seat, selling
/// one, giving the money back, and calling a session off when too few people
/// came forward.
/// </summary>
public class ExperienceService(
    StayHostDbContext db, CatalogService catalog, NotificationService notifications,
    PaymentGateway gateway, ILogger<ExperienceService> log)
{
    /* ------------------------------------------------------------ reading */

    public async Task<ExperienceDetailDto?> DetailAsync(string idOrSlug, CancellationToken ct)
    {
        var query = db.Experiences
            .Include(x => x.Host)
            .Include(x => x.Images)
            .Include(x => x.Slots)
            .AsSplitQuery();

        var experience = int.TryParse(idOrSlug, out var id)
            ? await query.FirstOrDefaultAsync(x => x.Id == id, ct)
            : await query.FirstOrDefaultAsync(x => x.Slug == idOrSlug, ct);

        return experience is null ? null : ToDetail(experience);
    }

    public async Task<IReadOnlyList<ExperienceDetailDto>> MineAsync(int userId, CancellationToken ct)
    {
        var profile = await db.Hosts.FirstOrDefaultAsync(h => h.UserId == userId, ct);
        if (profile is null) return [];

        var mine = await db.Experiences
            .Where(x => x.HostId == profile.Id)
            .Include(x => x.Host).Include(x => x.Images).Include(x => x.Slots)
            .AsSplitQuery()
            .ToListAsync(ct);

        return mine.Select(ToDetail).ToList();
    }

    private static ExperienceDetailDto ToDetail(Experience x)
    {
        var now = DateTime.UtcNow;

        return new ExperienceDetailDto(
            x.Id, x.Slug, x.Title, x.City, x.Country, x.Summary, x.Description,
            x.DurationMinutes, x.MaxGroup, x.MinGuests, x.LanguageList, x.MinAge,
            x.MeetingPoint, x.Latitude, x.Longitude, x.IncludedList,
            x.PricePerPerson, x.PrivateGroupPrice, x.IsPublished,
            x.Rating, x.ReviewCount, x.Host?.Name ?? "", x.Host?.Initials ?? "",
            x.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).ToList(),
            x.Slots
                .Where(s => s.StartsAt > now.AddHours(-x.DurationMinutes / 60.0 - 1))
                .OrderBy(s => s.StartsAt)
                .Select(s => new ExperienceSlotDto(
                    s.Id, s.StartsAt, s.Capacity, s.SeatsTaken, s.SeatsLeft,
                    s.IsPrivate, s.Status.ToString(), s.CancelReason))
                .ToList());
    }

    /* ------------------------------------------------------------ pricing */

    public async Task<ExperienceQuoteDto?> QuoteAsync(int slotId, int seats, bool wantsPrivate, CancellationToken ct)
    {
        var slot = await db.ExperienceSlots
            .Include(s => s.Experience)
            .FirstOrDefaultAsync(s => s.Id == slotId, ct);
        if (slot?.Experience is null) return null;

        var check = ExperienceRules.CanBook(slot.Experience, slot, seats, wantsPrivate, DateTime.UtcNow);
        var price = Pricing.QuoteExperience(new Pricing.ExperienceRequest
        {
            Experience = slot.Experience,
            Seats = seats,
            Private = wantsPrivate,
            StartsAt = slot.StartsAt,
            TaxRules = await catalog.ActiveTaxRulesAsync(ct)
        });

        return new ExperienceQuoteDto(
            slot.Id, slot.StartsAt, price.Seats, wantsPrivate,
            price.PerSeat, price.Subtotal, price.GuestServiceFee, price.Tax, price.Total,
            price.Lines.Select(l => new PriceLineDto(l.Key, l.Label, l.Amount)).ToList(),
            check.Ok, check.Ok ? null : check.Message);
    }

    /* ------------------------------------------------------------ booking */

    public async Task<(ExperienceBooking? Booking, string? Error)> BookAsync(
        User user, int slotId, BookExperienceRequest req, CancellationToken ct)
    {
        var slot = await db.ExperienceSlots
            .Include(s => s.Experience)
            .FirstOrDefaultAsync(s => s.Id == slotId, ct);
        if (slot?.Experience is null) return (null, "Không tìm thấy suất này.");

        var seats = Math.Max(1, req.Seats);
        var check = ExperienceRules.CanBook(slot.Experience, slot, seats, req.Private, DateTime.UtcNow);
        if (!check.Ok) return (null, check.Message);

        var price = Pricing.QuoteExperience(new Pricing.ExperienceRequest
        {
            Experience = slot.Experience,
            Seats = seats,
            Private = req.Private,
            StartsAt = slot.StartsAt,
            TaxRules = await catalog.ActiveTaxRulesAsync(ct)
        });

        // docs/09 §2.7 (scenario 2) — the seats are claimed BEFORE the card is
        // charged, and claimed with one conditional UPDATE so the database itself
        // decides the race: two guests going for the last two seats both pass
        // CanBook above, but only one UPDATE finds the seats still free. The loser
        // is turned away having never been charged.
        var taken = req.Private ? slot.Capacity : seats;

        var claimed = req.Private
            ? await db.ExperienceSlots
                .Where(s => s.Id == slotId && !s.IsPrivate && s.SeatsTaken == 0)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(s => s.SeatsTaken, s => s.Capacity)
                    .SetProperty(s => s.IsPrivate, true), ct)
            : await db.ExperienceSlots
                .Where(s => s.Id == slotId && !s.IsPrivate && s.SeatsTaken + taken <= s.Capacity)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(s => s.SeatsTaken, s => s.SeatsTaken + taken), ct);

        if (claimed == 0)
        {
            // Somebody else got there between the check and the claim. Answer with
            // what is actually left rather than a stale number.
            var left = await db.ExperienceSlots.AsNoTracking()
                .Where(s => s.Id == slotId)
                .Select(s => s.Capacity - s.SeatsTaken)
                .FirstOrDefaultAsync(ct);

            return (null, left > 0
                ? $"Vừa có người đặt trước bạn — chỉ còn {left} chỗ cho suất này."
                : "Vừa có người đặt hết chỗ của suất này.");
        }

        var attempt = gateway.Charge(price.Total, req.PaymentMethod ?? "card", req.CardLast4);
        if (!attempt.Ok)
        {
            // Give the seats straight back; a refused card must not hold a seat.
            if (req.Private)
                await db.ExperienceSlots.Where(s => s.Id == slotId)
                    .ExecuteUpdateAsync(set => set
                        .SetProperty(s => s.SeatsTaken, 0)
                        .SetProperty(s => s.IsPrivate, false), ct);
            else
                await db.ExperienceSlots.Where(s => s.Id == slotId)
                    .ExecuteUpdateAsync(set => set
                        .SetProperty(s => s.SeatsTaken, s => s.SeatsTaken - taken), ct);

            return (null, attempt.Reason);
        }

        var booking = new ExperienceBooking
        {
            Reference = $"XP{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}",
            SlotId = slot.Id,
            GuestUserId = user.Id,
            Seats = seats,
            IsPrivate = req.Private,
            Subtotal = price.Subtotal,
            ServiceFee = price.GuestServiceFee,
            Tax = price.Tax,
            Total = price.Total,
            HostServiceFee = price.HostServiceFee,
            HostPayout = price.HostPayout
        };

        db.ExperienceBookings.Add(booking);
        await db.SaveChangesAsync(ct);

        db.LedgerEntries.AddRange(Ledger.CaptureExperience(booking, price, DateTime.UtcNow));
        await db.SaveChangesAsync(ct);

        await notifications.QueueWithEmailAsync(
            user, NotificationKind.BookingConfirmed,
            "Đã đặt trải nghiệm",
            $"{slot.Experience.Title} · {seats} chỗ · mã {booking.Reference}.",
            "/experiences", ct);
        await db.SaveChangesAsync(ct);

        return (booking, null);
    }

    public async Task<string?> CancelAsync(int userId, int bookingId, CancellationToken ct)
    {
        var booking = await db.ExperienceBookings
            .Include(b => b.Slot!).ThenInclude(s => s.Experience)
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.GuestUserId == userId, ct);
        if (booking is null) return "Không tìm thấy vé này.";
        if (booking.Status != ExperienceBookingStatus.Confirmed) return "Vé này không còn hiệu lực.";

        var refund = ExperienceRules.GuestRefund(booking, booking.Slot!.StartsAt, DateTime.UtcNow);

        await ReleaseAsync(booking, refund, ExperienceBookingStatus.CancelledByGuest,
            refund > 0 ? "Khách huỷ trước 24 giờ." : "Khách huỷ sát giờ, không hoàn tiền.", ct);

        return null;
    }

    /// <summary>Frees the seats, posts the refund, and marks the ticket.</summary>
    private async Task ReleaseAsync(
        ExperienceBooking booking, decimal refund, ExperienceBookingStatus status, string reason,
        CancellationToken ct)
    {
        var slot = booking.Slot!;
        slot.SeatsTaken = Math.Max(0, slot.SeatsTaken - (booking.IsPrivate ? slot.Capacity : booking.Seats));
        if (booking.IsPrivate) slot.IsPrivate = false;

        booking.Status = status;
        booking.RefundedAmount = refund;
        booking.CancelReason = reason;
        booking.CancelledAt = DateTime.UtcNow;

        db.LedgerEntries.AddRange(Ledger.RefundExperience(booking, refund, DateTime.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    /* --------------------------------------------------------- the host */

    public async Task<(int? Id, string? Error)> SaveAsync(User user, SaveExperienceRequest req, CancellationToken ct)
    {
        var profile = await db.Hosts.FirstOrDefaultAsync(h => h.UserId == user.Id, ct);
        if (profile is null) return (null, "Bạn cần có hồ sơ chủ nhà trước.");

        var experience = req.Id is { } id
            ? await db.Experiences.Include(x => x.Images).FirstOrDefaultAsync(x => x.Id == id && x.HostId == profile.Id, ct)
            : new Experience { HostId = profile.Id };
        if (experience is null) return (null, "Không tìm thấy trải nghiệm này.");

        var title = (req.Title ?? "").Trim();
        if (title.Length < 4) return (null, "Tên trải nghiệm quá ngắn.");
        if (req.PricePerPerson <= 0) return (null, "Giá mỗi người phải lớn hơn 0.");
        if (req.MaxGroup < 1) return (null, "Nhóm tối đa phải từ 1 người.");
        if (req.MinGuests < 1 || req.MinGuests > req.MaxGroup)
            return (null, "Số người tối thiểu phải nằm trong nhóm tối đa.");

        experience.Title = title;
        experience.City = (req.City ?? "").Trim();
        experience.Summary = (req.Summary ?? "").Trim();
        experience.Description = (req.Description ?? "").Trim();
        experience.DurationMinutes = Math.Clamp(req.DurationMinutes, 30, 60 * 24);
        experience.MaxGroup = req.MaxGroup;
        experience.MinGuests = req.MinGuests;
        experience.Languages = string.Join(',', req.Languages ?? ["vi"]);
        experience.MinAge = Math.Clamp(req.MinAge, 0, 21);
        experience.MeetingPoint = (req.MeetingPoint ?? "").Trim();
        experience.Latitude = req.Latitude;
        experience.Longitude = req.Longitude;
        experience.Included = string.Join('\n', req.Included ?? []);
        experience.PricePerPerson = req.PricePerPerson;
        experience.PrivateGroupPrice = req.PrivateGroupPrice;

        // docs/09 §2.1–§2.3 — what the activity is decides what it must prove.
        experience.Category = (req.Category ?? "").Trim().ToLowerInvariant();
        experience.AllowsChildren = req.AllowsChildren;
        experience.LicenceName = Trimmed(req.LicenceName);
        experience.LicenceExpiresOn = req.LicenceExpiresOn;
        experience.InsurancePolicy = Trimmed(req.InsurancePolicy);
        experience.InsuranceExpiresOn = req.InsuranceExpiresOn;
        experience.SafetyPlan = Trimmed(req.SafetyPlan);
        experience.EmergencyPhone = Trimmed(req.EmergencyPhone);

        if (experience.Id == 0)
        {
            experience.Slug = Slugify(title);
            db.Experiences.Add(experience);
        }

        if (req.Images is { Count: > 0 })
        {
            db.ExperienceImages.RemoveRange(experience.Images);
            experience.Images = req.Images
                .Select((url, i) => new ExperienceImage { Url = url, SortOrder = i })
                .ToList();
        }

        // docs/09 §2.2 (MR-E-03) — a host does not publish an experience; they
        // submit it, and a reviewer decides. "Publish" therefore means "send to
        // the queue", and the paperwork the risk band demands has to be in first.
        if (req.Publish)
        {
            if (experience.Images.Count == 0) return (null, "Cần ít nhất một ảnh thật của hoạt động trước khi nộp.");

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var missing = ExperienceRules.PublishBlockers(experience, today);
            if (missing.Count > 0)
                return (null, $"Còn thiếu: {string.Join(", ", missing)}.");

            if (experience.ModerationStatus != ExperienceModeration.Approved)
            {
                experience.ModerationStatus = ExperienceModeration.PendingReview;
                experience.SubmittedForReviewAt = DateTime.UtcNow;
                experience.IsPublished = false;
            }
            else
            {
                experience.IsPublished = true;
            }
        }

        experience.RefreshSearchText();
        await db.SaveChangesAsync(ct);

        return (experience.Id, null);
    }

    private static string? Trimmed(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /* ------------------------------------------------- MR-E-03, moderation */

    /// <summary>
    /// docs/09 §2.2 — the queue a reviewer works through, oldest first, with the
    /// risk band and what was submitted so the checklist has something to check.
    /// </summary>
    public async Task<IReadOnlyList<PendingExperienceDto>> ReviewQueueAsync(CancellationToken ct)
    {
        var rows = await db.Experiences
            .Where(x => x.ModerationStatus == ExperienceModeration.PendingReview)
            .OrderBy(x => x.SubmittedForReviewAt)
            .Select(x => new
            {
                x.Id, x.Slug, x.Title, x.City, x.Category, x.AllowsChildren,
                x.LicenceName, x.LicenceExpiresOn, x.InsurancePolicy, x.InsuranceExpiresOn,
                x.SafetyPlan, x.EmergencyPhone, x.SubmittedForReviewAt,
                HostName = x.Host!.Name, HostUserId = x.Host.UserId,
                Cover = x.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault()
            })
            .Take(100)
            .ToListAsync(ct);

        return rows.Select(x =>
        {
            var risk = ExperienceRules.RiskOf(x.Category, x.AllowsChildren);
            return new PendingExperienceDto(
                x.Id, x.Slug, x.Title, x.City, x.Category, ExperienceRules.RiskLabel(risk),
                x.AllowsChildren, x.LicenceName, x.LicenceExpiresOn, x.InsurancePolicy,
                x.InsuranceExpiresOn, x.SafetyPlan, x.EmergencyPhone,
                x.HostName ?? "", x.HostUserId ?? 0, x.Cover, x.SubmittedForReviewAt,
                ExperienceRules.ReviewWorkingDays);
        }).ToList();
    }

    /// <summary>
    /// docs/09 §2.2 — one of three answers: approved, sent back with specific
    /// things to fix, or refused with a reason. A bare "no" is not one of them.
    /// </summary>
    public async Task<string?> ReviewAsync(
        User reviewer, int experienceId, string decision, string? note, CancellationToken ct)
    {
        var x = await db.Experiences
            .Include(e => e.Host!).ThenInclude(h => h.User)
            .Include(e => e.Images)
            .FirstOrDefaultAsync(e => e.Id == experienceId, ct);
        if (x is null) return "Không tìm thấy trải nghiệm này.";

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var trimmed = Trimmed(note);

        switch ((decision ?? "").Trim().ToLowerInvariant())
        {
            case "approve":
                var missing = ExperienceRules.PublishBlockers(x, today);
                if (missing.Count > 0) return $"Chưa duyệt được, còn thiếu: {string.Join(", ", missing)}.";

                x.ModerationStatus = ExperienceModeration.Approved;
                x.IsPublished = true;
                break;

            case "changes":
                if (trimmed is null) return "Cần ghi rõ phải sửa gì.";
                x.ModerationStatus = ExperienceModeration.ChangesRequested;
                x.IsPublished = false;
                break;

            case "reject":
                if (trimmed is null) return "Cần ghi rõ lý do từ chối.";
                x.ModerationStatus = ExperienceModeration.Rejected;
                x.IsPublished = false;
                break;

            default:
                return "Quyết định không hợp lệ.";
        }

        x.ReviewedAt = DateTime.UtcNow;
        x.ReviewedByUserId = reviewer.Id;
        x.ReviewerNote = trimmed;

        if (x.Host?.User is { } owner)
        {
            var (title, body) = x.ModerationStatus switch
            {
                ExperienceModeration.Approved =>
                    ("Trải nghiệm đã được duyệt", $"\"{x.Title}\" đã lên sóng và nhận đặt chỗ được."),
                ExperienceModeration.ChangesRequested =>
                    ("Cần chỉnh lại trải nghiệm", $"\"{x.Title}\": {trimmed}"),
                _ => ("Trải nghiệm bị từ chối", $"\"{x.Title}\": {trimmed}")
            };

            await notifications.QueueWithEmailAsync(owner,
                x.ModerationStatus == ExperienceModeration.Approved
                    ? NotificationKind.ListingApproved
                    : NotificationKind.ListingRejected,
                title, body, "/hosting", ct);
        }

        await db.SaveChangesAsync(ct);
        return null;
    }

    public async Task<string?> AddSlotsAsync(User user, int experienceId, AddSlotsRequest req, CancellationToken ct)
    {
        var experience = await OwnedAsync(user, experienceId, ct);
        if (experience is null) return "Bạn không có quyền với trải nghiệm này.";

        var starts = (req.StartsAt ?? []).Distinct().OrderBy(s => s).Take(60).ToList();
        if (starts.Count == 0) return "Chọn ít nhất một giờ bắt đầu.";

        var existing = await db.ExperienceSlots
            .Where(s => s.ExperienceId == experienceId)
            .Select(s => s.StartsAt)
            .ToListAsync(ct);

        // docs/09 §2.5 (scenario 4) — a session that overlaps one already on the
        // calendar (or another in this same batch) is blocked, so the host cannot
        // promise to be in two places at once. An exact repeat is idempotent.
        var duration = experience.DurationMinutes;
        var accepted = new List<DateTime>();
        foreach (var at in starts)
        {
            var utc = DateTime.SpecifyKind(at, DateTimeKind.Utc);
            if (existing.Contains(utc)) continue;

            if (existing.Any(e => ExperienceRules.Overlaps(utc, e, duration))
                || accepted.Any(a => ExperienceRules.Overlaps(utc, a, duration)))
                return $"Suất {utc:HH:mm dd/MM} chồng giờ với một suất khác (mỗi buổi kéo dài {duration} phút).";

            accepted.Add(utc);
        }

        foreach (var utc in accepted)
        {
            db.ExperienceSlots.Add(new ExperienceSlot
            {
                ExperienceId = experienceId,
                StartsAt = utc,
                Capacity = req.Capacity is > 0 ? Math.Min(req.Capacity.Value, experience.MaxGroup) : experience.MaxGroup
            });
        }

        await db.SaveChangesAsync(ct);
        return null;
    }

    public async Task<string?> CancelSlotAsync(User user, int slotId, string reason, CancellationToken ct)
    {
        var slot = await db.ExperienceSlots
            .Include(s => s.Experience)
            .FirstOrDefaultAsync(s => s.Id == slotId, ct);
        if (slot is null) return "Không tìm thấy suất này.";

        if (await OwnedAsync(user, slot.ExperienceId, ct) is null)
            return "Bạn không có quyền với trải nghiệm này.";

        await CallOffAsync(slot, reason, ct);
        return null;
    }

    private async Task<Experience?> OwnedAsync(User user, int experienceId, CancellationToken ct)
    {
        var profile = await db.Hosts.FirstOrDefaultAsync(h => h.UserId == user.Id, ct);
        return profile is null
            ? null
            : await db.Experiences.FirstOrDefaultAsync(x => x.Id == experienceId && x.HostId == profile.Id, ct);
    }

    /* ---------------------------------------------------------- MR-04 */

    /// <summary>
    /// Calls a session off and refunds everyone on it in full. Used both by the
    /// host and by the sweep that enforces the minimum party size.
    /// </summary>
    public async Task CallOffAsync(ExperienceSlot slot, string reason, CancellationToken ct)
    {
        var tickets = await db.ExperienceBookings
            .Include(b => b.Slot)
            .Include(b => b.GuestUser)
            .Where(b => b.SlotId == slot.Id && b.Status == ExperienceBookingStatus.Confirmed)
            .ToListAsync(ct);

        foreach (var ticket in tickets)
        {
            // Nobody pays for a session that was called off, whatever the reason.
            await ReleaseAsync(ticket, ticket.Total, ExperienceBookingStatus.CancelledWithSlot, reason, ct);

            await notifications.QueueWithEmailAsync(
                ticket.GuestUser, NotificationKind.BookingCancelled,
                "Suất trải nghiệm đã bị huỷ",
                $"{reason} Toàn bộ {ticket.Total:#,##0}₫ đã được hoàn lại.",
                "/experiences", ct);
        }

        slot.Status = SlotStatus.Cancelled;
        slot.CancelReason = reason;
        await db.SaveChangesAsync(ct);

        log.LogInformation("Experience slot {SlotId} called off: {Reason}", slot.Id, reason);
    }

    /// <summary>
    /// docs/01 MR-04 — sessions close to starting without enough people are
    /// called off automatically, and everyone gets their money back.
    /// </summary>
    public async Task<int> SweepAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var horizon = now + ExperienceRules.MinimumCheck;

        var due = await db.ExperienceSlots
            .Include(s => s.Experience)
            .Where(s => s.Status == SlotStatus.Open && s.StartsAt > now && s.StartsAt <= horizon)
            .ToListAsync(ct);

        var called = 0;
        foreach (var slot in due.Where(s => ExperienceRules.ShouldCallOff(s.Experience!, s, now)))
        {
            await CallOffAsync(slot,
                $"Không đủ {slot.Experience!.MinGuests} người tối thiểu nên suất này bị huỷ.", ct);
            called++;
        }

        return called;
    }

    /* ------------------------------------------------------------- DTOs */

    public async Task<ExperienceBookingDto?> BookingDtoAsync(int id, CancellationToken ct) =>
        (await BookingsAsync(b => b.Id == id, ct)).FirstOrDefault();

    public async Task<IReadOnlyList<ExperienceBookingDto>> MyBookingsAsync(int userId, CancellationToken ct) =>
        await BookingsAsync(b => b.GuestUserId == userId, ct);

    private async Task<List<ExperienceBookingDto>> BookingsAsync(
        System.Linq.Expressions.Expression<Func<ExperienceBooking, bool>> where, CancellationToken ct) =>
        await db.ExperienceBookings
            .Where(where)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new ExperienceBookingDto(
                b.Id, b.Reference,
                b.Slot!.ExperienceId, b.Slot.Experience!.Title, b.Slot.Experience.City,
                b.Slot.Experience.Slug, b.Slot.StartsAt, b.Slot.Experience.DurationMinutes,
                b.Seats, b.IsPrivate,
                b.Subtotal, b.ServiceFee, b.Tax, b.Total, b.RefundedAmount,
                b.Status.ToString(),
                ExperienceRules.StatusLabel(b.Status),
                ExperienceRules.StatusBadge(b.Status),
                b.CancelReason, b.CreatedAt))
            .ToListAsync(ct);

    private static string Slugify(string title)
    {
        var normalised = SearchText.Normalize(title);
        var slug = new string(normalised.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return $"{slug.Trim('-')}-{Guid.NewGuid().ToString("N")[..6]}";
    }
}
