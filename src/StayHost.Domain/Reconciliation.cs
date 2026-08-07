namespace StayHost.Domain;

/// <summary>What a day's reconciliation turned up, one line per disagreement.</summary>
public enum DiscrepancyKind
{
    /// <summary>The platform thinks it took money the gateway has no record of.</summary>
    MissingAtGateway = 0,
    /// <summary>The gateway took money the platform did not ask for.</summary>
    MissingAtPlatform = 1,
    /// <summary>Both know the charge; they disagree about the amount.</summary>
    AmountMismatch = 2
}

/// <summary>
/// docs/07 §7 — "Đối soát bắt buộc mỗi ngày: so danh sách giao dịch của sàn với
/// danh sách của cổng thanh toán. Lệch một giao dịch là báo động, không được bỏ
/// qua."
///
/// The comparison is pure so it can be trusted and tested; fetching either side
/// is somebody else's problem.
/// </summary>
public static class Reconciliation
{
    /// <summary>One charge as either side records it.</summary>
    public readonly record struct Record(string Reference, decimal Amount);

    public readonly record struct Discrepancy(DiscrepancyKind Kind, string Reference, decimal Ours, decimal Theirs)
    {
        public decimal Difference => Ours - Theirs;
    }

    public readonly record struct Report(
        DateOnly Day,
        int OursCount,
        int TheirsCount,
        decimal OursTotal,
        decimal TheirsTotal,
        IReadOnlyList<Discrepancy> Discrepancies)
    {
        /// <summary>docs/07 §7 — one line out of place is the alarm.</summary>
        public bool Balanced => Discrepancies.Count == 0;

        public decimal Difference => OursTotal - TheirsTotal;
    }

    /// <summary>
    /// Compares the two lists by reference. Anything only one side knows about,
    /// or that the two disagree on, comes back as a line to be looked at — never
    /// summed away into a net figure, because two errors that cancel out are two
    /// errors, not none.
    /// </summary>
    public static Report Compare(DateOnly day, IEnumerable<Record> ours, IEnumerable<Record> theirs)
    {
        var mine = ours.ToDictionary(r => r.Reference, r => r.Amount);
        var yours = theirs.ToDictionary(r => r.Reference, r => r.Amount);

        var found = new List<Discrepancy>();

        foreach (var (reference, amount) in mine)
        {
            if (!yours.TryGetValue(reference, out var theirAmount))
                found.Add(new Discrepancy(DiscrepancyKind.MissingAtGateway, reference, amount, 0));
            else if (theirAmount != amount)
                found.Add(new Discrepancy(DiscrepancyKind.AmountMismatch, reference, amount, theirAmount));
        }

        foreach (var (reference, amount) in yours)
        {
            if (!mine.ContainsKey(reference))
                found.Add(new Discrepancy(DiscrepancyKind.MissingAtPlatform, reference, 0, amount));
        }

        return new Report(
            day,
            mine.Count, yours.Count,
            mine.Values.Sum(), yours.Values.Sum(),
            found.OrderBy(d => d.Reference).ToList());
    }

    public static string KindLabel(DiscrepancyKind kind) => kind switch
    {
        DiscrepancyKind.MissingAtGateway => "Sàn có, cổng thanh toán không có",
        DiscrepancyKind.MissingAtPlatform => "Cổng thanh toán có, sàn không có",
        _ => "Hai bên lệch số tiền"
    };

    /// <summary>What the alarm says. A number nobody reads is not an alarm.</summary>
    public static string Summary(Report report) =>
        report.Balanced
            ? $"Đối soát {report.Day:dd/MM/yyyy}: khớp {report.OursCount} giao dịch."
            : $"Đối soát {report.Day:dd/MM/yyyy}: LỆCH {report.Discrepancies.Count} giao dịch, " +
              $"chênh {report.Difference:#,##0}₫. Cần kiểm tra ngay.";
}
