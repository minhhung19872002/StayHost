using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>docs/01 MR-08 → MR-10 — rooms of a kind, counted rather than exclusive.</summary>
public class HotelTests
{
    private static readonly DateOnly CheckIn = new(2026, 9, 10);
    private static readonly DateOnly CheckOut = new(2026, 9, 13);

    private static RoomTypeOption Room(int inventory = 3, int maxGuests = 2) =>
        new() { Id = 1, Name = "Phòng Deluxe", Inventory = inventory, MaxGuests = maxGuests, PricePerNight = 1_500_000m };

    /* ------------------------------------------------------------- MR-08 */

    [Fact]
    public void A_room_is_available_while_the_property_still_has_one()
    {
        Assert.True(HotelRules.CanBook(Room(inventory: 3), 2, takenOnBusiestNight: 2).Ok);
    }

    [Fact]
    public void The_last_room_of_a_kind_sells_out_that_kind_and_nothing_else()
    {
        var check = HotelRules.CanBook(Room(inventory: 3), 2, takenOnBusiestNight: 3);

        Assert.False(check.Ok);
        Assert.Equal(HotelRules.Refusal.SoldOut, check.Reason);
        Assert.Contains("Phòng Deluxe", check.Message);
    }

    [Fact]
    public void A_hotel_booking_needs_a_room_type_named()
    {
        Assert.Equal(
            HotelRules.Refusal.UnknownRoomType,
            HotelRules.CanBook(null, 2, 0).Reason);
    }

    [Fact]
    public void A_party_larger_than_the_room_takes_is_refused()
    {
        Assert.Equal(
            HotelRules.Refusal.TooManyGuests,
            HotelRules.CanBook(Room(maxGuests: 2), 4, 0).Reason);
    }

    [Fact]
    public void Occupancy_is_counted_per_night_not_per_booking()
    {
        // Three bookings across the stay, but never more than two on one night:
        // 10-11, 11-13, 12-13.
        var taken = new[]
        {
            (new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 11)),
            (new DateOnly(2026, 9, 11), new DateOnly(2026, 9, 13)),
            (new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 13))
        };

        Assert.Equal(2, HotelRules.PeakOccupancy(CheckIn, CheckOut, taken));
    }

    [Fact]
    public void A_booking_that_leaves_as_another_arrives_does_not_stack()
    {
        var taken = new[]
        {
            (new DateOnly(2026, 9, 8), new DateOnly(2026, 9, 10)),
            (new DateOnly(2026, 9, 13), new DateOnly(2026, 9, 15))
        };

        Assert.Equal(0, HotelRules.PeakOccupancy(CheckIn, CheckOut, taken));
    }

    [Fact]
    public void An_empty_property_has_nobody_in_it()
    {
        Assert.Equal(0, HotelRules.PeakOccupancy(CheckIn, CheckOut, []));
    }

    /* ------------------------------------------------------------- MR-09 */

    [Fact]
    public void The_room_type_chosen_replaces_the_listings_base_rate()
    {
        var listing = new Listing
        {
            Id = 1, City = "Đà Nẵng", Country = "Việt Nam", Type = PlaceType.Hotel,
            PricePerNight = 980_000m, CleaningFee = 0, WeekendSurchargeRate = 0m
        };

        var cheapest = Pricing.Quote(new Pricing.Request
        { Listing = listing, CheckIn = CheckIn, CheckOut = CheckOut, Party = new PartySize(2) });

        var suite = Pricing.Quote(new Pricing.Request
        {
            Listing = listing, CheckIn = CheckIn, CheckOut = CheckOut, Party = new PartySize(2),
            NightlyRateOverride = 5_900_000m
        });

        Assert.Equal(2_940_000m, cheapest.RoomBeforeDiscount);   // 3 × 980k
        Assert.Equal(17_700_000m, suite.RoomBeforeDiscount);     // 3 × 5.9m
    }

    [Fact]
    public void A_season_still_beats_the_room_rate()
    {
        var listing = new Listing { Id = 1, PricePerNight = 980_000m, WeekendSurchargeRate = 0m };
        var tet = new PriceRule
        {
            Id = 1, ListingId = 1, Kind = PriceRuleKind.Season, Name = "Tết",
            From = CheckIn, To = CheckOut, NightlyRate = 9_000_000m
        };

        var rate = Pricing.RateFor(listing, CheckIn, [tet], rateOverride: 5_900_000m);

        Assert.Equal(9_000_000m, rate.Rate);
        Assert.Equal("season", rate.Source);
    }

    /* ------------------------------------------------------------- MR-10 */

    [Fact]
    public void A_price_match_is_the_nightly_gap_across_every_night()
    {
        Assert.Equal(600_000m, HotelRules.MatchValue(1_500_000m, 1_300_000m, 3));
    }

    [Fact]
    public void A_competitor_who_is_not_cheaper_is_worth_nothing()
    {
        Assert.Equal(0m, HotelRules.MatchValue(1_500_000m, 1_500_000m, 3));
        Assert.Equal(0m, HotelRules.MatchValue(1_500_000m, 1_800_000m, 3));
    }

    [Fact]
    public void A_gap_too_small_to_matter_is_not_a_claim()
    {
        Assert.Equal(0m, HotelRules.MatchValue(1_500_000m, 1_495_000m, 3));
        Assert.Equal(30_000m, HotelRules.MatchValue(1_500_000m, 1_490_000m, 3));
    }

    [Fact]
    public void A_claim_may_only_be_raised_within_a_day_of_booking()
    {
        var booked = new DateTime(2026, 9, 1, 10, 0, 0, DateTimeKind.Utc);

        Assert.True(HotelRules.WithinWindow(booked, booked.AddHours(23)));
        Assert.False(HotelRules.WithinWindow(booked, booked.AddHours(25)));
    }

    [Fact]
    public void Granting_the_difference_as_balance_balances()
    {
        var booking = new Booking { Id = 1, Reference = "SH1" };

        var entries = Ledger.GrantCredit(booking, 600_000m, "Bù chênh lệch", DateTime.UtcNow);

        Assert.Equal(0m, Ledger.Imbalance(entries));
        Assert.Contains(entries, e => e.Account == LedgerAccount.PromotionalCredit);
        Assert.Contains(entries, e => e.Account == LedgerAccount.PlatformExpense);
        Assert.Empty(Ledger.GrantCredit(booking, 0m, "Không có gì", DateTime.UtcNow));
    }
}
