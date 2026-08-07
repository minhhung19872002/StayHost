namespace StayHost.Domain.Tests;

/// <summary>docs/07 §2 — what a guest may pay with, and the four things they may not.</summary>
public class PaymentMethodsTests
{
    [Fact]
    public void Every_group_docs_07_calls_required_is_offered()
    {
        var keys = PaymentMethods.Available().Select(m => m.Key).ToList();

        // §2.1: international cards, domestic NAPAS, the two wallets, balance.
        Assert.Contains("card", keys);
        Assert.Contains("napas", keys);
        Assert.Contains("momo", keys);
        Assert.Contains("zalopay", keys);
        Assert.Contains("balance", keys);
    }

    [Fact]
    public void What_is_only_wanted_or_later_stays_out_until_asked_for()
    {
        var required = PaymentMethods.Available().Select(m => m.Key).ToList();

        Assert.DoesNotContain("applepay", required);
        Assert.DoesNotContain("paypal", required);

        var wanted = PaymentMethods.Available(MethodTier.Wanted).Select(m => m.Key).ToList();
        Assert.Contains("applepay", wanted);
        Assert.DoesNotContain("paypal", wanted);
    }

    [Fact]
    public void The_four_things_docs_07_refuses_are_not_quietly_missing()
    {
        // §2.4 — refusing without a reason reads as a broken feature.
        Assert.True(PaymentMethods.IsRefused("cash"));
        Assert.True(PaymentMethods.IsRefused("transfer"));
        Assert.True(PaymentMethods.IsRefused("crypto"));
        Assert.True(PaymentMethods.IsRefused("on-arrival"));

        Assert.False(PaymentMethods.IsAccepted("cash"));
        Assert.Contains("giữ tiền cho tới khi bạn nhận phòng", PaymentMethods.RefusalReason());
    }

    [Fact]
    public void Manual_bank_transfer_is_a_refusal_not_an_option()
    {
        // It read as an offer on the checkout screen for a while; docs/07 §2.4
        // lists it alongside cash.
        Assert.False(PaymentMethods.IsAccepted("bank"));
        Assert.True(PaymentMethods.IsRefused("transfer"));
    }

    [Fact]
    public void Only_cards_can_be_saved()
    {
        // docs/07 §4 is about cards; a wallet is authorised in its own app.
        Assert.True(PaymentMethods.IsSavable("card"));
        Assert.True(PaymentMethods.IsSavable("napas"));
        Assert.False(PaymentMethods.IsSavable("momo"));
        Assert.False(PaymentMethods.IsSavable("balance"));
    }

    /* ---- §3, the fallback nobody charges today ---- */

    [Fact]
    public void A_booking_paid_entirely_from_the_balance_still_needs_somewhere_to_charge_later()
    {
        // docs/06 §3.3 collects damages through the stored method. Without one
        // the platform has a claim and nowhere to send it.
        Assert.True(PaymentMethods.NeedsFallbackMethod(0m, "balance", null));
        Assert.True(PaymentMethods.NeedsFallbackMethod(0m, "card", null));
        Assert.True(PaymentMethods.NeedsFallbackMethod(0m, null, null));
    }

    [Fact]
    public void A_card_on_file_is_a_fallback_even_when_nothing_is_charged_today()
    {
        Assert.False(PaymentMethods.NeedsFallbackMethod(0m, "card", "4242"));
        Assert.False(PaymentMethods.NeedsFallbackMethod(0m, "momo", null));
    }

    [Fact]
    public void A_booking_with_something_to_pay_needs_no_extra_promise()
    {
        // The method being used is the fallback.
        Assert.False(PaymentMethods.NeedsFallbackMethod(1_000_000m, "balance", null));
    }

    [Fact]
    public void An_unknown_key_is_neither_accepted_nor_explained_away()
    {
        Assert.False(PaymentMethods.IsAccepted("bitcoin-but-spelled-oddly"));
        Assert.Null(PaymentMethods.Find("bitcoin-but-spelled-oddly"));
    }
}
