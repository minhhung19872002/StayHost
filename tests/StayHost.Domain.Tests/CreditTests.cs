using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>Gift cards, balance and referrals — the platform's own money.</summary>
public class CreditTests
{
    private static readonly DateTime At = new(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Balance_only_covers_the_room_charge()
    {
        // Plenty of balance, but the room is all it may be spent on.
        Assert.Equal(3_000_000m, CreditRules.Spendable(balance: 5_000_000m, roomAfterDiscount: 3_000_000m));

        // Less balance than room: all of it goes in.
        Assert.Equal(500_000m, CreditRules.Spendable(balance: 500_000m, roomAfterDiscount: 3_000_000m));
    }

    [Fact]
    public void A_guest_with_no_balance_spends_nothing()
    {
        Assert.Equal(0m, CreditRules.Spendable(0m, 3_000_000m));
        Assert.Equal(0m, CreditRules.Spendable(-100m, 3_000_000m));
    }

    [Fact]
    public void Codes_avoid_the_characters_people_misread()
    {
        Assert.DoesNotContain('O', CreditRules.Alphabet);
        Assert.DoesNotContain('0', CreditRules.Alphabet);
        Assert.DoesNotContain('I', CreditRules.Alphabet);
        Assert.DoesNotContain('1', CreditRules.Alphabet);

        var code = CreditRules.NewCode("GC", _ => 0);
        Assert.Equal("GC-AAAAAAAAAA", code);
    }

    [Fact]
    public void Only_a_card_with_something_left_can_be_redeemed()
    {
        Assert.True(CreditRules.CanRedeem(new GiftCard { Status = GiftCardStatus.Active, Remaining = 500_000m }));
        Assert.False(CreditRules.CanRedeem(new GiftCard { Status = GiftCardStatus.Active, Remaining = 0m }));
        Assert.False(CreditRules.CanRedeem(new GiftCard { Status = GiftCardStatus.Redeemed, Remaining = 500_000m }));
        Assert.False(CreditRules.CanRedeem(new GiftCard { Status = GiftCardStatus.Cancelled, Remaining = 500_000m }));
    }

    [Fact]
    public void Selling_a_card_and_redeeming_it_moves_a_debt_rather_than_creating_one()
    {
        var sold = Ledger.SellGiftCard(1_000_000m, "GC-TEST", At);
        var redeemed = Ledger.RedeemGiftCard(1_000_000m, "GC-TEST", At);

        Assert.Equal(0m, Ledger.Imbalance(sold));
        Assert.Equal(0m, Ledger.Imbalance(redeemed));

        // The card liability is gone; the balance owed to the guest took its place.
        Assert.Equal(0m, Net(sold.Concat(redeemed), LedgerAccount.GiftCardLiability));
        Assert.Equal(-1_000_000m, Net(sold.Concat(redeemed), LedgerAccount.PromotionalCredit));

        // Nothing was given away: a gift card is bought, not granted.
        Assert.Equal(0m, Net(sold.Concat(redeemed), LedgerAccount.PlatformExpense));
    }

    [Fact]
    public void Spending_balance_discharges_the_liability_instead_of_costing_twice()
    {
        var (booking, price) = Sell(balance: 500_000m);

        var entries = Ledger.CaptureBooking(booking, price, At, paidNow: null, creditUsed: 500_000m);

        Assert.Equal(0m, Ledger.Imbalance(entries));

        // The whole promotion came out of balance, so the platform books no
        // further expense — it paid for that credit when it granted it.
        Assert.Equal(500_000m, Sum(entries, LedgerAccount.PromotionalCredit, LedgerDirection.Debit));
        Assert.Equal(0m, Sum(entries, LedgerAccount.PlatformExpense, LedgerDirection.Debit));
    }

    [Fact]
    public void A_promo_code_the_platform_gives_up_is_an_expense()
    {
        // docs/01 TC-09 — a coupon is money the platform gives up now, so it lands
        // in PlatformExpense. Balance does not: it was expensed when granted.
        var (booking, price) = Sell(coupon: 500_000m);

        var entries = Ledger.CaptureBooking(booking, price, At);

        Assert.Equal(0m, Ledger.Imbalance(entries));
        Assert.Equal(500_000m, Sum(entries, LedgerAccount.PlatformExpense, LedgerDirection.Debit));
        Assert.Equal(0m, Sum(entries, LedgerAccount.PromotionalCredit, LedgerDirection.Debit));
    }

    [Fact]
    public void A_coupon_and_the_balance_land_in_different_accounts()
    {
        // docs/03 §1 step 9 — both are step-9 reductions but they are not the same
        // money: the code is an expense, the balance discharges a prior liability.
        var (booking, price) = Sell(balance: 200_000m, coupon: 300_000m);

        var entries = Ledger.CaptureBooking(booking, price, At, paidNow: null, creditUsed: 200_000m);

        Assert.Equal(0m, Ledger.Imbalance(entries));
        Assert.Equal(200_000m, Sum(entries, LedgerAccount.PromotionalCredit, LedgerDirection.Debit));
        Assert.Equal(300_000m, Sum(entries, LedgerAccount.PlatformExpense, LedgerDirection.Debit));
    }

    [Fact]
    public void Credit_claimed_beyond_the_balance_line_is_ignored_rather_than_unbalancing()
    {
        var (booking, price) = Sell(balance: 200_000m);

        var entries = Ledger.CaptureBooking(booking, price, At, paidNow: null, creditUsed: 900_000m);

        Assert.Equal(0m, Ledger.Imbalance(entries));
        Assert.Equal(200_000m, Sum(entries, LedgerAccount.PromotionalCredit, LedgerDirection.Debit));
    }

    [Fact]
    public void A_referral_reward_is_the_platforms_own_expense()
    {
        var entries = Ledger.GrantCredit(null, 500_000m, "Thưởng giới thiệu", At);

        Assert.Equal(0m, Ledger.Imbalance(entries));
        Assert.Equal(500_000m, Sum(entries, LedgerAccount.PlatformExpense, LedgerDirection.Debit));
        Assert.Equal(500_000m, Sum(entries, LedgerAccount.PromotionalCredit, LedgerDirection.Credit));
    }

    [Fact]
    public void Both_sides_of_a_referral_are_worth_something()
    {
        Assert.True(CreditRules.ReferrerReward > 0);
        Assert.True(CreditRules.InviteeReward > 0);
        Assert.True(CreditRules.MinGiftCard < CreditRules.MaxGiftCard);
    }

    private static (Booking Booking, Pricing.Breakdown Price) Sell(decimal balance = 0, decimal coupon = 0)
    {
        var checkIn = new DateOnly(2026, 10, 7);
        var listing = new Listing
        {
            Id = 1, City = "Đà Lạt", Country = "Việt Nam",
            PricePerNight = 1_000_000m, CleaningFee = 500_000m, WeekendSurchargeRate = 0m
        };

        var price = Pricing.Quote(new Pricing.Request
        {
            Listing = listing,
            CheckIn = checkIn,
            CheckOut = checkIn.AddDays(5),
            Party = new PartySize(2),
            PromotionAmount = balance,
            CouponAmount = coupon
        });

        var booking = new Booking { Id = 1, Reference = "SH-1", Total = price.Total };
        return (booking, price);
    }

    private static decimal Sum(
        IEnumerable<LedgerEntry> entries, LedgerAccount account, LedgerDirection direction) =>
        entries.Where(e => e.Account == account && e.Direction == direction).Sum(e => e.Amount);

    private static decimal Net(IEnumerable<LedgerEntry> entries, LedgerAccount account) =>
        entries.Where(e => e.Account == account).Sum(e => e.Signed);

    /* ------------------------------------------- TC-08, the unpaid card */

    /// <summary>
    /// The hole this closed: buying a card created it Active on the spot, with
    /// no payment anywhere in the path, and redeeming turned it into balance
    /// spendable on a real stay. The rule that stops it was already written —
    /// CanRedeem has always insisted on Active — there had simply never been an
    /// unpaid state for it to say no to.
    /// </summary>
    [Fact]
    public void A_card_awaiting_payment_cannot_be_redeemed()
    {
        var ordered = new GiftCard
        {
            Code = "GC-TEST", Amount = 20_000_000m, Remaining = 20_000_000m,
            Status = GiftCardStatus.AwaitingPayment
        };
        Assert.False(CreditRules.CanRedeem(ordered));

        // Paying for it is the only thing that changes the answer.
        ordered.Status = GiftCardStatus.Active;
        Assert.True(CreditRules.CanRedeem(ordered));
    }

    [Fact]
    public void An_unpaid_card_does_not_read_as_valid()
    {
        // The default arm of the label used to answer "Còn hiệu lực" for anything
        // it did not recognise, which would have called an unpaid card good.
        Assert.Equal("Chờ thanh toán", CreditRules.StatusLabel(GiftCardStatus.AwaitingPayment));
        Assert.Equal("Còn hiệu lực", CreditRules.StatusLabel(GiftCardStatus.Active));
    }

    [Fact]
    public void Balance_cannot_buy_balance()
    {
        Assert.False(GiftCardPurchase.CanPayWith("balance"));
        Assert.Contains("số dư", GiftCardPurchase.Refusal("balance"));

        // Nor can the ways of paying that only exist around a stay.
        Assert.False(GiftCardPurchase.CanPayWith(PayAtProperty.Key));
        Assert.False(GiftCardPurchase.CanPayWith(""));
        Assert.False(GiftCardPurchase.CanPayWith(null));
    }

    [Fact]
    public void A_card_is_bought_with_the_methods_that_take_money()
    {
        foreach (var method in new[] { "card", "napas", "momo", "zalopay" })
            Assert.True(GiftCardPurchase.CanPayWith(method), method);
    }
}
