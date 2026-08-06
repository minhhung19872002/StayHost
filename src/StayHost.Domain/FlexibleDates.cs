namespace StayHost.Domain;

/// <summary>
/// How long the guest wants to stay when they have not fixed the dates
/// (docs/01 TM-06 and TM-07).
/// </summary>
public enum StayLength
{
    /// <summary>Two given dates, optionally shifted a few days either way.</summary>
    Exact = 0,
    /// <summary>A Friday to Sunday, whichever weekend has room.</summary>
    Weekend = 1,
    Week = 2,
    Month = 3,
    /// <summary>Several whole months, starting on the first of a chosen month.</summary>
    Months = 4
}

/// <summary>One candidate stay. Half-open: the last night is <c>CheckOut - 1</c>.</summary>
public readonly record struct StayWindow(DateOnly CheckIn, DateOnly CheckOut)
{
    public int Nights => CheckOut.DayNumber - CheckIn.DayNumber;

    public bool Overlaps(DateOnly from, DateOnly to) => CheckIn < to && from < CheckOut;
}

/// <summary>What the guest typed into the flexible date picker.</summary>
public sealed record FlexibleRequest
{
    public StayLength Length { get; init; } = StayLength.Exact;
    public DateOnly? CheckIn { get; init; }
    public DateOnly? CheckOut { get; init; }

    /// <summary>The "± 1–7 ngày" of TM-06. Zero means the dates are firm.</summary>
    public int FlexDays { get; init; }

    /// <summary>TM-07 — how many whole months the stay runs for.</summary>
    public int Months { get; init; }

    /// <summary>TM-07 — the months the stay may start in; any date inside a month counts.</summary>
    public IReadOnlyList<DateOnly> StartMonths { get; init; } = [];

    public bool IsFlexible => Length != StayLength.Exact || FlexDays > 0;
}

/// <summary>
/// Turns a loose wish — "a week sometime next month", "these dates give or take
/// three days", "two months from October" — into the handful of concrete stays
/// worth checking. Search then keeps a listing if any one of them is free.
/// </summary>
public static class FlexibleDates
{
    /// <summary>The spec allows ± 1–7 days and no more.</summary>
    public const int MaxShift = 7;

    /// <summary>Enough candidates to be useful, few enough to stay one query.</summary>
    public const int MaxWindows = 16;

    /// <summary>How far ahead "a week, whenever" looks when no date was given.</summary>
    public const int OpenHorizonDays = 30;

    public const int MaxMonths = 12;

    public static int NightsOf(StayLength length) => length switch
    {
        StayLength.Weekend => 2,
        StayLength.Week => 7,
        StayLength.Month => 30,
        _ => 0
    };

    public static string Label(StayLength length) => length switch
    {
        StayLength.Weekend => "Cuối tuần",
        StayLength.Week => "Một tuần",
        StayLength.Month => "Một tháng",
        StayLength.Months => "Theo tháng",
        _ => "Ngày cụ thể"
    };

    public static IReadOnlyList<StayWindow> Windows(FlexibleRequest req, DateOnly today)
    {
        var windows = req.Length switch
        {
            StayLength.Exact => Exact(req, today),
            StayLength.Months => ByMonth(req, today),
            _ => ByLength(req, today)
        };

        var kept = windows
            .Where(w => w.Nights > 0 && w.CheckIn >= today)
            .DistinctBy(w => (w.CheckIn, w.CheckOut));

        // Shifted firm dates keep their nearest-first order — a guest who said
        // "give or take five days" wants their own dates whenever they are free.
        // Everything else reads as a calendar, so it runs forwards.
        if (req.Length != StayLength.Exact) kept = kept.OrderBy(w => w.CheckIn);

        return kept.Take(MaxWindows).ToList();
    }

    /// <summary>
    /// Two firm dates slide together, so every candidate is the same length as
    /// the stay the guest asked for — "± 3 ngày" must not silently sell them a
    /// shorter trip.
    /// </summary>
    private static IEnumerable<StayWindow> Exact(FlexibleRequest req, DateOnly today)
    {
        if (req.CheckIn is not { } checkIn || req.CheckOut is not { } checkOut || checkOut <= checkIn)
            return [];

        var shift = Math.Clamp(req.FlexDays, 0, MaxShift);
        if (shift == 0) return [new StayWindow(checkIn, checkOut)];

        // Nearest first: a guest who says "give or take three days" would rather
        // have their own dates than the edge of the range.
        return Enumerable.Range(0, shift * 2 + 1)
            .Select(i => i % 2 == 0 ? i / 2 : -(i / 2 + 1))
            .Select(d => new StayWindow(checkIn.AddDays(d), checkOut.AddDays(d)))
            .Where(w => w.CheckIn >= today);
    }

    /// <summary>A weekend, a week or a month, anywhere inside the searched span.</summary>
    private static IEnumerable<StayWindow> ByLength(FlexibleRequest req, DateOnly today)
    {
        var nights = NightsOf(req.Length);
        if (nights == 0) return [];

        var anchor = req.CheckIn is { } given && given > today ? given : today;
        var shift = Math.Clamp(req.FlexDays, 0, MaxShift);

        // No date and no flex means "whenever" — look a month ahead rather than
        // returning a single arbitrary window.
        var (from, to) = shift > 0
            ? (Later(anchor.AddDays(-shift), today), anchor.AddDays(shift))
            : (anchor, anchor.AddDays(OpenHorizonDays));

        var starts = Enumerable
            .Range(0, Math.Max(1, to.DayNumber - from.DayNumber + 1))
            .Select(from.AddDays);

        if (req.Length == StayLength.Weekend)
            starts = starts.Where(d => d.DayOfWeek == DayOfWeek.Friday);

        return starts.Select(d => new StayWindow(d, d.AddDays(nights)));
    }

    /// <summary>
    /// docs/01 TM-07 — N whole months, starting on the first of each month the
    /// guest picked. With no months picked, offer the next three.
    /// </summary>
    private static IEnumerable<StayWindow> ByMonth(FlexibleRequest req, DateOnly today)
    {
        var months = Math.Clamp(req.Months, 1, MaxMonths);

        // A month already under way cannot be started from its first, so the
        // default run of months begins with the next one.
        var starts = req.StartMonths.Count > 0
            ? req.StartMonths.Select(FirstOfMonth)
            : Enumerable.Range(1, 3).Select(i => FirstOfMonth(today).AddMonths(i));

        return starts.Select(start => new StayWindow(start, start.AddMonths(months)));
    }

    /// <summary>The first candidate nothing is sitting on, or null when the listing is full.</summary>
    public static StayWindow? FirstFree(
        IReadOnlyList<StayWindow> windows, IReadOnlyCollection<(DateOnly From, DateOnly To)> occupied)
    {
        foreach (var w in windows)
        {
            if (!occupied.Any(o => w.Overlaps(o.From, o.To))) return w;
        }
        return null;
    }

    private static DateOnly FirstOfMonth(DateOnly d) => new(d.Year, d.Month, 1);

    private static DateOnly Later(DateOnly a, DateOnly b) => a > b ? a : b;
}
