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
    PlatformExpense = 7
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

    private static List<LedgerEntry> Post(string kind, int? bookingId, DateTime at, params Leg[] legs)
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
    public static List<LedgerEntry> CaptureBooking(Booking booking, Pricing.Breakdown price, DateTime at) =>
        Post("booking-captured", booking.Id, at,
            new Leg(LedgerAccount.GuestFunds, LedgerDirection.Debit, price.Total, $"Khách trả đơn {booking.Reference}"),
            // A promo code is money the platform gives up, not money the host loses.
            new Leg(LedgerAccount.PlatformExpense, LedgerDirection.Debit, price.Promotion, "Mã giảm giá sàn chịu"),
            new Leg(LedgerAccount.HostPayable, LedgerDirection.Credit, price.HostPayout, "Phần chủ nhà nhận"),
            new Leg(LedgerAccount.GuestServiceFeeRevenue, LedgerDirection.Credit, price.GuestServiceFee, "Phí dịch vụ khách"),
            new Leg(LedgerAccount.HostServiceFeeRevenue, LedgerDirection.Credit, price.HostServiceFee, "Phí dịch vụ chủ nhà"),
            new Leg(LedgerAccount.TaxPayable, LedgerDirection.Credit, price.Tax, "Thuế thu hộ"));

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
    /// Daily reconciliation (docs/03 §5). A non-zero result is the alarm the
    /// spec asks for — one đồng out and something is wrong.
    /// </summary>
    public static decimal Imbalance(IEnumerable<LedgerEntry> entries) =>
        entries.Sum(e => e.Signed);
}
