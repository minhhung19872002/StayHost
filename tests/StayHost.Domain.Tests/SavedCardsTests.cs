namespace StayHost.Domain.Tests;

/// <summary>docs/07 §4 — what may be kept about a card, and what may be done with it.</summary>
public class SavedCardsTests
{
    private static readonly DateOnly Today = new(2026, 8, 7);

    private static SavedCard Card(int month, int year, CardBrand brand = CardBrand.Visa) =>
        new() { Brand = brand, Last4 = "4242", ExpiryMonth = month, ExpiryYear = year };

    /* ---- reading the number, once, and then forgetting it ---- */

    [Fact]
    public void The_brand_comes_from_the_prefix_the_scheme_publishes()
    {
        Assert.Equal(CardBrand.Visa, SavedCards.BrandOf("4111 1111 1111 1111"));
        Assert.Equal(CardBrand.Mastercard, SavedCards.BrandOf("5500 0000 0000 0004"));
        Assert.Equal(CardBrand.Mastercard, SavedCards.BrandOf("2223 0000 0000 0007"));
        Assert.Equal(CardBrand.Amex, SavedCards.BrandOf("3782 822463 10005"));
        Assert.Equal(CardBrand.Jcb, SavedCards.BrandOf("3530 1113 3330 0000"));
    }

    [Fact]
    public void A_domestic_card_is_recognised_by_its_napas_prefix()
    {
        // 9704 is the issuer prefix every Vietnamese ATM card carries.
        Assert.Equal(CardBrand.Napas, SavedCards.BrandOf("9704 0000 0000 0018"));
    }

    [Fact]
    public void A_number_too_short_to_place_is_not_guessed_at()
    {
        Assert.Equal(CardBrand.Unknown, SavedCards.BrandOf("4111"));
        Assert.Equal(CardBrand.Unknown, SavedCards.BrandOf(""));
        Assert.Equal(CardBrand.Unknown, SavedCards.BrandOf(null));
    }

    [Fact]
    public void A_mistyped_number_is_caught_before_the_bank_has_to_say_no()
    {
        // A refusal reads to the guest as their bank declining them; a typo
        // caught here reads as a typo.
        Assert.True(SavedCards.IsPlausibleNumber("4111 1111 1111 1111"));
        Assert.False(SavedCards.IsPlausibleNumber("4111 1111 1111 1112"));
        Assert.False(SavedCards.IsPlausibleNumber("4111"));
    }

    [Fact]
    public void Only_the_last_four_digits_survive()
    {
        Assert.Equal("1111", SavedCards.Last4Of("4111 1111 1111 1111"));
        Assert.Equal("", SavedCards.Last4Of("41"));
    }

    /* ---- expiry ---- */

    [Fact]
    public void A_card_is_good_until_the_end_of_its_expiry_month()
    {
        var card = Card(8, 2026);

        Assert.False(SavedCards.IsExpired(card, new DateOnly(2026, 8, 31)));
        Assert.True(SavedCards.IsExpired(card, new DateOnly(2026, 9, 1)));
    }

    [Fact]
    public void A_card_expiring_within_a_fortnight_is_worth_warning_about()
    {
        // docs/07 §4 — "nhắc khách cập nhật trước 14 ngày". A card running out on
        // 31/08 is inside the fortnight from 20/08 and outside it from 07/08.
        Assert.True(SavedCards.ExpiringSoon(Card(8, 2026), new DateOnly(2026, 8, 20)));
        Assert.False(SavedCards.ExpiringSoon(Card(8, 2026), Today));
        Assert.False(SavedCards.ExpiringSoon(Card(12, 2026), Today));
    }

    [Fact]
    public void A_card_already_expired_is_not_a_card_expiring_soon()
    {
        // It needs replacing, not a reminder that it is about to need replacing.
        var dead = Card(7, 2026);

        Assert.True(SavedCards.IsExpired(dead, Today));
        Assert.False(SavedCards.ExpiringSoon(dead, Today));
    }

    [Fact]
    public void What_is_shown_is_brand_last_four_and_expiry_and_nothing_else()
    {
        var shown = SavedCards.Display(Card(8, 2026));

        Assert.Equal("Visa •••• 4242 · 08/26", shown);
    }

    /* ---- removing one ---- */

    [Fact]
    public void A_card_with_money_still_to_be_taken_from_it_cannot_be_removed()
    {
        var block = SavedCards.CanRemove(hasScheduledCharge: true, hasOpenBooking: false);

        Assert.Equal(SavedCards.RemovalBlock.ScheduledCharge, block);
        Assert.Contains("lịch thu tự động", SavedCards.RemovalMessage(block));
    }

    [Fact]
    public void A_card_on_an_unfinished_booking_cannot_be_removed()
    {
        var block = SavedCards.CanRemove(hasScheduledCharge: false, hasOpenBooking: true);

        Assert.Equal(SavedCards.RemovalBlock.OpenBooking, block);
        Assert.Contains("chưa hoàn tất", SavedCards.RemovalMessage(block));
    }

    [Fact]
    public void A_card_doing_nothing_can_be_removed()
    {
        Assert.Equal(SavedCards.RemovalBlock.None, SavedCards.CanRemove(false, false));
    }

    [Fact]
    public void The_scheduled_charge_is_the_reason_the_guest_hears_first()
    {
        // Both are true; the one that would take money is the one to name.
        Assert.Equal(SavedCards.RemovalBlock.ScheduledCharge, SavedCards.CanRemove(true, true));
    }

    /* ---- which one is default ---- */

    [Fact]
    public void The_first_card_saved_becomes_the_default()
    {
        var cards = new List<SavedCard> { Card(8, 2027) };
        SavedCards.Reseat(cards);

        Assert.True(cards[0].IsDefault);
    }

    [Fact]
    public void Choosing_a_default_takes_it_off_the_previous_one()
    {
        var a = Card(8, 2027);
        a.Id = 1;
        a.IsDefault = true;
        var b = Card(9, 2027);
        b.Id = 2;

        SavedCards.Reseat([a, b], preferredId: 2);

        Assert.False(a.IsDefault);
        Assert.True(b.IsDefault);
    }

    [Fact]
    public void Removing_the_default_hands_the_title_on_rather_than_leaving_none()
    {
        // The automatic second charge of docs/07 §6 has to have something to use.
        var b = Card(9, 2027);
        b.Id = 2;

        SavedCards.Reseat([b]);

        Assert.True(b.IsDefault);
    }
}
