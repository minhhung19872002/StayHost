using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>
/// The eight scenarios docs/03 §1 says must be checked, plus the rounding and
/// ordering rules around them. If one of these fails the money is wrong, which
/// docs/00 §6.1 treats as the most serious kind of bug there is.
/// </summary>
public class PricingTests
{
    private const decimal Nightly = 1_000_000m;
    private const decimal Cleaning = 350_000m;

    /// <summary>A Monday, so the default range never straddles a weekend by accident.</summary>
    private static readonly DateOnly Monday = new(2026, 9, 7);

    private static Listing MakeListing(Action<Listing>? tweak = null)
    {
        var l = new Listing
        {
            Id = 1,
            Title = "Test",
            City = "Đà Lạt",
            Country = "Việt Nam",
            PricePerNight = Nightly,
            CleaningFee = Cleaning,
            WeekendSurchargeRate = 0m,
            FreeGuestThreshold = 2,
            MaxGuests = 8
        };
        tweak?.Invoke(l);
        return l;
    }

    private static Pricing.Request Request(
        Listing listing, int nights, PartySize? party = null,
        IReadOnlyCollection<TaxRule>? taxes = null,
        IReadOnlyCollection<PriceRule>? rules = null,
        DateOnly? checkIn = null,
        DateOnly? bookedOn = null) =>
        new()
        {
            Listing = listing,
            CheckIn = checkIn ?? Monday,
            CheckOut = (checkIn ?? Monday).AddDays(nights),
            Party = party ?? new PartySize(2),
            TaxRules = taxes ?? [],
            PriceRules = rules ?? [],
            // Far enough ahead that no booking-time discount fires unless a test asks for one.
            BookedOn = bookedOn ?? (checkIn ?? Monday).AddDays(-3)
        };

    /* -------------------------------------------------- scenario 1 of eight */

    [Fact]
    public void Three_nights_with_no_discounts_is_room_plus_cleaning_plus_14_percent()
    {
        var price = Pricing.Quote(Request(MakeListing(), nights: 3));

        Assert.Equal(3_000_000m, price.RoomBeforeDiscount);
        Assert.Equal(0m, price.RoomDiscount);
        Assert.Equal(3_350_000m, price.Subtotal);
        Assert.Equal(469_000m, price.GuestServiceFee);          // 14% of the subtotal
        Assert.Equal(0m, price.Tax);                            // no tax rule configured for Đà Lạt
        Assert.Equal(3_819_000m, price.Total);
    }

    /* -------------------------------------------------- scenario 2 of eight */

    [Fact]
    public void Weekly_discount_touches_the_room_charge_only()
    {
        var listing = MakeListing(l => l.WeeklyDiscountPercent = 10);
        var price = Pricing.Quote(Request(listing, nights: 7));

        Assert.Equal(7_000_000m, price.RoomBeforeDiscount);
        Assert.Equal(700_000m, price.RoomDiscount);
        Assert.Equal(Cleaning, price.CleaningFee);              // untouched by the discount
        Assert.Equal(6_650_000m, price.Subtotal);
    }

    /* -------------------------------------------------- scenario 3 of eight */

    [Fact]
    public void Thirty_nights_uses_the_monthly_tier_and_drops_the_weekly_one()
    {
        var listing = MakeListing(l =>
        {
            l.WeeklyDiscountPercent = 10;
            l.MonthlyDiscountPercent = 20;
        });

        var price = Pricing.Quote(Request(listing, nights: 30));

        Assert.Equal(20, price.DiscountPercent);
        Assert.Single(price.DiscountParts);
        Assert.Equal("length-month", price.DiscountParts[0].Key);
    }

    /* -------------------------------------------------- scenario 4 of eight */

    [Fact]
    public void A_day_price_on_two_of_three_nights_leaves_one_night_at_the_base_rate()
    {
        var rules = new[]
        {
            new PriceRule { Id = 1, Kind = PriceRuleKind.DayOverride, From = Monday, To = Monday.AddDays(1), NightlyRate = 1_500_000m }
        };

        var price = Pricing.Quote(Request(MakeListing(), nights: 3, rules: rules));

        Assert.Equal(["day", "day", "base"], price.Nightly.Select(n => n.Source).ToArray());
        Assert.Equal(1_500_000m + 1_500_000m + Nightly, price.RoomBeforeDiscount);
    }

    [Fact]
    public void A_day_price_beats_a_season_on_the_same_night()
    {
        var rules = new[]
        {
            new PriceRule { Id = 1, Kind = PriceRuleKind.Season, From = Monday, To = Monday.AddDays(10), NightlyRate = 2_000_000m },
            new PriceRule { Id = 2, Kind = PriceRuleKind.DayOverride, From = Monday, To = Monday, NightlyRate = 900_000m }
        };

        var price = Pricing.Quote(Request(MakeListing(), nights: 2, rules: rules));

        Assert.Equal(900_000m, price.Nightly[0].Rate);
        Assert.Equal(2_000_000m, price.Nightly[1].Rate);
    }

    [Fact]
    public void A_season_beats_the_weekend_rate()
    {
        var friday = new DateOnly(2026, 9, 11);
        var listing = MakeListing(l => l.WeekendSurchargeRate = 0.5m);
        var rules = new[]
        {
            new PriceRule { Id = 1, Kind = PriceRuleKind.Season, From = friday, To = friday, NightlyRate = 1_200_000m }
        };

        var price = Pricing.Quote(Request(listing, nights: 1, rules: rules, checkIn: friday));

        Assert.Equal(1_200_000m, price.RoomBeforeDiscount);
    }

    [Fact]
    public void Friday_and_saturday_carry_the_weekend_rate()
    {
        var friday = new DateOnly(2026, 9, 11);
        var listing = MakeListing(l => l.WeekendSurchargeRate = 0.2m);

        var price = Pricing.Quote(Request(listing, nights: 3, checkIn: friday));

        Assert.Equal(["weekend", "weekend", "base"], price.Nightly.Select(n => n.Source).ToArray());
        Assert.Equal(1_200_000m + 1_200_000m + Nightly, price.RoomBeforeDiscount);
    }

    /* -------------------------------------------------- scenario 5 of eight */

    [Fact]
    public void Five_guests_over_a_threshold_of_two_are_charged_for_three()
    {
        var listing = MakeListing(l => l.ExtraGuestFee = 200_000m);
        var price = Pricing.Quote(Request(listing, nights: 3, party: new PartySize(Adults: 5)));

        Assert.Equal(3 * 200_000m * 3, price.ExtraGuestFee);
    }

    /* -------------------------------------------------- scenario 6 of eight */

    [Fact]
    public void Infants_never_trigger_the_extra_guest_surcharge()
    {
        var listing = MakeListing(l => l.ExtraGuestFee = 200_000m);
        var price = Pricing.Quote(Request(listing, nights: 3, party: new PartySize(Adults: 2, Infants: 1)));

        Assert.Equal(0m, price.ExtraGuestFee);
    }

    /* -------------------------------------------------- scenario 7 of eight */

    [Fact]
    public void Percentages_add_instead_of_compounding()
    {
        var listing = MakeListing(l =>
        {
            l.WeeklyDiscountPercent = 10;
            l.EarlyBirdDays = 60;
            l.EarlyBirdPercent = 15;
        });

        var price = Pricing.Quote(Request(listing, nights: 7, bookedOn: Monday.AddDays(-90)));

        Assert.Equal(25, price.DiscountPercent);                 // not 23.5
        Assert.Equal(7_000_000m * 0.25m, price.RoomDiscount);
    }

    /* -------------------------------------------------- scenario 8 of eight */

    [Fact]
    public void Total_discount_is_capped_at_sixty_percent()
    {
        var listing = MakeListing(l =>
        {
            l.MonthlyDiscountPercent = 40;
            l.EarlyBirdDays = 30;
            l.EarlyBirdPercent = 30;
        });

        var price = Pricing.Quote(Request(listing, nights: 30, bookedOn: Monday.AddDays(-90)) with
        {
            ListingBookingCount = 0    // adds another 20%, taking the raw total to 90%
        });

        Assert.Equal(60, price.DiscountPercent);
    }

    /* --------------------------------------------------------- other rules */

    [Fact]
    public void Only_the_larger_of_early_bird_and_last_minute_applies()
    {
        var listing = MakeListing(l =>
        {
            l.EarlyBirdDays = 1;
            l.EarlyBirdPercent = 5;
            l.LastMinuteDays = 7;
            l.LastMinutePercent = 12;
        });

        var price = Pricing.Quote(Request(listing, nights: 2, bookedOn: Monday.AddDays(-2)));

        Assert.Equal(12, price.DiscountPercent);
        Assert.Equal("last-minute", price.DiscountParts.Single().Key);
    }

    [Fact]
    public void The_first_three_stays_on_a_listing_get_the_new_listing_discount()
    {
        var price = Pricing.Quote(Request(MakeListing(), nights: 2) with { ListingBookingCount = 2 });
        Assert.Equal(20, price.DiscountPercent);

        var fourth = Pricing.Quote(Request(MakeListing(), nights: 2) with { ListingBookingCount = 3 });
        Assert.Equal(0, fourth.DiscountPercent);
    }

    [Fact]
    public void Pet_fee_can_be_charged_per_stay_or_per_night()
    {
        var perStay = MakeListing(l => { l.PetsAllowed = true; l.PetFee = 300_000m; });
        Assert.Equal(300_000m, Pricing.Quote(Request(perStay, 3, new PartySize(2, Pets: 1))).PetFee);

        var perNight = MakeListing(l => { l.PetsAllowed = true; l.PetFee = 100_000m; l.PetFeePerNight = true; });
        Assert.Equal(300_000m, Pricing.Quote(Request(perNight, 3, new PartySize(2, Pets: 1))).PetFee);
    }

    [Fact]
    public void The_cleaning_fee_is_charged_once_no_matter_how_long_the_stay()
    {
        var three = Pricing.Quote(Request(MakeListing(), nights: 3));
        var ten = Pricing.Quote(Request(MakeListing(), nights: 10));

        Assert.Equal(Cleaning, three.CleaningFee);
        Assert.Equal(Cleaning, ten.CleaningFee);
    }

    /* ------------------------------------------------------------------ tax */

    [Fact]
    public void Regional_taxes_stack_and_support_all_four_methods()
    {
        var taxes = new[]
        {
            new TaxRule { Id = 1, City = "Đà Lạt", Name = "VAT 8%", Method = TaxMethod.Percentage, Base = TaxBase.SubtotalPlusGuestFee, Value = 0.08m, SortOrder = 1 },
            new TaxRule { Id = 2, City = "Đà Lạt", Name = "Phí lưu trú mỗi đêm", Method = TaxMethod.PerNight, Value = 20_000m, SortOrder = 2 },
            new TaxRule { Id = 3, City = "Đà Lạt", Name = "Phí du lịch mỗi khách/đêm", Method = TaxMethod.PerGuestPerNight, Value = 5_000m, SortOrder = 3 },
            new TaxRule { Id = 4, Name = "Phí đăng ký lưu trú", Method = TaxMethod.PerStay, Value = 30_000m, SortOrder = 4 }
        };

        var price = Pricing.Quote(Request(MakeListing(), nights: 3, taxes: taxes));

        // 8% of (3,350,000 + 469,000) = 305,520
        Assert.Equal(4, price.TaxLines.Count);
        Assert.Equal(305_520m, price.TaxLines[0].Amount);
        Assert.Equal(60_000m, price.TaxLines[1].Amount);        // 20k × 3 nights
        Assert.Equal(30_000m, price.TaxLines[2].Amount);        // 5k × 2 guests × 3 nights
        Assert.Equal(30_000m, price.TaxLines[3].Amount);
        Assert.Equal(425_520m, price.Tax);
    }

    [Fact]
    public void A_tax_rule_for_another_city_is_ignored()
    {
        var taxes = new[]
        {
            new TaxRule { Id = 1, City = "Đà Nẵng", Name = "VAT", Method = TaxMethod.Percentage, Value = 0.08m }
        };

        Assert.Equal(0m, Pricing.Quote(Request(MakeListing(), nights: 3, taxes: taxes)).Tax);
    }

    /* -------------------------------------------------- fees and rounding */

    [Fact]
    public void The_host_keeps_the_subtotal_less_three_percent()
    {
        var price = Pricing.Quote(Request(MakeListing(), nights: 3));

        Assert.Equal(100_500m, price.HostServiceFee);           // 3% of 3,350,000
        Assert.Equal(3_249_500m, price.HostPayout);
    }

    [Fact]
    public void The_total_is_exactly_the_sum_of_the_displayed_lines()
    {
        var listing = MakeListing(l =>
        {
            l.WeeklyDiscountPercent = 13;                       // deliberately awkward percentages
            l.ExtraGuestFee = 133_333m;
            l.PetsAllowed = true;
            l.PetFee = 77_777m;
            l.CleaningFee = 333_333m;
        });

        var taxes = new[]
        {
            new TaxRule { Id = 1, City = "Đà Lạt", Name = "VAT", Method = TaxMethod.Percentage, Value = 0.081m }
        };

        var price = Pricing.Quote(Request(listing, nights: 7, new PartySize(4, Pets: 1), taxes));

        Assert.Equal(price.Total, price.Lines.Sum(l => l.Amount));
        Assert.All(price.Lines, l => Assert.Equal(decimal.Round(l.Amount), l.Amount));
    }

    [Fact]
    public void A_promotion_comes_off_last_and_never_takes_the_total_negative()
    {
        var price = Pricing.Quote(Request(MakeListing(), nights: 3) with { PromotionAmount = 500_000m });
        Assert.Equal(3_319_000m, price.Total);

        var huge = Pricing.Quote(Request(MakeListing(), nights: 3) with { PromotionAmount = 99_000_000m });
        Assert.Equal(0m, huge.Total);
    }

    /* ---- docs/01 ĐP-09 — a promo code alongside the balance ---- */

    [Fact]
    public void A_coupon_and_the_balance_are_two_separate_lines()
    {
        // docs/03 §1 step 9, docs/07 §3 — a code and the guest's balance are
        // different money and a receipt shows them apart, not merged into one row.
        var price = Pricing.Quote(Request(MakeListing(), nights: 3) with
        {
            CouponAmount = 300_000m,
            PromotionAmount = 200_000m
        });

        Assert.Equal(300_000m, price.Coupon);
        Assert.Equal(200_000m, price.Promotion);
        Assert.Equal(500_000m, price.Coupon + price.Promotion);

        Assert.Contains(price.Lines, l => l.Key == "coupon" && l.Amount == -300_000m);
        Assert.Contains(price.Lines, l => l.Key == "promotion" && l.Amount == -200_000m);
    }

    [Fact]
    public void The_coupon_comes_off_before_the_balance_so_the_balance_covers_less()
    {
        // With a code applied, the balance only has to cover what is left, so a
        // guest keeps more of it than if the two stacked in the other order.
        var gross = Pricing.Quote(Request(MakeListing(), nights: 3));
        var whole = gross.Subtotal + gross.GuestServiceFee + gross.Tax;

        var price = Pricing.Quote(Request(MakeListing(), nights: 3) with
        {
            CouponAmount = 300_000m,
            PromotionAmount = 99_000_000m   // more balance than the stay could ever use
        });

        // Coupon takes its 300k; the balance is capped at the remainder, never more.
        Assert.Equal(300_000m, price.Coupon);
        Assert.Equal(whole - 300_000m, price.Promotion);
        Assert.Equal(0m, price.Total);
    }

    [Fact]
    public void A_coupon_alone_never_takes_the_total_negative()
    {
        var huge = Pricing.Quote(Request(MakeListing(), nights: 3) with { CouponAmount = 99_000_000m });
        Assert.Equal(0m, huge.Total);
        Assert.Equal(0m, huge.Promotion);
    }
}
