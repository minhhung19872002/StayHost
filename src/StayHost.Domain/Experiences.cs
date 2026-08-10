namespace StayHost.Domain;

public enum SlotStatus
{
    Open = 0,
    /// <summary>Called off, either by the host or because too few people came forward.</summary>
    Cancelled = 1
}

public enum ExperienceBookingStatus
{
    Confirmed = 0,
    CancelledByGuest = 1,
    /// <summary>The whole session was called off; everyone was refunded.</summary>
    CancelledWithSlot = 2,
    Completed = 3
}

/// <summary>
/// docs/00 §2 and docs/01 MR-01 — something a local runs, sold by the seat
/// rather than by the night.
/// </summary>
public class Experience
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string City { get; set; } = "";
    public string Country { get; set; } = "Việt Nam";

    public int HostId { get; set; }
    public HostProfile? Host { get; set; }

    public string Summary { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>How long one session runs.</summary>
    public int DurationMinutes { get; set; } = 120;

    /// <summary>Seats in one session, and the fewest that make it worth running.</summary>
    public int MaxGroup { get; set; } = 10;
    public int MinGuests { get; set; } = 2;

    /// <summary>Comma-separated language codes the host runs it in.</summary>
    public string Languages { get; set; } = "vi";
    public int MinAge { get; set; }

    public string MeetingPoint { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    /// <summary>One item per line: what a ticket covers.</summary>
    public string Included { get; set; } = "";

    public decimal PricePerPerson { get; set; }

    /// <summary>
    /// docs/01 MR-03 — one price for the whole session with nobody else on it.
    /// Null means the host does not offer that.
    /// </summary>
    public decimal? PrivateGroupPrice { get; set; }

    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";
    public bool IsPublished { get; set; }

    public double Rating { get; set; }
    public int ReviewCount { get; set; }

    public string SearchText { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ExperienceImage> Images { get; set; } = [];
    public List<ExperienceSlot> Slots { get; set; } = [];

    public void RefreshSearchText() =>
        SearchText = StayHost.Domain.SearchText.Normalize(
            string.Join(' ', Title, City, Country, Summary, Description, MeetingPoint));

    public IReadOnlyList<string> LanguageList =>
        Languages.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public IReadOnlyList<string> IncludedList =>
        Included.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public class ExperienceImage
{
    public int Id { get; set; }
    public int ExperienceId { get; set; }
    public Experience? Experience { get; set; }
    public string Url { get; set; } = "";
    public string? Caption { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>docs/01 MR-02 — one session at one time, with seats that run out.</summary>
public class ExperienceSlot
{
    public int Id { get; set; }
    public int ExperienceId { get; set; }
    public Experience? Experience { get; set; }

    /// <summary>The local start, stored as UTC.</summary>
    public DateTime StartsAt { get; set; }

    public int Capacity { get; set; }
    public int SeatsTaken { get; set; }

    /// <summary>Set when one booking took the session for itself (docs/01 MR-03).</summary>
    public bool IsPrivate { get; set; }

    public SlotStatus Status { get; set; } = SlotStatus.Open;
    public string? CancelReason { get; set; }

    public int SeatsLeft => Math.Max(0, Capacity - SeatsTaken);
}

public class ExperienceBooking
{
    public int Id { get; set; }
    public string Reference { get; set; } = "";

    public int SlotId { get; set; }
    public ExperienceSlot? Slot { get; set; }

    public int GuestUserId { get; set; }
    public User? GuestUser { get; set; }

    public int Seats { get; set; } = 1;
    public bool IsPrivate { get; set; }

    // The priced ticket, frozen at booking time like every other receipt here.
    public decimal Subtotal { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public decimal HostServiceFee { get; set; }
    public decimal HostPayout { get; set; }

    public ExperienceBookingStatus Status { get; set; } = ExperienceBookingStatus.Confirmed;
    public decimal RefundedAmount { get; set; }
    public string? CancelReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledAt { get; set; }

    // docs/09 §4 (MR-C-03) — the host is paid a day after the session ends.
    public PayoutStatus PayoutStatus { get; set; } = PayoutStatus.Scheduled;
    public DateTime? PaidOutAt { get; set; }
    public string? PayoutReference { get; set; }
}

/// <summary>
/// The rules a session obeys. Kept apart from storage so the awkward cases —
/// too few people, a private booking on a shared session — are testable.
/// </summary>
public static class ExperienceRules
{
    /// <summary>
    /// docs/01 MR-04 — how long before a session starts the decision is made on
    /// whether enough people are coming.
    /// </summary>
    public static readonly TimeSpan MinimumCheck = TimeSpan.FromHours(48);

    /// <summary>
    /// docs/09 §2.8 — the experience cancellation ladder, its own and not the stay
    /// policy: a full refund a week out, half inside the week, nothing in the last
    /// day, plus a 24-hour grace right after booking while the session is ≥48h off.
    /// </summary>
    public static readonly TimeSpan FullRefundLead = TimeSpan.FromDays(7);
    public static readonly TimeSpan HalfRefundLead = TimeSpan.FromHours(24);
    public static readonly TimeSpan GraceWindow = TimeSpan.FromHours(24);
    public static readonly TimeSpan GraceLead = TimeSpan.FromHours(48);

    public enum Refusal
    {
        None = 0,
        SlotCancelled,
        AlreadyStarted,
        NotEnoughSeats,
        PrivateNotOffered,
        PrivateNeedsEmptySlot,
        BelowMinimumParty
    }

    public readonly record struct Check(bool Ok, Refusal Reason, string Message)
    {
        public static Check Pass => new(true, Refusal.None, "");
        public static Check Fail(Refusal reason, string message) => new(false, reason, message);
    }

    public static Check CanBook(Experience experience, ExperienceSlot slot, int seats, bool wantsPrivate, DateTime now)
    {
        if (slot.Status == SlotStatus.Cancelled)
            return Check.Fail(Refusal.SlotCancelled, "Suất này đã bị huỷ.");

        if (slot.StartsAt <= now)
            return Check.Fail(Refusal.AlreadyStarted, "Suất này đã bắt đầu.");

        if (seats < 1)
            return Check.Fail(Refusal.BelowMinimumParty, "Chọn ít nhất một chỗ.");

        if (wantsPrivate)
        {
            if (experience.PrivateGroupPrice is null)
                return Check.Fail(Refusal.PrivateNotOffered, "Trải nghiệm này không nhận nhóm riêng.");

            // A private booking takes the whole session, so it cannot join one
            // that already has other people on it.
            if (slot.SeatsTaken > 0)
                return Check.Fail(Refusal.PrivateNeedsEmptySlot, "Suất này đã có người đặt nên không thuê riêng được.");

            return seats <= slot.Capacity
                ? Check.Pass
                : Check.Fail(Refusal.NotEnoughSeats, $"Nhóm riêng tối đa {slot.Capacity} người.");
        }

        if (slot.IsPrivate)
            return Check.Fail(Refusal.PrivateNeedsEmptySlot, "Suất này đã được thuê riêng.");

        return seats <= slot.SeatsLeft
            ? Check.Pass
            : Check.Fail(Refusal.NotEnoughSeats, $"Chỉ còn {slot.SeatsLeft} chỗ cho suất này.");
    }

    /// <summary>
    /// docs/01 MR-04 — a session inside the decision window without enough
    /// people is called off, and everyone on it is refunded in full.
    /// </summary>
    public static bool ShouldCallOff(Experience experience, ExperienceSlot slot, DateTime now) =>
        slot.Status == SlotStatus.Open
        && !slot.IsPrivate
        && slot.StartsAt > now
        && slot.StartsAt - now <= MinimumCheck
        && slot.SeatsTaken < experience.MinGuests;

    /// <summary>
    /// docs/09 §2.5 (scenario 4) — two sessions of the same experience clash when
    /// their windows touch. Every session runs the same length, so two starts
    /// closer together than that duration overlap. An exact repeat of an existing
    /// start is the same session, not a clash, so the caller screens those first.
    /// </summary>
    public static bool Overlaps(DateTime a, DateTime b, int durationMinutes) =>
        (a - b).Duration() < TimeSpan.FromMinutes(durationMinutes);

    /// <summary>
    /// docs/09 §2.8 — a guest cancelling their own ticket, on the tiered ladder:
    /// ≥7 days 100%, 24h–7 days 50%, &lt;24h nothing; and the 24-hour grace after
    /// booking (while the session is still ≥48h away) returns everything.
    /// </summary>
    public static decimal GuestRefund(ExperienceBooking booking, DateTime startsAt, DateTime now)
    {
        var lead = startsAt - now;

        var withinGrace = now >= booking.CreatedAt
            && now - booking.CreatedAt <= GraceWindow
            && lead >= GraceLead;

        if (withinGrace || lead >= FullRefundLead)
            return booking.Total;

        if (lead >= HalfRefundLead)
            return Math.Round(booking.Total * 0.5m, 0, MidpointRounding.AwayFromZero);

        return 0m;
    }

    public static string StatusLabel(ExperienceBookingStatus status) => status switch
    {
        ExperienceBookingStatus.Confirmed => "Đã xác nhận",
        ExperienceBookingStatus.CancelledByGuest => "Khách đã huỷ",
        ExperienceBookingStatus.CancelledWithSlot => "Suất bị huỷ",
        _ => "Đã hoàn tất"
    };

    public static string StatusBadge(ExperienceBookingStatus status) => status switch
    {
        ExperienceBookingStatus.Confirmed => "confirmed",
        ExperienceBookingStatus.Completed => "confirmed",
        _ => "cancelled"
    };
}
