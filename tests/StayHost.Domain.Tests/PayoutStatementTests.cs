using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>
/// docs/07 §15.4 — reading the bank's record of what left the account.
///
/// This is the second pair of eyes on the only button that posts a payout to the
/// ledger. Everything it gets wrong is expensive in the same direction: a
/// transfer recorded as done that the bank never made leaves a host unpaid with
/// the books insisting otherwise.
/// </summary>
public class PayoutStatementTests
{
    private static readonly Dictionary<string, decimal> Outstanding = new()
    {
        ["PO-20260818-42"] = 4_500_000m,
        ["PO-20260818-7"] = 1_200_000m
    };

    private static readonly HashSet<string> Settled = new() { "PO-20260817-9" };

    private static PayoutStatements.Outcome Judge(string memo, decimal amount) =>
        PayoutStatements.Judge(
            new PayoutStatements.Debit("FT26081800123", amount, memo), Outstanding, Settled);

    [Fact]
    public void The_bank_moving_exactly_what_the_file_asked_is_the_only_thing_that_posts()
    {
        var o = Judge("CK den StayHost PO-20260818-42 tra chu nha", 4_500_000m);

        Assert.Equal(PayoutStatements.Verdict.Transferred, o.Verdict);
        Assert.Equal("PO-20260818-42", o.Batch);
        Assert.True(PayoutStatements.Settles(o.Verdict));
    }

    /// <summary>
    /// Statements routinely arrive with the punctuation stripped, so a reference
    /// that only matches when hyphenated would match nothing in practice.
    /// </summary>
    [Fact]
    public void A_reference_is_found_even_when_the_bank_drops_its_punctuation()
    {
        var o = Judge("CHUYEN TIEN STAYHOST PO202608184 2 THANG 8", 4_500_000m);

        Assert.Equal(PayoutStatements.Verdict.Transferred, o.Verdict);
        Assert.Equal("PO-20260818-42", o.Batch);
    }

    /// <summary>
    /// Short by a hundred thousand is not "close enough" — the host is owed the
    /// difference, and deciding what to do about that is a person's job.
    /// </summary>
    [Fact]
    public void A_transfer_that_left_short_is_not_posted()
    {
        var o = Judge("StayHost PO-20260818-42", 4_400_000m);

        Assert.Equal(PayoutStatements.Verdict.WrongAmount, o.Verdict);
        Assert.Equal(4_500_000m, o.Expected);
        Assert.False(PayoutStatements.Settles(o.Verdict));
    }

    [Fact]
    public void A_debit_carrying_no_reference_of_ours_is_left_alone()
    {
        var o = Judge("THANH TOAN TIEN DIEN THANG 8", 2_000_000m);

        Assert.Equal(PayoutStatements.Verdict.Unidentified, o.Verdict);
        Assert.Null(o.Batch);
    }

    /// <summary>
    /// Pasting the same day twice is a normal thing for an operator to do. It
    /// has to be boring, not a second ledger entry.
    /// </summary>
    [Fact]
    public void A_transfer_confirmed_earlier_is_old_news_rather_than_a_new_one()
    {
        var o = Judge("StayHost PO-20260817-9", 900_000m);

        Assert.Equal(PayoutStatements.Verdict.AlreadySeen, o.Verdict);
        Assert.False(PayoutStatements.Settles(o.Verdict));
    }

    /// <summary>
    /// The reason the matcher refuses to guess. Host 42's only transfer of the
    /// day and host 4's second transfer of the day are different references that
    /// flatten to the same string, and paying the wrong host balances the books
    /// perfectly while doing it.
    /// </summary>
    [Fact]
    public void Two_transfers_that_flatten_alike_are_refused_rather_than_guessed()
    {
        var outstanding = new Dictionary<string, decimal>
        {
            ["PO-20260818-42"] = 4_500_000m,
            ["PO-20260818-4-2"] = 4_500_000m
        };

        var o = PayoutStatements.Judge(
            new PayoutStatements.Debit("FT1", 4_500_000m, "StayHost PO202608184 2"),
            outstanding, new HashSet<string>());

        Assert.Equal(PayoutStatements.Verdict.Ambiguous, o.Verdict);
        Assert.Null(o.Batch);
        Assert.False(PayoutStatements.Settles(o.Verdict));
    }

    /// <summary>
    /// The opposite trap: "PO-20260818-4" is a prefix of "PO-20260818-42", so a
    /// line naming the longer one must not be called ambiguous merely because
    /// the shorter one is also outstanding.
    /// </summary>
    [Fact]
    public void A_shorter_reference_inside_a_longer_one_is_not_ambiguity()
    {
        var outstanding = new Dictionary<string, decimal>
        {
            ["PO-20260818-4"] = 1_000_000m,
            ["PO-20260818-42"] = 4_500_000m
        };

        var o = PayoutStatements.Judge(
            new PayoutStatements.Debit("FT2", 4_500_000m, "StayHost PO-20260818-42"),
            outstanding, new HashSet<string>());

        Assert.Equal(PayoutStatements.Verdict.Transferred, o.Verdict);
        Assert.Equal("PO-20260818-42", o.Batch);
    }

    [Fact]
    public void A_reference_with_nothing_outstanding_under_it_is_not_a_match()
    {
        var o = PayoutStatements.Judge(
            new PayoutStatements.Debit("FT3", 500_000m, "StayHost PO-20260101-1"),
            Outstanding, Settled);

        Assert.Equal(PayoutStatements.Verdict.Unidentified, o.Verdict);
    }

    [Fact]
    public void Every_verdict_has_something_to_say_to_the_operator()
    {
        foreach (var v in Enum.GetValues<PayoutStatements.Verdict>())
        {
            Assert.NotEmpty(PayoutStatements.VerdictLabel(v));
            Assert.NotEmpty(PayoutStatements.Explain(
                new PayoutStatements.Outcome(v, new PayoutStatements.Debit("x", 1m, "y"), null, 0)));
        }
    }
}
