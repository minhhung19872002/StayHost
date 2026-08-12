using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>docs/07 §2.3 and §7 — finding bookings inside a bank statement.</summary>
public class BankTransferTests
{
    private static readonly Dictionary<string, decimal> Awaited = new()
    {
        ["SH1A2B3C4D"] = 2_672_000m,
        ["SV7EA95836"] = 518_400m
    };

    private static readonly HashSet<string> Seen = new() { "FT26081200001" };

    private static BankTransfers.Outcome Judge(string bankRef, decimal amount, string memo) =>
        BankTransfers.Judge(new BankTransfers.Credit(bankRef, amount, memo), Awaited, Seen);

    [Fact]
    public void A_reference_is_found_through_whatever_the_bank_wrapped_it_in()
    {
        // What banks actually put in the description column.
        Assert.Equal("SH1A2B3C4D", BankTransfers.ReferenceIn("SH1A2B3C4D"));
        Assert.Equal("SH1A2B3C4D", BankTransfers.ReferenceIn("CT DEN:0123456 SH1A2B3C4D NGUYEN VAN A"));
        Assert.Equal("SH1A2B3C4D", BankTransfers.ReferenceIn("chuyen tien SH-1A2B3C4D"));
        Assert.Equal("SH1A2B3C4D", BankTransfers.ReferenceIn("SH 1A2B3C4D"));
        Assert.Equal("SV7EA95836", BankTransfers.ReferenceIn("thanh toan don sv7ea95836"));
        Assert.Equal("XP2E5A975A", BankTransfers.ReferenceIn("VE XP2E5A975A"));
    }

    [Fact]
    public void Text_with_nothing_reference_shaped_in_it_finds_nothing()
    {
        Assert.Null(BankTransfers.ReferenceIn("LUONG THANG 8"));
        Assert.Null(BankTransfers.ReferenceIn("SH12345"));        // too short
        Assert.Null(BankTransfers.ReferenceIn("AB1A2B3C4D"));     // not one of ours
        Assert.Null(BankTransfers.ReferenceIn(""));
        Assert.Null(BankTransfers.ReferenceIn(null));
    }

    [Fact]
    public void A_credit_for_the_right_amount_pays_the_booking()
    {
        var o = Judge("FT26081300007", 2_672_000m, "CT DEN SH1A2B3C4D");

        Assert.Equal(BankTransfers.Verdict.Paid, o.Verdict);
        Assert.Equal("SH1A2B3C4D", o.Booking);
        Assert.False(o.NeedsSomebody);
    }

    [Fact]
    public void Short_payment_is_not_a_payment()
    {
        // docs/07 §7 refuses to net two errors into none, and a guest who sent
        // less than the total has not paid for the stay. Somebody decides what
        // happens next; the platform does not quietly accept it.
        var o = Judge("FT26081300008", 2_000_000m, "SH1A2B3C4D");

        Assert.Equal(BankTransfers.Verdict.WrongAmount, o.Verdict);
        Assert.Equal(2_672_000m, o.Expected);
        Assert.True(o.NeedsSomebody);

        // The sentence has to name both numbers, or the person reading it cannot
        // tell what to do. Formatted the way the rest of the domain formats money
        // rather than with a separator spelled out here, which would only pin the
        // culture the tests happen to run under.
        var said = BankTransfers.Explain(o);
        Assert.Contains(2_672_000m.ToString("#,##0"), said);
        Assert.Contains(2_000_000m.ToString("#,##0"), said);
    }

    [Fact]
    public void Paying_too_much_is_also_somebody_elses_decision()
    {
        Assert.Equal(BankTransfers.Verdict.WrongAmount,
            Judge("FT26081300009", 3_000_000m, "SH1A2B3C4D").Verdict);
    }

    [Fact]
    public void Money_with_no_reference_waits_for_a_person()
    {
        var o = Judge("FT26081300010", 500_000m, "NGUYEN VAN A chuyen tien");

        Assert.Equal(BankTransfers.Verdict.Unidentified, o.Verdict);
        Assert.Null(o.Booking);
        Assert.True(o.NeedsSomebody);
    }

    [Fact]
    public void A_reference_nobody_is_waiting_on_is_flagged_not_ignored()
    {
        // A booking already paid, already cancelled, or a reference someone made
        // up. Real money arrived either way, so it cannot be dropped silently.
        var o = Judge("FT26081300011", 100_000m, "SH99999999");

        Assert.Equal(BankTransfers.Verdict.NotAwaited, o.Verdict);
        Assert.True(o.NeedsSomebody);
    }

    [Fact]
    public void Importing_the_same_statement_twice_confirms_nothing_twice()
    {
        // The whole reason a credit without the bank's own id is refused: this is
        // the only thing standing between a re-import and a booking being
        // confirmed, or worse, refunded, on money that arrived once.
        var o = Judge("FT26081200001", 2_672_000m, "SH1A2B3C4D");

        Assert.Equal(BankTransfers.Verdict.AlreadySeen, o.Verdict);
        Assert.False(o.NeedsSomebody);
    }

    [Fact]
    public void The_reference_is_looked_for_before_anything_else_about_the_line()
    {
        // Same booking, two credits: the first pays it, the second is a duplicate
        // by the bank's id rather than by the amount, because a guest may legally
        // transfer the same amount twice for two different bookings.
        var seen = new HashSet<string>();
        var first = BankTransfers.Judge(
            new BankTransfers.Credit("FT-A", 518_400m, "SV7EA95836"), Awaited, seen);
        Assert.Equal(BankTransfers.Verdict.Paid, first.Verdict);

        seen.Add("FT-A");
        Assert.Equal(BankTransfers.Verdict.AlreadySeen, BankTransfers.Judge(
            new BankTransfers.Credit("FT-A", 518_400m, "SV7EA95836"), Awaited, seen).Verdict);

        // A different bank reference for the same booking is a second real credit,
        // and the platform is no longer waiting on it — a line for a person.
        Assert.Equal(BankTransfers.Verdict.Paid, BankTransfers.Judge(
            new BankTransfers.Credit("FT-B", 518_400m, "SV7EA95836"), Awaited, seen).Verdict);
    }
}
