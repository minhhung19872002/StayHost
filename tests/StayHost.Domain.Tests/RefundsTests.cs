using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>docs/07 §10 — a refund goes back where the money came from.</summary>
public class RefundsTests
{
    private static Refunds.Sources Mixed => new(Card: 800_000m, Credit: 200_000m);

    [Fact]
    public void One_source_in_means_one_source_out()
    {
        var cardOnly = Refunds.Allocate(new Refunds.Sources(1_000_000m, 0), 1_000_000m);
        Assert.Equal(1_000_000m, cardOnly.ToCard);
        Assert.Equal(0m, cardOnly.ToCredit);

        var creditOnly = Refunds.Allocate(new Refunds.Sources(0, 500_000m), 500_000m);
        Assert.Equal(0m, creditOnly.ToCard);
        Assert.Equal(500_000m, creditOnly.ToCredit);
    }

    [Fact]
    public void Several_sources_unwind_in_the_reverse_order_they_were_taken()
    {
        // Taken: balance first, then the card (docs/07 §3). So the card is repaid first.
        var split = Refunds.Allocate(Mixed, 1_000_000m);
        Assert.Equal(800_000m, split.ToCard);
        Assert.Equal(200_000m, split.ToCredit);
    }

    [Fact]
    public void A_partial_refund_comes_off_the_card_before_it_touches_the_balance()
    {
        var split = Refunds.Allocate(Mixed, 500_000m);
        Assert.Equal(500_000m, split.ToCard);
        Assert.Equal(0m, split.ToCredit);
    }

    [Fact]
    public void A_partial_refund_larger_than_the_card_spills_into_the_balance()
    {
        var split = Refunds.Allocate(Mixed, 900_000m);
        Assert.Equal(800_000m, split.ToCard);
        Assert.Equal(100_000m, split.ToCredit);
    }

    [Fact]
    public void Refunding_twice_never_sends_back_more_than_came_in()
    {
        // "tổng hoàn không bao giờ vượt số đã thu"
        var first = Refunds.Allocate(Mixed, 600_000m);
        var second = Refunds.Allocate(Mixed, 600_000m, alreadyRefunded: first.Total);

        Assert.Equal(600_000m, first.Total);
        Assert.Equal(400_000m, second.Total);
        Assert.Equal(Mixed.Total, first.Total + second.Total);
        Assert.Equal(200_000m, second.Unrefundable);
    }

    [Fact]
    public void The_second_refund_continues_where_the_first_left_off()
    {
        var first = Refunds.Allocate(Mixed, 600_000m);           // all card
        var second = Refunds.Allocate(Mixed, 400_000m, first.Total);

        Assert.Equal(200_000m, second.ToCard);                   // the rest of the card
        Assert.Equal(200_000m, second.ToCredit);                 // then the balance
    }

    [Fact]
    public void Asking_for_more_than_was_paid_returns_what_was_paid_and_says_so()
    {
        var split = Refunds.Allocate(Mixed, 5_000_000m);
        Assert.Equal(Mixed.Total, split.Total);
        Assert.Equal(4_000_000m, split.Unrefundable);
    }

    [Fact]
    public void Nothing_paid_means_nothing_to_send_back()
    {
        var split = Refunds.Allocate(new Refunds.Sources(0, 0), 500_000m);
        Assert.Equal(0m, split.Total);
        Assert.Equal(500_000m, split.Unrefundable);
    }

    [Fact]
    public void A_refund_of_nothing_is_not_an_error()
    {
        var split = Refunds.Allocate(Mixed, 0);
        Assert.Equal(0m, split.Total);
        Assert.Equal(0m, split.Unrefundable);
    }

    [Fact]
    public void Negative_amounts_cannot_be_used_to_take_money_the_other_way()
    {
        var split = Refunds.Allocate(Mixed, -500_000m);
        Assert.Equal(0m, split.Total);
    }

    /* ------------------------------------------------- the closed-card path */

    [Fact]
    public void A_card_that_cannot_receive_the_refund_sends_it_to_the_balance()
    {
        var split = Refunds.Redirect(Refunds.Allocate(Mixed, 1_000_000m));

        Assert.Equal(0m, split.ToCard);
        Assert.Equal(1_000_000m, split.ToCredit);
        Assert.Equal(Mixed.Total, split.Total);
    }

    [Fact]
    public void Redirecting_a_refund_that_was_already_all_balance_changes_nothing()
    {
        var creditOnly = Refunds.Allocate(new Refunds.Sources(0, 300_000m), 300_000m);
        Assert.Equal(creditOnly, Refunds.Redirect(creditOnly));
    }

    [Fact]
    public void The_guest_is_told_when_money_lands_somewhere_they_did_not_expect()
    {
        Assert.Contains("số dư Staylio", Refunds.RedirectNotice(1_000_000m));
    }

    /* ------------------------------------------------------- what is said */

    [Fact]
    public void The_wait_is_stated_before_the_guest_confirms_and_named_as_the_banks()
    {
        var cardOnly = Refunds.Allocate(new Refunds.Sources(1_000_000m, 0), 1_000_000m);
        var notice = Refunds.TimingNotice(cardOnly);

        Assert.Contains($"{Refunds.CardRefundDaysMin}–{Refunds.CardRefundDaysMax} ngày", notice);
        Assert.Contains("ngân hàng", notice);
    }

    [Fact]
    public void Balance_comes_back_at_once_and_says_so()
    {
        var creditOnly = Refunds.Allocate(new Refunds.Sources(0, 300_000m), 300_000m);
        Assert.Contains("ngay lập tức", Refunds.TimingNotice(creditOnly));
    }

    [Fact]
    public void A_split_refund_explains_both_halves()
    {
        var notice = Refunds.TimingNotice(Refunds.Allocate(Mixed, 1_000_000m));
        Assert.Contains("về thẻ", notice);
        Assert.Contains("về số dư", notice);
    }
}
