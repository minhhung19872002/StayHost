namespace StayHost.Domain;

/// <summary>
/// docs/02 G8 — how a co-host is paid out of what the owner earns.
///
/// The four shapes are the ones the customer asked for on 03/09/2026 ("làm
/// giống Airbnb"): a cut of the cleaning fee, that plus a percentage, a
/// percentage on its own, or a flat amount per booking. Everything here divides
/// money the owner has already earned — nothing in this file can increase what
/// a guest pays or what the platform keeps.
/// </summary>
public enum CoHostPayoutKind
{
    /// <summary>No share. The owner keeps the lot, which is what QL-19 alone described.</summary>
    None = 0,

    /// <summary>The cleaning fee's share of the owner's earnings, and nothing else.</summary>
    CleaningFee = 1,

    /// <summary>The cleaning fee, plus a percentage of everything that is not the cleaning fee.</summary>
    CleaningFeePlusPercent = 2,

    /// <summary>A percentage of the earnings excluding the cleaning fee.</summary>
    Percent = 3,

    /// <summary>A percentage of the earnings including the cleaning fee.</summary>
    PercentWithCleaning = 4,

    /// <summary>A flat amount for each booking, however big the booking was.</summary>
    Fixed = 5
}

/// <summary>
/// Where a co-host's share stands. An owner proposes; the co-host has to say yes
/// before a single đồng is routed away from the owner, because the person being
/// paid is the one who has to declare the income.
/// </summary>
public enum CoHostPayoutStatus
{
    None = 0,
    Proposed = 1,
    Active = 2,
    Declined = 3,
    /// <summary>Nobody answered inside <see cref="CoHostPayouts.ConfirmWindow"/>.</summary>
    Expired = 4
}

/// <summary>
/// docs/02 G8, docs/07 §19 — splitting one booking's host earnings between the
/// owner and the people helping them run the place.
///
/// Two things make this safe to bolt onto a payout engine that has only ever
/// paid one person:
///
///  * It never invents money. The shares are carved out of the owner's earnings
///    for that booking and the remainder is the owner's, so the total debited
///    from <see cref="LedgerAccount.HostPayable"/> is exactly what it was before
///    anybody was invited.
///  * It is computed at the moment of the transfer, not at booking time. A stay
///    that was cancelled down to a fraction pays a fraction — the co-host's
///    percentage follows the money that actually survived, which is the rule
///    Airbnb states in as many words and the one a co-host would otherwise be
///    able to profit from by cancelling.
/// </summary>
public static class CoHostPayouts
{
    /// <summary>
    /// How long a proposal stands. A share of somebody's income is not something
    /// to leave hanging: after this the offer lapses and the owner has to make it
    /// again, so an invitation sent in March cannot quietly start diverting money
    /// in September.
    /// </summary>
    public static readonly TimeSpan ConfirmWindow = TimeSpan.FromDays(14);

    public static DateTime ConfirmBy(DateTime proposedAt) => proposedAt + ConfirmWindow;

    public static bool ProposalExpired(DateTime proposedAt, DateTime now) => now >= ConfirmBy(proposedAt);

    /// <summary>Nobody may be promised more than the whole of a booking.</summary>
    public const decimal MaxPercent = 100m;

    /* ------------------------------------------------------------- the terms */

    /// <summary>One co-host's agreed share, flattened out of the database row.</summary>
    public readonly record struct Terms(
        int CoHostId,
        CoHostPayoutKind Kind,
        decimal Percent = 0m,
        decimal Fixed = 0m);

    /// <summary>What one co-host gets out of one booking, and why.</summary>
    public readonly record struct Share(int CoHostId, decimal Amount, string Basis);

    /// <summary>The whole division of one booking's host earnings.</summary>
    public readonly record struct Split(IReadOnlyList<Share> Shares, decimal ToHost)
    {
        public decimal ToCoHosts => Shares.Sum(s => s.Amount);
    }

    /// <summary>
    /// docs/07 §19 — the owner's earnings, split.
    ///
    /// <paramref name="earnings"/> is the host payout for the booking: the
    /// subtotal less the host service fee, with tax already excluded because tax
    /// was never the host's money. That is the same "potential earnings" figure
    /// the owner sees on their own statement, so a co-host on 20% gets a fifth of
    /// what the owner would otherwise have banked and there is no second
    /// definition of the word to argue about later.
    ///
    /// <paramref name="cleaningShare"/> is the part of those earnings that came
    /// from the cleaning fee — the fee less the service fee withheld on it. It is
    /// passed in rather than derived because only the caller knows the rate that
    /// applied to that booking, and a rate read from today's settings would
    /// quietly repay old bookings at new numbers.
    ///
    /// Order matters and follows Airbnb's: the cleaning-fee claims first (they
    /// name a specific part of the money), then percentages excluding cleaning
    /// highest to lowest, then percentages including it, then flat amounts. Ties
    /// break on the co-host id so two identical terms always resolve the same way
    /// rather than by whatever order the database handed them over.
    ///
    /// Nothing is ever allocated past <paramref name="earnings"/>. A booking that
    /// cannot cover what was promised pays what it can, in that order, and the
    /// owner can be left with nothing — which is Airbnb's stated behaviour, and
    /// the only alternative to inventing money the guest never paid.
    /// </summary>
    public static Split Allocate(decimal earnings, decimal cleaningShare, IEnumerable<Terms> terms)
    {
        var pot = Math.Max(0m, earnings);
        var cleaning = Math.Clamp(cleaningShare, 0m, pot);
        var roomPart = pot - cleaning;

        var shares = new List<Share>();
        var left = pot;

        foreach (var t in Ordered(terms))
        {
            if (left <= 0) break;

            var (want, basis) = Wanted(t, pot, roomPart, cleaning);
            var amount = Math.Min(Round(want), left);
            if (amount <= 0) continue;

            shares.Add(new Share(t.CoHostId, amount, basis));
            left -= amount;
        }

        return new Split(shares, left);
    }

    /// <summary>
    /// The claims in the order they are honoured. Kept separate from
    /// <see cref="Allocate"/> so the ordering can be tested on its own — it is the
    /// part that decides who goes short when a booking cannot cover everyone.
    /// </summary>
    public static IEnumerable<Terms> Ordered(IEnumerable<Terms> terms) =>
        terms
            .Where(t => t.Kind != CoHostPayoutKind.None)
            .OrderBy(t => Rank(t.Kind))
            .ThenByDescending(t => t.Kind == CoHostPayoutKind.Fixed ? t.Fixed : t.Percent)
            .ThenBy(t => t.CoHostId);

    private static int Rank(CoHostPayoutKind kind) => kind switch
    {
        CoHostPayoutKind.CleaningFee => 0,
        CoHostPayoutKind.CleaningFeePlusPercent => 1,
        CoHostPayoutKind.Percent => 2,
        CoHostPayoutKind.PercentWithCleaning => 3,
        _ => 4
    };

    private static (decimal Amount, string Basis) Wanted(
        Terms t, decimal pot, decimal roomPart, decimal cleaning) => t.Kind switch
    {
        CoHostPayoutKind.CleaningFee =>
            (cleaning, "Phí dọn dẹp"),

        CoHostPayoutKind.CleaningFeePlusPercent =>
            (cleaning + roomPart * Rate(t.Percent),
                $"Phí dọn dẹp + {Trim(t.Percent)}% phần còn lại"),

        CoHostPayoutKind.Percent =>
            (roomPart * Rate(t.Percent), $"{Trim(t.Percent)}% (không gồm phí dọn dẹp)"),

        CoHostPayoutKind.PercentWithCleaning =>
            (pot * Rate(t.Percent), $"{Trim(t.Percent)}% (gồm cả phí dọn dẹp)"),

        CoHostPayoutKind.Fixed =>
            (t.Fixed, $"{Vnd.Format(t.Fixed)} mỗi đơn"),

        _ => (0m, "")
    };

    private static decimal Rate(decimal percent) => Math.Clamp(percent, 0m, MaxPercent) / 100m;

    /// <summary>Đồng has no minor unit, and a payout file that carries one is refused.</summary>
    private static decimal Round(decimal amount) =>
        Math.Round(Math.Max(0m, amount), 0, MidpointRounding.AwayFromZero);

    private static string Trim(decimal percent) => percent.ToString("0.##");

    /* ----------------------------------------------------- the cleaning share */

    /// <summary>
    /// The part of a booking's host earnings that came from the cleaning fee.
    ///
    /// The host service fee is withheld on the whole subtotal, cleaning fee
    /// included, so the fee does not reach the host whole and a co-host paid "the
    /// cleaning fee" cannot be paid more of it than the owner received. Charging
    /// the gross fee against net earnings is how a 100% cleaning-fee co-host ends
    /// up taking a slice of the room revenue too.
    /// </summary>
    public static decimal CleaningShare(decimal cleaningFee, decimal subtotal, decimal hostPayout)
    {
        if (cleaningFee <= 0 || subtotal <= 0 || hostPayout <= 0) return 0m;

        var share = Round(hostPayout * Math.Min(cleaningFee, subtotal) / subtotal);
        return Math.Min(share, hostPayout);
    }

    /* ----------------------------------------------------------- what to say */

    /// <summary>
    /// The four shapes an owner picks between, and which box each one needs
    /// filled in. Ordered the way the screen offers them: the cleaning fee first
    /// because it is the arrangement most people already have informally.
    /// </summary>
    public static readonly (CoHostPayoutKind Kind, string Key, bool NeedsPercent, bool NeedsAmount)[] All =
    [
        (CoHostPayoutKind.CleaningFee, "cleaning", false, false),
        (CoHostPayoutKind.CleaningFeePlusPercent, "cleaning-plus-percent", true, false),
        (CoHostPayoutKind.Percent, "percent", true, false),
        (CoHostPayoutKind.PercentWithCleaning, "percent-with-cleaning", true, false),
        (CoHostPayoutKind.Fixed, "fixed", false, true)
    ];

    public static string Key(CoHostPayoutKind kind) =>
        All.FirstOrDefault(k => k.Kind == kind).Key ?? "none";

    public static CoHostPayoutKind Parse(string? key) =>
        All.FirstOrDefault(k => k.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Kind;

    public static string StatusKey(CoHostPayoutStatus status) => status.ToString().ToLowerInvariant();

    public static string KindLabel(CoHostPayoutKind kind) => kind switch
    {
        CoHostPayoutKind.CleaningFee => "Phí dọn dẹp",
        CoHostPayoutKind.CleaningFeePlusPercent => "Phí dọn dẹp + phần trăm",
        CoHostPayoutKind.Percent => "Phần trăm (không gồm phí dọn dẹp)",
        CoHostPayoutKind.PercentWithCleaning => "Phần trăm (gồm cả phí dọn dẹp)",
        CoHostPayoutKind.Fixed => "Số tiền cố định mỗi đơn",
        _ => "Không chia thu nhập"
    };

    /// <summary>The terms in one line, for a list where the owner scans several at once.</summary>
    public static string Describe(CoHostPayoutKind kind, decimal percent, decimal fixedAmount) => kind switch
    {
        CoHostPayoutKind.CleaningFee => "Toàn bộ phí dọn dẹp",
        CoHostPayoutKind.CleaningFeePlusPercent => $"Phí dọn dẹp + {Trim(percent)}% phần còn lại",
        CoHostPayoutKind.Percent => $"{Trim(percent)}% mỗi đơn (không gồm phí dọn dẹp)",
        CoHostPayoutKind.PercentWithCleaning => $"{Trim(percent)}% mỗi đơn (gồm cả phí dọn dẹp)",
        CoHostPayoutKind.Fixed => $"{Vnd.Format(fixedAmount)} mỗi đơn",
        _ => "Không chia thu nhập"
    };

    public static string StatusLabel(CoHostPayoutStatus status) => status switch
    {
        CoHostPayoutStatus.Proposed => "Chờ người đồng quản lý xác nhận",
        CoHostPayoutStatus.Active => "Đang áp dụng",
        CoHostPayoutStatus.Declined => "Đã từ chối",
        CoHostPayoutStatus.Expired => "Đề nghị đã quá hạn",
        _ => "Không chia thu nhập"
    };

    /// <summary>
    /// Whether these terms make sense before they are ever offered to anybody.
    /// Said as a sentence rather than a boolean: an owner typing 150% deserves to
    /// be told which number is wrong.
    /// </summary>
    public static string? Invalid(CoHostPayoutKind kind, decimal percent, decimal fixedAmount)
    {
        if (kind == CoHostPayoutKind.None) return null;

        var needsPercent = kind is CoHostPayoutKind.Percent
            or CoHostPayoutKind.PercentWithCleaning or CoHostPayoutKind.CleaningFeePlusPercent;

        if (needsPercent)
        {
            if (percent <= 0) return "Nhập phần trăm lớn hơn 0.";
            if (percent > MaxPercent) return $"Phần trăm không vượt quá {MaxPercent:0}%.";
        }

        if (kind == CoHostPayoutKind.Fixed)
        {
            if (fixedAmount <= 0) return "Nhập số tiền lớn hơn 0.";
            if (fixedAmount > 500_000_000m) return "Số tiền mỗi đơn quá lớn.";
        }

        return null;
    }

    /// <summary>
    /// docs/07 §19 — an owner cannot promise away more than they will earn. This
    /// is a warning at the moment of proposing, not a refusal: the shares are
    /// capped at payout time anyway, and a percentage that only overshoots on a
    /// one-night booking is a perfectly ordinary arrangement.
    /// </summary>
    public static decimal Overcommitted(IEnumerable<Terms> terms)
    {
        var total = terms
            .Where(t => t.Kind is CoHostPayoutKind.Percent
                or CoHostPayoutKind.PercentWithCleaning or CoHostPayoutKind.CleaningFeePlusPercent)
            .Sum(t => t.Percent);

        return total > MaxPercent ? total : 0m;
    }

    /* -------------------------------------------------------- what to tell them */

    public static string ProposalNotice(string ownerName, string what, DateTime proposedAt) =>
        $"{ownerName} đề nghị chia cho bạn {what} từ mỗi đơn đặt. " +
        $"Bạn cần xác nhận trước {ConfirmBy(proposedAt):HH:mm dd/MM/yyyy}, sau đó đề nghị sẽ hết hạn.";

    public static string ConfirmedNotice(string coHostName, string what) =>
        $"{coHostName} đã xác nhận nhận {what} từ mỗi đơn đặt. " +
        "Khoản này được trừ vào thu nhập của bạn và chuyển thẳng cho họ.";

    public static string DeclinedNotice(string coHostName) =>
        $"{coHostName} đã từ chối đề nghị chia thu nhập. Bạn vẫn nhận toàn bộ thu nhập như trước.";

    public static string ExpiredNotice(string coHostName) =>
        $"Đề nghị chia thu nhập cho {coHostName} đã quá {ConfirmWindow.TotalDays:0} ngày mà chưa được xác nhận, " +
        "nên đã hết hiệu lực. Bạn có thể đề nghị lại.";

    /// <summary>
    /// Said to the co-host when a booking they were paid for is refunded after the
    /// fact. It names the mechanism, because "bạn đang nợ sàn" with no explanation
    /// is how somebody concludes their money was taken.
    /// </summary>
    public static string ClawbackNotice(decimal amount, string reference) =>
        $"Đơn {reference} đã được hoàn tiền sau khi bạn nhận phần chia. " +
        $"{Vnd.Format(amount)} sẽ được trừ vào các lần chuyển tiền tiếp theo của bạn.";

    /// <summary>Said to the owner on the transfer that no longer carries the whole earnings.</summary>
    public static string DeductionNote(decimal toCoHosts, int people) =>
        people == 1
            ? $"Đã chia {Vnd.Format(toCoHosts)} cho người đồng quản lý."
            : $"Đã chia {Vnd.Format(toCoHosts)} cho {people} người đồng quản lý.";
}
