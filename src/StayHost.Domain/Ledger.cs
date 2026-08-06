namespace StayHost.Domain;

/// <summary>
/// Where money sits. docs/00 §6.1: every amount is recorded on both sides, so
/// the sum of every debit must equal the sum of every credit, forever.
/// </summary>
public enum LedgerAccount
{
    /// <summary>Cash the platform is holding on the guest's behalf.</summary>
    GuestFunds = 0,
    /// <summary>Owed to the host until payout runs.</summary>
    HostPayable = 1,
    /// <summary>Platform revenue from the guest service fee.</summary>
    GuestServiceFeeRevenue = 2,
    /// <summary>Platform revenue from the host service fee.</summary>
    HostServiceFeeRevenue = 3,
    /// <summary>Collected on behalf of the tax authority; never platform revenue.</summary>
    TaxPayable = 4,
    /// <summary>Owed back to the guest after a cancellation, until the refund settles.</summary>
    GuestRefundPayable = 5,
    /// <summary>Promotional balance the platform has granted, e.g. host-cancellation goodwill.</summary>
    PromotionalCredit = 6,
    /// <summary>The platform's own expense line, funding credits and goodwill.</summary>
    PlatformExpense = 7,
    /// <summary>
    /// The half of a part-paid booking the guest still owes (docs/01 ĐP-06).
    /// The stay is recognised whole at booking time, so this stands in for the
    /// cash until the second charge lands.
    /// </summary>
    GuestReceivable = 8,
    /// <summary>
    /// Shares collected for a split bill (docs/01 ĐP-07) before the booking is
    /// confirmed. Nothing is recognised while the money sits here — it either
    /// becomes a booking or goes back to the people who sent it.
    /// </summary>
    SplitEscrow = 9
}

public enum LedgerDirection
{
    Debit = 1,
    Credit = 2
}

/// <summary>
/// One immutable half of a transaction. Rows are only ever inserted — a
/// correction is a new transaction, never an edit (docs/00 §6.2).
/// </summary>
public class LedgerEntry
{
    public long Id { get; set; }

    /// <summary>Groups the halves of one transaction; every group must balance.</summary>
    public Guid TransactionId { get; set; }
    /// <summary>What happened: "booking-captured", "booking-refunded", "host-payout".</summary>
    public string TransactionKind { get; set; } = "";

    public int? BookingId { get; set; }
    public Booking? Booking { get; set; }

    /// <summary>Set instead of <see cref="BookingId"/> when the money is for a session.</summary>
    public int? ExperienceBookingId { get; set; }
    public ExperienceBooking? ExperienceBooking { get; set; }

    /// <summary>Set when the money is for a service job (docs/01 MR-05 → MR-07).</summary>
    public int? ServiceBookingId { get; set; }
    public ServiceBooking? ServiceBooking { get; set; }

    public LedgerAccount Account { get; set; }
    public LedgerDirection Direction { get; set; }
    /// <summary>Always positive; <see cref="Direction"/> carries the sign.</summary>
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";

    public string Memo { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Signed amount, for summing a column.</summary>
    public decimal Signed => Direction == LedgerDirection.Debit ? Amount : -Amount;
}

/// <summary>
/// Builds balanced transactions. Nothing here touches the database; callers add
/// the returned rows to their unit of work, which keeps the rules testable.
/// </summary>
public static class Ledger
{
    public sealed class UnbalancedTransactionException(decimal debits, decimal credits)
        : InvalidOperationException($"Bút toán không cân: nợ {debits:#,##0}₫ ≠ có {credits:#,##0}₫.");

    private sealed record Leg(LedgerAccount Account, LedgerDirection Direction, decimal Amount, string Memo);

    private static List<LedgerEntry> Post(
        string kind, int? bookingId, DateTime at, params Leg[] legs) =>
        Post(kind, bookingId, null, at, legs);

    private static List<LedgerEntry> Post(
        string kind, int? bookingId, int? experienceBookingId, DateTime at, params Leg[] legs) =>
        Post(kind, bookingId, experienceBookingId, null, at, legs);

    private static List<LedgerEntry> Post(
        string kind, int? bookingId, int? experienceBookingId, int? serviceBookingId,
        DateTime at, params Leg[] legs)
    {
        var real = legs.Where(l => l.Amount > 0).ToList();

        var debits = real.Where(l => l.Direction == LedgerDirection.Debit).Sum(l => l.Amount);
        var credits = real.Where(l => l.Direction == LedgerDirection.Credit).Sum(l => l.Amount);
        if (debits != credits) throw new UnbalancedTransactionException(debits, credits);

        var transactionId = Guid.NewGuid();
        return real.Select(l => new LedgerEntry
        {
            TransactionId = transactionId,
            TransactionKind = kind,
            BookingId = bookingId,
            ExperienceBookingId = experienceBookingId,
            ServiceBookingId = serviceBookingId,
            Account = l.Account,
            Direction = l.Direction,
            Amount = l.Amount,
            Memo = l.Memo,
            CreatedAt = at
        }).ToList();
    }

    /// <summary>
    /// The guest has paid. Cash lands in the platform's hands and is immediately
    /// split into what it owes the host, what it keeps, and what it holds for tax.
    /// </summary>
    /// <param name="paidNow">
    /// What the card was actually charged. Less than the total means the guest
    /// paid a deposit (docs/01 ĐP-06); the rest is carried as a receivable so
    /// the host's share, the fees and the tax are all recognised at once.
    /// </param>
    public static List<LedgerEntry> CaptureBooking(
        Booking booking, Pricing.Breakdown price, DateTime at, decimal? paidNow = null)
    {
        var cash = paidNow is { } part ? Math.Clamp(part, 0, price.Total) : price.Total;

        return Post("booking-captured", booking.Id, at,
            new Leg(LedgerAccount.GuestFunds, LedgerDirection.Debit, cash, $"Khách trả đơn {booking.Reference}"),
            new Leg(LedgerAccount.GuestReceivable, LedgerDirection.Debit, price.Total - cash, "Phần khách còn nợ"),
            // A promo code is money the platform gives up, not money the host loses.
            new Leg(LedgerAccount.PlatformExpense, LedgerDirection.Debit, price.Promotion, "Mã giảm giá sàn chịu"),
            new Leg(LedgerAccount.HostPayable, LedgerDirection.Credit, price.HostPayout, "Phần chủ nhà nhận"),
            new Leg(LedgerAccount.GuestServiceFeeRevenue, LedgerDirection.Credit, price.GuestServiceFee, "Phí dịch vụ khách"),
            new Leg(LedgerAccount.HostServiceFeeRevenue, LedgerDirection.Credit, price.HostServiceFee, "Phí dịch vụ chủ nhà"),
            new Leg(LedgerAccount.TaxPayable, LedgerDirection.Credit, price.Tax, "Thuế thu hộ"));
    }

    /// <summary>
    /// docs/01 MR-03 — a seat at a session. Same accounts as a stay: the money
    /// engine does not care what was sold, only how it splits.
    /// </summary>
    public static List<LedgerEntry> CaptureExperience(
        ExperienceBooking booking, Pricing.ExperienceBreakdown price, DateTime at) =>
        Post("experience-captured", null, booking.Id, at,
            new Leg(LedgerAccount.GuestFunds, LedgerDirection.Debit, price.Total, $"Khách trả vé {booking.Reference}"),
            new Leg(LedgerAccount.HostPayable, LedgerDirection.Credit, price.HostPayout, "Phần chủ trải nghiệm nhận"),
            new Leg(LedgerAccount.GuestServiceFeeRevenue, LedgerDirection.Credit, price.GuestServiceFee, "Phí dịch vụ khách"),
            new Leg(LedgerAccount.HostServiceFeeRevenue, LedgerDirection.Credit, price.HostServiceFee, "Phí dịch vụ chủ nhà"),
            new Leg(LedgerAccount.TaxPayable, LedgerDirection.Credit, price.Tax, "Thuế thu hộ"));

    /// <summary>
    /// A ticket refunded, whether the guest pulled out or the session was called
    /// off. A full refund reverses exactly what the capture recognised.
    /// </summary>
    public static List<LedgerEntry> RefundExperience(
        ExperienceBooking booking, decimal amount, DateTime at)
    {
        if (amount <= 0) return [];

        // Everything is returned in the proportion it was taken, so a partial
        // refund never quietly comes out of the host's share alone.
        var share = booking.Total == 0 ? 0m : amount / booking.Total;
        var hostBack = Math.Round(booking.HostPayout * share, 0, MidpointRounding.AwayFromZero);
        var guestFeeBack = Math.Round(booking.ServiceFee * share, 0, MidpointRounding.AwayFromZero);
        var hostFeeBack = Math.Round(booking.HostServiceFee * share, 0, MidpointRounding.AwayFromZero);
        var taxBack = amount - hostBack - guestFeeBack - hostFeeBack;

        return Post("experience-refunded", null, booking.Id, at,
            new Leg(LedgerAccount.HostPayable, LedgerDirection.Debit, hostBack, "Thu lại phần chủ trải nghiệm"),
            new Leg(LedgerAccount.GuestServiceFeeRevenue, LedgerDirection.Debit, guestFeeBack, "Hoàn phí dịch vụ khách"),
            new Leg(LedgerAccount.HostServiceFeeRevenue, LedgerDirection.Debit, hostFeeBack, "Hoàn phí dịch vụ chủ nhà"),
            new Leg(LedgerAccount.TaxPayable, LedgerDirection.Debit, taxBack, "Hoàn thuế"),
            new Leg(LedgerAccount.GuestFunds, LedgerDirection.Credit, amount, $"Hoàn vé {booking.Reference}"));
    }

    /// <summary>
    /// docs/01 MR-05 → MR-07 — a service job. A partner job pays the platform a
    /// commission where a host's own service pays the host service fee; both land
    /// in the same revenue account because both are what the platform kept.
    /// </summary>
    public static List<LedgerEntry> CaptureService(
        ServiceBooking booking, Pricing.ServiceBreakdown price, DateTime at) =>
        Post("service-captured", null, null, booking.Id, at,
            new Leg(LedgerAccount.GuestFunds, LedgerDirection.Debit, price.Total, $"Khách trả dịch vụ {booking.Reference}"),
            new Leg(LedgerAccount.HostPayable, LedgerDirection.Credit, price.ProviderPayout, "Phần bên cung cấp nhận"),
            new Leg(LedgerAccount.GuestServiceFeeRevenue, LedgerDirection.Credit, price.GuestServiceFee, "Phí dịch vụ khách"),
            new Leg(LedgerAccount.HostServiceFeeRevenue, LedgerDirection.Credit, price.PlatformCut, "Phần sàn giữ lại"),
            new Leg(LedgerAccount.TaxPayable, LedgerDirection.Credit, price.Tax, "Thuế thu hộ"));

    /// <summary>A service job refunded, in the proportion each account received it.</summary>
    public static List<LedgerEntry> RefundService(ServiceBooking booking, decimal amount, DateTime at)
    {
        if (amount <= 0) return [];

        var share = booking.Total == 0 ? 0m : amount / booking.Total;
        var providerBack = Math.Round(booking.ProviderPayout * share, 0, MidpointRounding.AwayFromZero);
        var guestFeeBack = Math.Round(booking.ServiceFee * share, 0, MidpointRounding.AwayFromZero);
        var cutBack = Math.Round(booking.PlatformCut * share, 0, MidpointRounding.AwayFromZero);
        var taxBack = amount - providerBack - guestFeeBack - cutBack;

        return Post("service-refunded", null, null, booking.Id, at,
            new Leg(LedgerAccount.HostPayable, LedgerDirection.Debit, providerBack, "Thu lại phần bên cung cấp"),
            new Leg(LedgerAccount.GuestServiceFeeRevenue, LedgerDirection.Debit, guestFeeBack, "Hoàn phí dịch vụ khách"),
            new Leg(LedgerAccount.HostServiceFeeRevenue, LedgerDirection.Debit, cutBack, "Hoàn phần sàn giữ"),
            new Leg(LedgerAccount.TaxPayable, LedgerDirection.Debit, taxBack, "Hoàn thuế"),
            new Leg(LedgerAccount.GuestFunds, LedgerDirection.Credit, amount, $"Hoàn dịch vụ {booking.Reference}"));
    }

    /// <summary>docs/01 ĐP-07 — one person's share, held until the last one lands.</summary>
    public static List<LedgerEntry> HoldShare(int bookingId, string reference, decimal amount, DateTime at) =>
        Post("split-share-held", bookingId, at,
            new Leg(LedgerAccount.GuestFunds, LedgerDirection.Debit, amount, $"Phần chia đơn {reference}"),
            new Leg(LedgerAccount.SplitEscrow, LedgerDirection.Credit, amount, "Giữ chờ đủ người"));

    /// <summary>The last share landed: what was held becomes the money for the booking.</summary>
    public static List<LedgerEntry> ReleaseEscrow(Booking booking, decimal amount, DateTime at) =>
        Post("split-escrow-released", booking.Id, at,
            new Leg(LedgerAccount.SplitEscrow, LedgerDirection.Debit, amount, "Giải toả tiền giữ"),
            new Leg(LedgerAccount.GuestFunds, LedgerDirection.Credit, amount, $"Chuyển vào đơn {booking.Reference}"));

    /// <summary>Nobody finished paying: every share held goes back where it came from.</summary>
    public static List<LedgerEntry> ReturnShare(int bookingId, string reference, decimal amount, DateTime at) =>
        Post("split-share-returned", bookingId, at,
            new Leg(LedgerAccount.SplitEscrow, LedgerDirection.Debit, amount, "Trả lại phần đã giữ"),
            new Leg(LedgerAccount.GuestFunds, LedgerDirection.Credit, amount, $"Hoàn phần chia đơn {reference}"));

    /// <summary>
    /// docs/01 ĐP-06 — the second charge. Nothing is recognised again; the cash
    /// simply arrives and the receivable goes away.
    /// </summary>
    public static List<LedgerEntry> CollectBalance(Booking booking, decimal amount, DateTime at) =>
        Post("balance-collected", booking.Id, at,
            new Leg(LedgerAccount.GuestFunds, LedgerDirection.Debit, amount, $"Khách trả nốt đơn {booking.Reference}"),
            new Leg(LedgerAccount.GuestReceivable, LedgerDirection.Credit, amount, "Xoá phần còn nợ"));

    /// <summary>
    /// A part-paid booking cancelled before the rest was taken. What the guest
    /// is owed is set against what they still owe rather than moved as cash —
    /// nobody sends money in both directions on the same booking.
    /// </summary>
    public static List<LedgerEntry> NetRefundAgainstReceivable(Booking booking, decimal amount, DateTime at) =>
        amount <= 0
            ? []
            : Post("refund-netted", booking.Id, at,
                new Leg(LedgerAccount.GuestRefundPayable, LedgerDirection.Debit, amount, "Cấn trừ vào phần khách còn nợ"),
                new Leg(LedgerAccount.GuestReceivable, LedgerDirection.Credit, amount, $"Đơn {booking.Reference}"));

    /// <summary>
    /// A part-paid booking that ends before the balance was ever collected: the
    /// receivable is written off against the same accounts that recognised it,
    /// so the books do not carry a debt nobody is going to pay.
    /// </summary>
    public static List<LedgerEntry> WriteOffReceivable(Booking booking, decimal amount, DateTime at) =>
        amount <= 0
            ? []
            : Post("receivable-written-off", booking.Id, at,
                new Leg(LedgerAccount.PlatformExpense, LedgerDirection.Debit, amount, "Xoá nợ khách không trả"),
                new Leg(LedgerAccount.GuestReceivable, LedgerDirection.Credit, amount, $"Đơn {booking.Reference}"));

    /// <summary>
    /// A cancellation. Everything being returned is taken back out of the
    /// accounts that received it and parked as a payable to the guest.
    /// </summary>
    public static List<LedgerEntry> RefundBooking(
        Booking booking, Cancellation.Outcome outcome, decimal hostServiceFeeReturned, DateTime at)
    {
        var roomAndCleaning = outcome.RoomRefund + outcome.CleaningRefund;

        var entries = Post("booking-refunded", booking.Id, at,
            new Leg(LedgerAccount.HostPayable, LedgerDirection.Debit, roomAndCleaning - hostServiceFeeReturned, "Thu lại phần chủ nhà"),
            new Leg(LedgerAccount.HostServiceFeeRevenue, LedgerDirection.Debit, hostServiceFeeReturned, "Hoàn phí dịch vụ chủ nhà"),
            new Leg(LedgerAccount.GuestServiceFeeRevenue, LedgerDirection.Debit, outcome.ServiceFeeRefund, "Hoàn phí dịch vụ khách"),
            new Leg(LedgerAccount.TaxPayable, LedgerDirection.Debit, outcome.TaxRefund, "Hoàn thuế"),
            new Leg(LedgerAccount.GuestRefundPayable, LedgerDirection.Credit, outcome.Amount, $"Hoàn cho khách đơn {booking.Reference}"));

        if (outcome.GoodwillCredit > 0)
        {
            entries.AddRange(Post("goodwill-credit", booking.Id, at,
                new Leg(LedgerAccount.PlatformExpense, LedgerDirection.Debit, outcome.GoodwillCredit, "Bù đắp do chủ nhà huỷ"),
                new Leg(LedgerAccount.PromotionalCredit, LedgerDirection.Credit, outcome.GoodwillCredit, "Số dư tặng khách")));
        }

        return entries;
    }

    /// <summary>Cash actually leaves for the guest's card; the payable is cleared.</summary>
    public static List<LedgerEntry> SettleRefund(Booking booking, decimal amount, DateTime at) =>
        Post("refund-settled", booking.Id, at,
            new Leg(LedgerAccount.GuestRefundPayable, LedgerDirection.Debit, amount, "Chi hoàn tiền"),
            new Leg(LedgerAccount.GuestFunds, LedgerDirection.Credit, amount, $"Chuyển trả khách đơn {booking.Reference}"));

    /// <summary>Cash leaves for the host, 24 hours after check-in (docs/03 §5).</summary>
    public static List<LedgerEntry> PayoutHost(Booking booking, decimal amount, DateTime at) =>
        Post("host-payout", booking.Id, at,
            new Leg(LedgerAccount.HostPayable, LedgerDirection.Debit, amount, "Chi trả chủ nhà"),
            new Leg(LedgerAccount.GuestFunds, LedgerDirection.Credit, amount, $"Chuyển cho chủ nhà đơn {booking.Reference}"));

    /// <summary>
    /// docs/01 AT-04 — an admin's ruling on a claim. Money moves between the
    /// two sides of the booking, never out of thin air: whatever the guest gets
    /// back comes out of what the host was owed, and the reverse for damages.
    /// </summary>
    public static List<LedgerEntry> SettleClaim(
        Booking booking, decimal toGuest, decimal toHost, DateTime at)
    {
        if (toGuest > 0)
        {
            return Post("claim-to-guest", booking.Id, at,
                new Leg(LedgerAccount.HostPayable, LedgerDirection.Debit, toGuest, $"Bồi thường khách, hồ sơ đơn {booking.Reference}"),
                new Leg(LedgerAccount.GuestRefundPayable, LedgerDirection.Credit, toGuest, "Phải trả khách theo phân xử"));
        }

        if (toHost > 0)
        {
            return Post("claim-to-host", booking.Id, at,
                new Leg(LedgerAccount.GuestFunds, LedgerDirection.Debit, toHost, $"Khách bồi thường, hồ sơ đơn {booking.Reference}"),
                new Leg(LedgerAccount.HostPayable, LedgerDirection.Credit, toHost, "Phải trả chủ nhà theo phân xử"));
        }

        return [];
    }

    /// <summary>
    /// Daily reconciliation (docs/03 §5). A non-zero result is the alarm the
    /// spec asks for — one đồng out and something is wrong.
    /// </summary>
    public static decimal Imbalance(IEnumerable<LedgerEntry> entries) =>
        entries.Sum(e => e.Signed);
}
