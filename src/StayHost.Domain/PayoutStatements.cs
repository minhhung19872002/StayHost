namespace StayHost.Domain;

/// <summary>
/// docs/07 §15.4 — reading the bank's own record of what left the account, and
/// checking it against the transfers this platform said it made.
///
/// The transfer file is the only place money leaves Staylio, and until now the
/// only thing that recorded a transfer as done was a person pressing *Đã
/// chuyển*. That button posts the ledger entry, so a mis-press writes "paid the
/// host" for a transfer the bank never executed — and nothing anywhere would
/// disagree. docs/07 §7 asks the same question of the gateways and answers it by
/// comparing against the gateway's own list; this is that second pair of eyes
/// for the outgoing side.
///
/// It is deliberately the mirror of <see cref="BankTransfers"/>, which reads
/// money coming <em>in</em>. Same shapes, same vocabulary, opposite direction —
/// so an operator who has learned one screen has learned both.
///
/// Nothing here touches the database or posts anything. It turns (a statement
/// line, what is outstanding) into a verdict, and the caller decides what that
/// is worth.
/// </summary>
public static class PayoutStatements
{
    /// <summary>One debit as the bank recorded it.</summary>
    /// <param name="BankReference">
    /// The bank's own id for the transfer, which is what makes importing the
    /// same statement twice harmless. A line without one is refused rather than
    /// risked.
    /// </param>
    public readonly record struct Debit(string BankReference, decimal Amount, string Description);

    public enum Verdict
    {
        /// <summary>The bank moved exactly what the file asked for. This one may be posted.</summary>
        Transferred = 0,

        /// <summary>Money left the account carrying no payout reference this platform issued.</summary>
        Unidentified = 1,

        /// <summary>The reference is real but no transfer is outstanding under it.</summary>
        NotAwaited = 2,

        /// <summary>Right transfer, wrong money. Never posted — a person has to look.</summary>
        WrongAmount = 3,

        /// <summary>This transfer was already confirmed, so the line is old news.</summary>
        AlreadySeen = 4,

        /// <summary>
        /// The line could be read as two different outstanding transfers.
        ///
        /// It happens because bank statements strip punctuation: <c>PO-20260818-42</c>
        /// for host 42 and <c>PO-20260818-4-2</c>, the second transfer of the day
        /// for host 4, both flatten to <c>PO2026081842</c>. Guessing between them
        /// would pay the wrong host and the ledger would balance perfectly while
        /// it did. So it refuses to guess.
        /// </summary>
        Ambiguous = 5
    }

    public readonly record struct Outcome(Verdict Verdict, Debit Debit, string? Batch, decimal Expected);

    /// <summary>
    /// Which outstanding transfer this statement line is about, or null.
    ///
    /// It does not try to parse a reference out of the text — it looks for the
    /// references actually outstanding, in the line, both as written and with
    /// punctuation removed. That way an unknown format cannot be mistaken for a
    /// real one, and a real one cannot be missed because the bank dropped its
    /// hyphens. Where two candidates both fit, the answer is "more than one",
    /// not a coin toss.
    /// </summary>
    public static IReadOnlyList<string> ReferencesIn(string? line, IEnumerable<string> outstanding)
    {
        if (string.IsNullOrWhiteSpace(line)) return [];

        var text = Flatten(line);
        var hits = new List<string>();

        foreach (var reference in outstanding)
        {
            if (reference.Length == 0) continue;
            if (text.Contains(Flatten(reference), StringComparison.Ordinal)) hits.Add(reference);
        }

        // "PO2026081842" contains "PO202608184", so a longer match makes a
        // shorter one that is a prefix of it redundant rather than ambiguous.
        return hits.Count <= 1
            ? hits
            : hits.Where(h => !hits.Any(other => other != h && Covers(other, h))).ToList();
    }

    /// <summary>
    /// Whether one candidate makes another redundant rather than ambiguous.
    ///
    /// Compared <em>after</em> flattening, not before. "PO-20260818-4-2" is the
    /// longer string but flattens to exactly the same thing as "PO-20260818-42",
    /// so measuring the originals would let the wrong one swallow the other and
    /// turn the ambiguity this whole verdict exists for back into a confident
    /// answer — pointed at a different host.
    /// </summary>
    private static bool Covers(string candidate, string other)
    {
        var a = Flatten(candidate);
        var b = Flatten(other);

        return a.Length > b.Length && a.StartsWith(b, StringComparison.Ordinal);
    }

    private static string Flatten(string s) =>
        new(s.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    /// <summary>
    /// What one line of the statement means, given what is still outstanding and
    /// what has already been confirmed.
    /// </summary>
    /// <param name="outstanding">Reference → the amount the file asked the bank to move.</param>
    /// <param name="settled">
    /// References already confirmed. Re-importing the same statement is a normal
    /// thing to do — an operator pastes the whole day twice — and it must be
    /// boring rather than dangerous.
    /// </param>
    public static Outcome Judge(
        Debit debit,
        IReadOnlyDictionary<string, decimal> outstanding,
        IReadOnlySet<string> settled)
    {
        var found = ReferencesIn(debit.Description, outstanding.Keys.Concat(settled));

        if (found.Count > 1) return new Outcome(Verdict.Ambiguous, debit, null, 0);
        if (found.Count == 0) return new Outcome(Verdict.Unidentified, debit, null, 0);

        var reference = found[0];

        if (settled.Contains(reference))
            return new Outcome(Verdict.AlreadySeen, debit, reference, 0);

        if (!outstanding.TryGetValue(reference, out var expected))
            return new Outcome(Verdict.NotAwaited, debit, reference, 0);

        // Exact, not "close enough". A transfer that left short is a transfer
        // that did not do what the file said, and the host is owed the
        // difference — which is a person's decision, not this one's.
        if (debit.Amount != expected)
            return new Outcome(Verdict.WrongAmount, debit, reference, expected);

        return new Outcome(Verdict.Transferred, debit, reference, expected);
    }

    /// <summary>The one verdict that may post a ledger entry.</summary>
    public static bool Settles(Verdict v) => v == Verdict.Transferred;

    public static string VerdictLabel(Verdict v) => v switch
    {
        Verdict.Transferred => "Ngân hàng đã chuyển",
        Verdict.Unidentified => "Không rõ lệnh nào",
        Verdict.NotAwaited => "Không có lệnh nào đang chờ",
        Verdict.WrongAmount => "Sai số tiền",
        Verdict.AlreadySeen => "Đã xác nhận trước đó",
        Verdict.Ambiguous => "Khớp nhiều lệnh",
        _ => "Không rõ"
    };

    /// <summary>What the operator is told, and what to do about it.</summary>
    public static string Explain(Outcome o) => o.Verdict switch
    {
        Verdict.Transferred =>
            "Ngân hàng đã chuyển đúng số tiền của lệnh này. Đã ghi sổ.",
        Verdict.Unidentified =>
            "Khoản này rời tài khoản nhưng không mang mã lệnh nào của StayHost. "
            + "Có thể là chi tiêu khác của công ty — kiểm tra trước khi bỏ qua.",
        Verdict.NotAwaited =>
            "Mã lệnh có thật nhưng không có lệnh nào đang chờ dưới mã đó.",
        Verdict.WrongAmount =>
            $"Lệnh yêu cầu {o.Expected:N0}₫ nhưng ngân hàng chuyển {o.Debit.Amount:N0}₫. "
            + "Không ghi sổ — chênh lệch phải có người xử lý.",
        Verdict.AlreadySeen =>
            "Lệnh này đã được xác nhận trước đó, dòng sao kê chỉ là bản sao.",
        Verdict.Ambiguous =>
            "Dòng này khớp nhiều lệnh cùng lúc nên không đoán được là lệnh nào. "
            + "Xác nhận tay lệnh đúng.",
        _ => ""
    };
}
