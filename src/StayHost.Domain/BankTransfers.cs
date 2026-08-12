using System.Text.RegularExpressions;

namespace StayHost.Domain;

/// <summary>
/// docs/07 §2.3 and §7 — reading a bank statement back and finding the bookings
/// in it.
///
/// A credit on a statement is anonymous: an amount, a time, and a line of text a
/// bank assembled out of whatever the payer typed. The only thing tying it to a
/// booking is the reference, which is why the QR puts the reference in for the
/// guest rather than trusting them to type it.
///
/// So the matching does not parse columns. Bank exports disagree about column
/// order, headings, date format and decimal separator, and a parser tuned to one
/// bank's CSV breaks the week they change it. It scans each line for something
/// shaped like a StayHost reference instead — two letters and eight hex digits is
/// distinctive enough that a false positive would have to be deliberate — and
/// reads the amount off the same line. That works on MB's export, on anybody
/// else's, and on a block of text pasted out of internet banking.
/// </summary>
public static class BankTransfers
{
    /// <summary>
    /// SH for a stay, SV for a service, XP for a ticket, then eight hex digits.
    /// Every reference this platform issues has that shape.
    /// </summary>
    private static readonly Regex Reference =
        new("(SH|SV|XP)[0-9A-F]{8}", RegexOptions.Compiled);

    /// <summary>
    /// Everything that is not a letter or a digit, dropped before scanning. Banks
    /// pad memos with punctuation and spaces — "CT DEN:0123 SH-1A2B3C4D NGUYEN A"
    /// — and none of it belongs to the reference.
    /// </summary>
    private static readonly Regex Noise = new("[^A-Za-z0-9]", RegexOptions.Compiled);

    /// <summary>The reference hidden in a statement line, or null if there is none.</summary>
    public static string? ReferenceIn(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        var flattened = Noise.Replace(line, "").ToUpperInvariant();
        var found = Reference.Match(flattened);
        return found.Success ? found.Value : null;
    }

    /// <summary>One credit as the bank recorded it.</summary>
    /// <param name="BankReference">
    /// The bank's own id for the transfer. This is what makes importing the same
    /// statement twice harmless, so a line without one is refused rather than
    /// risked.
    /// </param>
    public readonly record struct Credit(string BankReference, decimal Amount, string Description)
    {
        public string? Booking => ReferenceIn(Description);
    }

    public enum Verdict
    {
        /// <summary>Reference found, amount agrees: this booking has been paid.</summary>
        Paid = 0,
        /// <summary>Money arrived carrying no reference anybody here issued.</summary>
        Unidentified = 1,
        /// <summary>The reference exists but the platform is not waiting on it.</summary>
        NotAwaited = 2,
        /// <summary>Right booking, wrong money.</summary>
        WrongAmount = 3,
        /// <summary>This exact credit has been imported before.</summary>
        AlreadySeen = 4
    }

    public readonly record struct Outcome(Verdict Verdict, Credit Credit, string? Booking, decimal Expected)
    {
        public bool NeedsSomebody => Verdict is not (Verdict.Paid or Verdict.AlreadySeen);
    }

    /// <summary>
    /// What to do with one credit, given what the platform is waiting for and
    /// what it has already seen.
    ///
    /// A short payment is never accepted as a payment: docs/07 §7 refuses to net
    /// two errors into none, and a guest who transferred less than the total has
    /// not paid for the booking. It becomes a line for somebody to look at, which
    /// is the honest answer — the money is real, the booking is not settled, and
    /// only a human knows which of those to fix.
    /// </summary>
    public static Outcome Judge(
        Credit credit,
        IReadOnlyDictionary<string, decimal> awaited,
        IReadOnlySet<string> alreadySeen)
    {
        if (alreadySeen.Contains(credit.BankReference))
            return new Outcome(Verdict.AlreadySeen, credit, credit.Booking, 0);

        if (credit.Booking is not { } reference)
            return new Outcome(Verdict.Unidentified, credit, null, 0);

        if (!awaited.TryGetValue(reference, out var expected))
            return new Outcome(Verdict.NotAwaited, credit, reference, 0);

        return credit.Amount == expected
            ? new Outcome(Verdict.Paid, credit, reference, expected)
            : new Outcome(Verdict.WrongAmount, credit, reference, expected);
    }

    /// <summary>The sentence an operator reads next to a line they have to act on.</summary>
    public static string Explain(Outcome o) => o.Verdict switch
    {
        Verdict.Paid => "Đã khớp và xác nhận đơn.",
        Verdict.AlreadySeen => "Giao dịch này đã nhập trước đó, bỏ qua.",
        Verdict.Unidentified => "Không tìm thấy mã đơn trong nội dung chuyển khoản.",
        Verdict.NotAwaited => $"Không có đơn nào đang chờ mã {o.Booking}.",
        _ => $"Đơn {o.Booking} chờ {o.Expected:#,##0}₫ nhưng nhận {o.Credit.Amount:#,##0}₫."
    };
}
