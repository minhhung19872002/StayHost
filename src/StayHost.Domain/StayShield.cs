namespace StayHost.Domain;

/// <summary>Which side of the programme a case belongs to (docs/06 §1).</summary>
public enum ShieldSide
{
    Guest = 0,
    Host = 1
}

/// <summary>
/// The situations docs/06 covers. K1–K4 are the guest's, C1–C3 the host's.
/// C4 (third-party liability) was deliberately left out — see docs/06 §10.
/// </summary>
public enum ShieldCase
{
    /// <summary>Host cancelled a confirmed booking inside 30 days of check-in.</summary>
    K1 = 1,
    /// <summary>Guest arrived and could not get in.</summary>
    K2 = 2,
    /// <summary>Seriously different from the listing.</summary>
    K3 = 3,
    /// <summary>Not habitable: filth, pests, no power or water, unsafe.</summary>
    K4 = 4,

    /// <summary>Damage to property.</summary>
    C1 = 11,
    /// <summary>Putting it right: deep cleaning, deodorising, new locks.</summary>
    C2 = 12,
    /// <summary>Income lost because the next booking had to be cancelled.</summary>
    C3 = 13,
    /// <summary>
    /// docs/06 section 3.1 - damage a guest did to somebody who is not on the
    /// booking at all: a neighbour, or the building's shared property. The host
    /// files it, but the money goes to the injured party.
    /// </summary>
    C4 = 14
}

public enum ShieldStatus
{
    /// <summary>Filed and waiting on the other side (24 hours).</summary>
    Open = 0,
    /// <summary>The other side agreed; nothing to arbitrate.</summary>
    Accepted = 1,
    /// <summary>The other side agreed to part of it.</summary>
    PartiallyAccepted = 2,
    /// <summary>The other side objected, or said nothing for 24 hours.</summary>
    UnderReview = 3,
    /// <summary>Decided and paid.</summary>
    Settled = 4,
    Rejected = 5,
    /// <summary>Appealed once, waiting on a different reviewer (docs/06 §6).</summary>
    Appealed = 6
}

/// <summary>How a case was resolved for the guest (docs/06 §2.3, in this order).</summary>
public enum ShieldRemedy
{
    None = 0,
    /// <summary>Level 1 — the platform found somewhere equivalent.</summary>
    Rehoused = 1,
    /// <summary>Level 2 — the guest booked it themselves and sent the receipts.</summary>
    SelfRehoused = 2,
    /// <summary>Level 3 — money back.</summary>
    Refunded = 3
}

/// <summary>
/// docs/06 §10 — the numbers the client fixed on 06/08/2026. One place, so a
/// later change is one edit rather than a search across the codebase.
/// </summary>
public sealed record ShieldSettings
{
    // --- guest side
    /// <summary>K-A: how much of the original booking the platform will cover in rehousing difference.</summary>
    public decimal RehousingTopUpRate { get; init; } = 0.40m;

    /// <summary>K-B: balance handed to the guest when the host walks away.</summary>
    public decimal HostCancelCreditRate { get; init; } = 0.10m;

    /// <summary>K-C: ceiling on travel and one emergency night, per booking.</summary>
    public decimal ExpenseCeiling { get; init; } = 3_000_000m;

    // --- host side
    /// <summary>C-A.</summary>
    public decimal HostClaimCeiling { get; init; } = 75_000_000m;

    /// <summary>C-B.</summary>
    public decimal HostYearlyCeiling { get; init; } = 350_000_000m;

    /// <summary>C-C: the first slice the host carries themselves.</summary>
    public decimal HostDeductible { get; init; } = 500_000m;

    /// <summary>C-D: the most nights of lost income that will be covered.</summary>
    public int LostIncomeNights { get; init; } = 5;

    /// <summary>C-E: per item, and only when it was declared on the listing beforehand.</summary>
    public decimal HighValueItemCeiling { get; init; } = 15_000_000m;

    // --- fund
    /// <summary>Q-A: what the host is paid when nobody was at fault.</summary>
    public decimal ForceMajeureHostRate { get; init; } = 0.25m;

    /// <summary>Q-B: share of service-fee revenue set aside each month.</summary>
    public decimal FundContributionRate { get; init; } = 0.05m;

    /// <summary>Q-C: spending past this share of the month's contribution raises the alarm.</summary>
    public decimal FundAlarmRate { get; init; } = 0.80m;

    /// <summary>A-A: this many cases in a year and every later one is reviewed by hand.</summary>
    public int ClaimsBeforeFlag { get; init; } = 4;

    /// <summary>docs/06 §10 — the desk is staffed around the clock, so K2/K4 keep the one-hour promise.</summary>
    public bool RoundTheClockDesk { get; init; } = true;

    /// <summary>docs/06 §10 — the C4 branch, switched on by the client on 06/08/2026.</summary>
    public bool ThirdPartyBranch { get; init; } = true;

    public static ShieldSettings Current { get; set; } = new();
}

/// <summary>
/// Every rule in docs/06 that can be decided without touching the database.
/// Kept here so the awkward parts — the windows, the waiting periods, the
/// ladder, the deductible, the ceilings — are testable on their own.
/// </summary>
public static class Shield
{
    /* --------------------------------------------------------- §2.1 §3.1 */

    public static ShieldSide SideOf(ShieldCase kind) =>
        kind is ShieldCase.C1 or ShieldCase.C2 or ShieldCase.C3 or ShieldCase.C4
            ? ShieldSide.Host
            : ShieldSide.Guest;

    /// <summary>
    /// docs/06 section 3.1 C4 - the loss belongs to somebody who was never party
    /// to the booking, which changes who is paid and who carries the excess.
    /// </summary>
    public static bool IsThirdParty(ShieldCase kind) => kind == ShieldCase.C4;

    /// <summary>docs/06 §2.1 K1 — a cancellation this close to the stay is the platform's problem too.</summary>
    public static readonly TimeSpan HostCancelWindow = TimeSpan.FromDays(30);

    /// <summary>docs/06 §2.2 — how long a guest has to report K2, K3 or K4.</summary>
    public static readonly TimeSpan GuestReportWindow = TimeSpan.FromHours(72);

    /// <summary>
    /// docs/06 §3.4 — how long a host has after checkout, for the two kinds that
    /// are not about damage the guest could have paid for at the door.
    ///
    /// Lost income (C3) is only knowable once the next booking is cancelled, and
    /// a neighbour's claim (C4) arrives when the neighbour notices. Neither is
    /// something a guest settles in cash on their way out.
    /// </summary>
    public static readonly TimeSpan HostReportWindow = TimeSpan.FromDays(14);

    /// <summary>
    /// docs/06 §3.4 — damage and cleaning have to be raised **while the guest is
    /// still standing there** (customer's rule, 17/08/2026): "chủ nhà phải báo
    /// cho khách lúc khách trả phòng, chứ không phải vài ngày sau mới báo, thì
    /// lúc trả phòng khách trả tiền mặt cho chủ luôn."
    ///
    /// So the platform allows the checkout day and no more. A host who finds the
    /// television broken a week later has lost the only moment when the person
    /// who broke it was in the room — and the guest has lost the only moment
    /// when they could see the damage they are being charged for. The short
    /// window protects both, which is why it is short rather than generous.
    /// </summary>
    public static readonly TimeSpan DamageReportWindow = TimeSpan.FromHours(24);

    /// <summary>
    /// True for the two kinds settled face to face: property damage and the
    /// cleaning it needs. Lost income and a third party's claim are not.
    /// </summary>
    public static bool SettledAtCheckout(ShieldCase kind) =>
        kind is ShieldCase.C1 or ShieldCase.C2;

    /// <summary>How long this kind of case has, which is not the same for all four.</summary>
    public static TimeSpan ReportWindow(ShieldCase kind) =>
        SettledAtCheckout(kind) ? DamageReportWindow : HostReportWindow;

    /// <summary>docs/06 §2.2 — how long to give the host before the platform steps in.</summary>
    public static TimeSpan WaitOnHost(ShieldCase kind) =>
        kind == ShieldCase.K2 ? TimeSpan.FromHours(1) : TimeSpan.FromHours(3);

    /// <summary>docs/06 §6 — how long the other side has to answer.</summary>
    public static readonly TimeSpan ResponseWindow = TimeSpan.FromHours(24);

    /// <summary>docs/06 §6 — one appeal, and only inside a week.</summary>
    public static readonly TimeSpan AppealWindow = TimeSpan.FromDays(7);

    /* ------------------------------------------------------------- §2.2 */

    public enum Refusal
    {
        None = 0,
        WrongSide,
        TooEarly,
        WindowClosed,
        NextGuestArrived,
        HostNotContacted,
        StillWaitingOnHost,
        NoEvidence,
        AlreadyOpen,
        BookedOffPlatform,
        NothingClaimed,
        BranchOff,
        NoThirdParty
    }

    public readonly record struct Check(bool Ok, Refusal Reason, string Message)
    {
        public static Check Pass => new(true, Refusal.None, "");
        public static Check Fail(Refusal reason, string message) => new(false, reason, message);
    }

    /// <summary>Everything the eligibility rules need, gathered by the caller.</summary>
    public sealed record Request
    {
        public required ShieldCase Kind { get; init; }
        public required DateTime Now { get; init; }

        /// <summary>Local check-in moment of the booking, and the checkout after it.</summary>
        public required DateTime CheckInAt { get; init; }
        public required DateTime CheckOutAt { get; init; }

        /// <summary>When the guest first messaged the host about this, in the platform's own inbox.</summary>
        public DateTime? HostContactedAt { get; init; }

        /// <summary>docs/06 §2.2 — no waiting when it is a safety matter or strangers are inside.</summary>
        public bool Urgent { get; init; }

        /// <summary>When the next guest checks in, if there is one (docs/06 §3.4).</summary>
        public DateTime? NextGuestArrivesAt { get; init; }

        /// <summary>docs/06 §3.1 C4 — who was hurt, when it was not the host.</summary>
        public string? ThirdParty { get; init; }

        public int EvidenceCount { get; init; }
        public bool AlreadyHasOpenCase { get; init; }
        public bool PaidThroughPlatform { get; init; } = true;
        public decimal Claimed { get; init; }
    }

    /// <summary>
    /// docs/06 §2.2 and §3.4 — may this case be filed at all. K1 never comes
    /// through here: a host cancellation opens its own case automatically.
    /// </summary>
    public static Check CanFile(Request req)
    {
        if (req.Kind == ShieldCase.K1)
            return Check.Fail(Refusal.WrongSide, "Chủ nhà huỷ được xử lý tự động, không cần mở hồ sơ.");

        if (!req.PaidThroughPlatform)
            return Check.Fail(Refusal.BookedOffPlatform, "Chỉ áp dụng cho đơn đặt và thanh toán qua StayHost.");

        if (req.AlreadyHasOpenCase)
            return Check.Fail(Refusal.AlreadyOpen, "Đơn này đã có một hồ sơ StayShield đang mở.");

        var settings = ShieldSettings.Current;
        return SideOf(req.Kind) == ShieldSide.Guest ? GuestCheck(req) : HostCheck(req, settings);
    }

    private static Check GuestCheck(Request req)
    {
        if (req.Now < req.CheckInAt)
            return Check.Fail(Refusal.TooEarly, "Chỉ mở được từ giờ nhận phòng trở đi.");

        if (req.Now - req.CheckInAt > GuestReportWindow)
            return Check.Fail(Refusal.WindowClosed,
                "Đã quá 72 giờ kể từ giờ nhận phòng. Bạn vẫn có thể mở yêu cầu ở Trung tâm giải quyết.");

        if (req.EvidenceCount < 1)
            return Check.Fail(Refusal.NoEvidence, "Cần ít nhất một ảnh hoặc video làm bằng chứng.");

        // Talking to the host first is the rule, and the record of it has to be
        // in the platform's inbox — docs/06 §2.2 does not count what happens
        // over WhatsApp. Danger and strangers in the property are the exceptions.
        if (req.Urgent) return Check.Pass;

        if (req.HostContactedAt is not { } contacted)
            return Check.Fail(Refusal.HostNotContacted,
                "Hãy nhắn cho chủ nhà trong StayHost trước khi mở hồ sơ.");

        var wait = WaitOnHost(req.Kind);
        return req.Now - contacted >= wait
            ? Check.Pass
            : Check.Fail(Refusal.StillWaitingOnHost,
                $"Vui lòng chờ chủ nhà {wait.TotalHours:0} giờ kể từ lúc bạn nhắn.");
    }

    private static Check HostCheck(Request req, ShieldSettings settings)
    {
        if (IsThirdParty(req.Kind))
        {
            if (!settings.ThirdPartyBranch)
                return Check.Fail(Refusal.BranchOff, "StayHost chưa mở nhánh bồi thường cho bên thứ ba.");

            if (string.IsNullOrWhiteSpace(req.ThirdParty))
                return Check.Fail(Refusal.NoThirdParty, "Cho biết bên bị thiệt hại là ai.");
        }

        if (req.Now < req.CheckOutAt)
            return Check.Fail(Refusal.TooEarly, "Chỉ mở được sau khi khách trả phòng.");

        if (req.Claimed <= 0)
            return Check.Fail(Refusal.NothingClaimed, "Liệt kê ít nhất một khoản thiệt hại.");

        if (req.EvidenceCount < 1)
            return Check.Fail(Refusal.NoEvidence, "Cần ảnh hoặc video hiện trạng hư hỏng.");

        if (req.HostContactedAt is null)
            return Check.Fail(Refusal.HostNotContacted,
                "Hãy nhắn cho khách trong StayHost trước khi mở hồ sơ.");

        // Once somebody else has slept there, nobody can say who did it. That
        // reasoning is about the inside of the property, so it does not apply to
        // a neighbour's car or the building's lobby — a C4 case keeps its
        // fortnight even after the next guest arrives.
        if (!IsThirdParty(req.Kind) && req.NextGuestArrivesAt is { } next && req.Now >= next)
            return Check.Fail(Refusal.NextGuestArrived,
                "Khách tiếp theo đã nhận phòng nên không xác định được ai gây ra.");

        // docs/06 §3.4 — damage is settled face to face on the day, so the window
        // for it is the checkout day and not a fortnight. Lost income and a
        // neighbour's claim keep the fortnight: neither is something a guest
        // could have paid for on their way out.
        var window = ReportWindow(req.Kind);

        return req.Now - req.CheckOutAt <= window
            ? Check.Pass
            : Check.Fail(Refusal.WindowClosed, SettledAtCheckout(req.Kind)
                ? "Hư hỏng phải báo cho khách lúc trả phòng, trong vòng 24 giờ. " +
                  "Quá hạn này thì khách đã đi và không còn ai đối chất được."
                : "Đã quá 14 ngày kể từ ngày khách trả phòng.");
    }

    /* ------------------------------------------------------------- §2.3 */

    /// <summary>What a guest gets, once a case is accepted.</summary>
    public readonly record struct GuestOutcome(
        decimal Refund,
        decimal Credit,
        decimal Expenses,
        decimal RehousingTopUp,
        decimal FromFund,
        decimal FromHost,
        string Summary);

    /// <summary>
    /// docs/06 §2.3 and §4. K1 and K2 return the whole booking including the
    /// service fee; K3 and K4 return the nights the guest did not get. The host
    /// carries the room money either way, the fund carries everything the guest
    /// is out of pocket beyond it.
    /// </summary>
    public static GuestOutcome SettleGuest(
        ShieldCase kind, decimal bookingTotal, decimal hostPayout, int nights, int nightsUnused,
        decimal expensesClaimed, decimal rehousingDifference, ShieldRemedy remedy,
        ShieldSettings? settings = null)
    {
        var s = settings ?? ShieldSettings.Current;
        var safeNights = Math.Max(1, nights);
        var unused = Math.Clamp(nightsUnused, 0, safeNights);

        var wholeBooking = kind is ShieldCase.K1 or ShieldCase.K2;

        var refund = remedy == ShieldRemedy.Rehoused
            ? 0m                                        // the guest kept a stay, so nothing is returned
            : wholeBooking
                ? bookingTotal
                : Round(bookingTotal * unused / safeNights);

        // Expenses and rehousing top-ups only ever apply where the spec allows.
        var expenses = wholeBooking ? Math.Min(Math.Max(0m, expensesClaimed), s.ExpenseCeiling) : 0m;

        var topUp = remedy is ShieldRemedy.Rehoused or ShieldRemedy.SelfRehoused
            ? Math.Min(Math.Max(0m, rehousingDifference), Round(bookingTotal * s.RehousingTopUpRate))
            : 0m;

        var credit = kind == ShieldCase.K1 ? Round(bookingTotal * s.HostCancelCreditRate) : 0m;

        // The host loses their share of what was returned; the platform's fund
        // covers the difference, the credit and the out-of-pocket costs.
        var hostShare = Math.Min(refund, Round(hostPayout * (wholeBooking ? 1m : (decimal)unused / safeNights)));
        var fromFund = refund - hostShare + expenses + topUp + credit;

        var summary = remedy switch
        {
            ShieldRemedy.Rehoused => "Đã chuyển khách sang chỗ tương đương, sàn bù phần chênh lệch.",
            ShieldRemedy.SelfRehoused => "Khách tự tìm chỗ khác, sàn hoàn đơn gốc và bù chênh lệch trong hạn mức.",
            _ => wholeBooking
                ? "Hoàn toàn bộ đơn, kể cả phí dịch vụ."
                : $"Hoàn {unused}/{safeNights} đêm chưa sử dụng."
        };

        return new GuestOutcome(refund, credit, expenses, topUp, fromFund, hostShare, summary);
    }

    /// <summary>What a host gets, and where it comes from.</summary>
    public readonly record struct HostOutcome(
        decimal Approved,
        decimal Deductible,
        decimal FromDeposit,
        decimal FromGuest,
        decimal FromFund,
        decimal TrimmedByCeiling,
        string Summary);

    /// <summary>
    /// docs/06 §3.2 and §3.3. The ceilings bite first, then the host's own
    /// excess, and only what is left is chased — deposit, then the guest, then
    /// the fund. The order is fixed and must not be rearranged.
    /// </summary>
    /// <param name="thirdParty">
    /// docs/06 §3.1 C4. The excess of §3.2 is the host carrying the first slice
    /// of their own loss; here the loss is a neighbour's, so charging the host
    /// for it would be charging them for somebody else's damage. The ceilings
    /// still apply — they are written per claim and per host, not per kind.
    /// </param>
    public static HostOutcome SettleHost(
        decimal claimed, decimal deposit, decimal recoverableFromGuest, decimal alreadyPaidThisYear,
        ShieldSettings? settings = null, bool thirdParty = false)
    {
        var s = settings ?? ShieldSettings.Current;

        var wanted = Math.Max(0m, claimed);
        var perClaim = Math.Min(wanted, s.HostClaimCeiling);
        var yearLeft = Math.Max(0m, s.HostYearlyCeiling - Math.Max(0m, alreadyPaidThisYear));
        var allowed = Math.Min(perClaim, yearLeft);

        var deductible = thirdParty ? 0m : Math.Min(allowed, s.HostDeductible);
        var approved = allowed - deductible;

        var fromDeposit = Math.Min(approved, Math.Max(0m, deposit));
        var fromGuest = Math.Min(approved - fromDeposit, Math.Max(0m, recoverableFromGuest));
        var fromFund = approved - fromDeposit - fromGuest;

        var summary = allowed < wanted
            ? $"Duyệt {Vnd.Format(allowed)} trong yêu cầu {Vnd.Format(wanted)} do chạm hạn mức."
            : $"Duyệt đủ {Vnd.Format(allowed)}.";

        return new HostOutcome(
            approved, deductible, fromDeposit, fromGuest, fromFund, wanted - allowed, summary);
    }

    /// <summary>
    /// docs/06 §3.2 C-E — an expensive item only counts up to the ceiling, and
    /// only if the host declared it on the listing before the guest arrived.
    /// </summary>
    public static decimal AllowedForItem(decimal value, bool declared, ShieldSettings? settings = null)
    {
        var s = settings ?? ShieldSettings.Current;
        if (value <= s.HighValueItemCeiling) return Math.Max(0m, value);
        return declared ? value : s.HighValueItemCeiling;
    }

    /// <summary>docs/06 §3.1 C3 — lost income, capped at the agreed number of nights.</summary>
    public static decimal LostIncome(decimal nightlyRate, int nightsCancelled, ShieldSettings? settings = null)
    {
        var s = settings ?? ShieldSettings.Current;
        return Round(Math.Max(0m, nightlyRate) * Math.Clamp(nightsCancelled, 0, s.LostIncomeNights));
    }

    /// <summary>
    /// docs/06 §8 row "Bất khả kháng" — the guest is refunded in full and the
    /// host, who did nothing wrong and still lost the dates, is compensated
    /// <c>Q-A</c> of the booking value out of the fund.
    ///
    /// "Giá trị đơn" is read as the booking total, the number both sides saw at
    /// checkout, rather than the host's own payout — the parameter was agreed
    /// against the figure on the order, not against a net the guest never sees.
    ///
    /// Nothing here is a claim: no case is opened, no deductible applies, and
    /// the host does not have to ask. It is the one payout in docs/06 that the
    /// platform owes without anybody filing for it.
    /// </summary>
    public static decimal ForceMajeureHostAward(decimal bookingTotal, ShieldSettings? settings = null)
    {
        var s = settings ?? ShieldSettings.Current;
        return Round(Math.Max(0m, bookingTotal) * s.ForceMajeureHostRate);
    }

    /* --------------------------------------------------------------- §6 */

    /// <summary>docs/06 §6 — how fast the platform promised to answer.</summary>
    public static TimeSpan FirstResponseDue(ShieldCase kind, ShieldSettings? settings = null)
    {
        var s = settings ?? ShieldSettings.Current;

        // The one-hour promise only holds because the desk is staffed around the
        // clock; without that it would be a promise nobody could keep.
        if (kind is ShieldCase.K2 or ShieldCase.K4)
            return s.RoundTheClockDesk ? TimeSpan.FromHours(1) : TimeSpan.FromHours(4);

        return SideOf(kind) == ShieldSide.Guest ? TimeSpan.FromHours(4) : TimeSpan.FromHours(24);
    }

    /// <summary>And how long until a decision is owed.</summary>
    public static TimeSpan DecisionDue(ShieldCase kind) => kind switch
    {
        ShieldCase.K2 or ShieldCase.K4 => TimeSpan.FromHours(24),
        ShieldCase.K1 or ShieldCase.K3 => TimeSpan.FromDays(3),
        _ => TimeSpan.FromDays(7)
    };

    /// <summary>Silence for a day sends the case to a person (docs/06 §6).</summary>
    public static bool ResponseLapsed(DateTime openedAt, DateTime now) =>
        now - openedAt >= ResponseWindow;

    public static bool CanAppeal(ShieldStatus status, DateTime? decidedAt, bool alreadyAppealed, DateTime now) =>
        !alreadyAppealed
        && status is ShieldStatus.Settled or ShieldStatus.Rejected
        && decidedAt is { } at
        && now - at <= AppealWindow;

    /* --------------------------------------------------------------- §5 */

    /// <summary>docs/06 §5 — what goes into the fund out of a month's service-fee revenue.</summary>
    public static decimal FundContribution(decimal serviceFeeRevenue, ShieldSettings? settings = null) =>
        Round(Math.Max(0m, serviceFeeRevenue) * (settings ?? ShieldSettings.Current).FundContributionRate);

    /// <summary>True when the month's spending has passed the alarm threshold.</summary>
    public static bool FundAlarm(decimal spentThisMonth, decimal contributedThisMonth,
        ShieldSettings? settings = null)
    {
        var s = settings ?? ShieldSettings.Current;
        if (contributedThisMonth <= 0) return spentThisMonth > 0;
        return spentThisMonth >= contributedThisMonth * s.FundAlarmRate;
    }

    /* --------------------------------------------------------------- §7 */

    /// <summary>
    /// docs/06 §7 — a case from a flagged account never settles itself. §5 adds
    /// one more: an empty fund does not turn anybody away, it sends every new
    /// case to a person instead.
    /// </summary>
    public static bool NeedsManualReview(
        int casesInLastYear, bool flagged, bool fundExhausted = false, ShieldSettings? settings = null) =>
        flagged
        || fundExhausted
        || casesInLastYear >= (settings ?? ShieldSettings.Current).ClaimsBeforeFlag;

    /* ------------------------------------------------------------ words */

    /// <summary>
    /// docs/06 §11 — nothing the user reads may sound like insurance. These are
    /// the only words the interface uses for the programme.
    /// </summary>
    public static string CaseLabel(ShieldCase kind) => kind switch
    {
        ShieldCase.K1 => "Chủ nhà huỷ sát ngày",
        ShieldCase.K2 => "Không vào được chỗ ở",
        ShieldCase.K3 => "Khác xa mô tả",
        ShieldCase.K4 => "Chỗ ở không ở được",
        ShieldCase.C1 => "Hư hỏng tài sản",
        ShieldCase.C2 => "Chi phí khắc phục",
        ShieldCase.C3 => "Mất thu nhập",
        _ => "Thiệt hại cho bên thứ ba"
    };

    public static string StatusLabel(ShieldStatus status) => status switch
    {
        ShieldStatus.Open => "Đang chờ bên kia phản hồi",
        ShieldStatus.Accepted => "Bên kia đã đồng ý",
        ShieldStatus.PartiallyAccepted => "Đồng ý một phần",
        ShieldStatus.UnderReview => "StayHost đang xem xét",
        ShieldStatus.Settled => "Đã xử lý xong",
        ShieldStatus.Rejected => "Không được chấp nhận",
        _ => "Đang xét lại"
    };

    public static string StatusBadge(ShieldStatus status) => status switch
    {
        ShieldStatus.Settled or ShieldStatus.Accepted => "confirmed",
        ShieldStatus.Rejected => "cancelled",
        _ => "pending"
    };

    /// <summary>
    /// The words docs/06 §11 forbids anywhere a user can read. Kept next to the
    /// rules so a new screen has something to check itself against.
    /// </summary>
    public static readonly string[] ForbiddenWords =
    [
        "bảo hiểm", "phí bảo hiểm", "quyền lợi bảo hiểm", "bồi thường bảo hiểm", "người được bảo hiểm"
    ];

    /// <summary>True when a piece of user-facing text strays into insurance language.</summary>
    public static bool ReadsAsInsurance(string? text) =>
        !string.IsNullOrWhiteSpace(text)
        && ForbiddenWords.Any(w => SearchText.Normalize(text).Contains(SearchText.Normalize(w)));

    private static decimal Round(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero);
}
