using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>
/// docs/07 §2.5 — the guest settles with the host on arrival.
///
/// This reverses what §2.4 used to refuse in one line, and the reason it gave is
/// worth keeping in view: the platform holding the money is what protected both
/// sides, and none of that protection exists here. Every test below is really
/// about one thing — Staylio never touches this money, so it must not claim to.
/// </summary>
public class PayAtPropertyTests
{
    private static PayAtProperty.Check Use(
        bool accepts = true, bool deposit = false, bool split = false, bool platformMoney = false) =>
        PayAtProperty.CanUse(accepts, deposit, split, platformMoney);

    [Fact]
    public void It_is_offered_only_where_the_host_turned_it_on()
    {
        Assert.True(Use().Ok);
        Assert.Equal(PayAtProperty.Refusal.NotOfferedHere, Use(accepts: false).Reason);
    }

    [Fact]
    public void It_does_not_combine_with_anything_that_needs_the_platform_in_the_middle()
    {
        // A deposit, a split and a balance are all the platform moving money.
        // This method is the platform stepping out of the payment entirely, so
        // each pairing is refused rather than half-honoured.
        Assert.Equal(PayAtProperty.Refusal.NotWithDeposit, Use(deposit: true).Reason);
        Assert.Equal(PayAtProperty.Refusal.NotWithSplit, Use(split: true).Reason);
        Assert.Equal(PayAtProperty.Refusal.NotWithPlatformMoney, Use(platformMoney: true).Reason);
    }

    [Fact]
    public void Every_refusal_tells_the_guest_what_to_do_instead()
    {
        foreach (var check in new[] { Use(accepts: false), Use(deposit: true), Use(split: true) })
        {
            Assert.False(check.Ok);
            Assert.NotEmpty(check.Message);
        }
    }

    [Fact]
    public void The_host_owes_both_service_fees_because_they_are_holding_both()
    {
        // The guest hands over the whole quoted total, which already contains
        // their 14% (docs/03 §1) — the price does not change with the method. So
        // the host is holding Staylio's fee as well as their own.
        Assert.Equal(1_030_000m, PayAtProperty.FeesOwed(700_000m, 330_000m));
        Assert.Equal(0m, PayAtProperty.FeesOwed(0m, 0m));
    }

    [Fact]
    public void A_negative_fee_never_becomes_a_credit_to_the_host()
    {
        Assert.Equal(0m, PayAtProperty.FeesOwed(-500m, 0m));
        Assert.Equal(100m, PayAtProperty.FeesOwed(-500m, 100m));
    }

    [Fact]
    public void The_catalogue_offers_it_and_no_longer_refuses_it()
    {
        // It moved out of the §2.4 refusals when the customer reversed that on
        // 28/08/2026; both halves have to agree or the checkout offers a method
        // the payment page explains it does not accept.
        Assert.True(PaymentMethods.IsAccepted(PayAtProperty.Key));
        Assert.False(PaymentMethods.IsRefused("on-arrival"));
        Assert.DoesNotContain("Trả khi nhận phòng", PaymentMethods.RefusedLabels);
    }

    [Fact]
    public void Nothing_is_charged_when_the_booking_is_made()
    {
        Assert.False(PaymentMethods.ChargesOnBooking(PayAtProperty.Key));
        Assert.True(PaymentMethods.SettlesAtProperty(PayAtProperty.Key));

        // Told apart from the other method that is not charged on booking: a
        // transfer does arrive, and has a whole reconciliation path behind it.
        Assert.False(PaymentMethods.ChargesOnBooking("vietqr"));
        Assert.False(PaymentMethods.SettlesAtProperty("vietqr"));
        Assert.False(PaymentMethods.SettlesAtProperty("card"));
    }

    [Fact]
    public void It_is_not_a_card_and_cannot_be_saved()
    {
        Assert.False(PaymentMethods.IsCard(PayAtProperty.Key));
        Assert.False(PaymentMethods.IsSavable(PayAtProperty.Key));
    }

    [Fact]
    public void What_the_guest_is_told_names_the_amount_and_who_gets_it()
    {
        var notice = PayAtProperty.Notice(2_500_000m);

        // Formatted the way every other money string in this codebase is, so the
        // assertion cannot pass on a machine whose culture happens to agree.
        Assert.Contains($"{2_500_000m:#,##0}", notice);
        Assert.Contains("chủ nhà", notice);
        Assert.Contains("không thu trước", notice);
    }

    [Fact]
    public void The_host_is_warned_about_the_protection_they_are_giving_up()
    {
        // It is their protection, so they are the ones told plainly that a
        // no-show costs them the night with nothing held against it.
        Assert.Contains("không giữ tiền", PayAtProperty.HostWarning);
        Assert.Contains("không tới", PayAtProperty.HostWarning);
        Assert.Contains("chuyển tiền kế tiếp", PayAtProperty.HostWarning);
    }
}
