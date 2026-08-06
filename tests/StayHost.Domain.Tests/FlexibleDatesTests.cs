using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>docs/01 TM-06 and TM-07 — loose dates turned into concrete stays.</summary>
public class FlexibleDatesTests
{
    private static readonly DateOnly Today = new(2026, 9, 1);

    private static IReadOnlyList<StayWindow> Windows(FlexibleRequest req) =>
        FlexibleDates.Windows(req, Today);

    [Fact]
    public void Firm_dates_stay_a_single_window()
    {
        var windows = Windows(new FlexibleRequest
        {
            CheckIn = new DateOnly(2026, 9, 10),
            CheckOut = new DateOnly(2026, 9, 13)
        });

        Assert.Single(windows);
        Assert.Equal(3, windows[0].Nights);
    }

    [Fact]
    public void Plus_or_minus_three_days_keeps_the_length_of_the_stay()
    {
        var windows = Windows(new FlexibleRequest
        {
            CheckIn = new DateOnly(2026, 9, 10),
            CheckOut = new DateOnly(2026, 9, 13),
            FlexDays = 3
        });

        Assert.Equal(7, windows.Count);
        Assert.All(windows, w => Assert.Equal(3, w.Nights));

        // Nearest first: the guest's own dates lead, then one day either way.
        Assert.Equal(new DateOnly(2026, 9, 10), windows[0].CheckIn);
        Assert.Equal(new DateOnly(2026, 9, 9), windows[1].CheckIn);
        Assert.Equal(new DateOnly(2026, 9, 11), windows[2].CheckIn);
        Assert.Equal(new DateOnly(2026, 9, 7), windows.Min(w => w.CheckIn));
        Assert.Equal(new DateOnly(2026, 9, 13), windows.Max(w => w.CheckIn));
    }

    [Fact]
    public void A_shift_never_starts_a_stay_in_the_past()
    {
        var windows = Windows(new FlexibleRequest
        {
            CheckIn = Today.AddDays(1),
            CheckOut = Today.AddDays(3),
            FlexDays = 7
        });

        Assert.All(windows, w => Assert.True(w.CheckIn >= Today));
    }

    [Fact]
    public void The_spec_allows_seven_days_of_slack_at_most()
    {
        var windows = Windows(new FlexibleRequest
        {
            CheckIn = new DateOnly(2026, 10, 10),
            CheckOut = new DateOnly(2026, 10, 12),
            FlexDays = 30
        });

        Assert.Equal(new DateOnly(2026, 10, 3), windows.Min(w => w.CheckIn));
        Assert.Equal(new DateOnly(2026, 10, 17), windows.Max(w => w.CheckIn));
    }

    [Fact]
    public void A_weekend_means_friday_to_sunday()
    {
        var windows = Windows(new FlexibleRequest { Length = StayLength.Weekend });

        Assert.NotEmpty(windows);
        Assert.All(windows, w =>
        {
            Assert.Equal(DayOfWeek.Friday, w.CheckIn.DayOfWeek);
            Assert.Equal(2, w.Nights);
        });
    }

    [Fact]
    public void A_week_with_no_dates_looks_a_month_ahead()
    {
        var windows = Windows(new FlexibleRequest { Length = StayLength.Week });

        Assert.All(windows, w => Assert.Equal(7, w.Nights));
        Assert.Equal(Today, windows[0].CheckIn);
        Assert.True(windows.Count <= FlexibleDates.MaxWindows);
    }

    [Fact]
    public void Two_months_from_a_chosen_month_run_first_to_first()
    {
        var windows = Windows(new FlexibleRequest
        {
            Length = StayLength.Months,
            Months = 2,
            StartMonths = [new DateOnly(2026, 11, 20)]
        });

        Assert.Single(windows);
        Assert.Equal(new DateOnly(2026, 11, 1), windows[0].CheckIn);
        Assert.Equal(new DateOnly(2027, 1, 1), windows[0].CheckOut);
    }

    [Fact]
    public void Choosing_by_month_without_naming_one_offers_the_next_three()
    {
        var windows = Windows(new FlexibleRequest { Length = StayLength.Months, Months = 1 });

        Assert.Equal(3, windows.Count);
        Assert.Equal(new DateOnly(2026, 10, 1), windows[0].CheckIn);
        Assert.Equal(new DateOnly(2026, 12, 1), windows[^1].CheckIn);
    }

    [Fact]
    public void A_listing_is_offered_the_first_stay_nothing_sits_on()
    {
        var windows = Windows(new FlexibleRequest
        {
            CheckIn = new DateOnly(2026, 9, 10),
            CheckOut = new DateOnly(2026, 9, 12),
            FlexDays = 2
        });

        var booked = new[] { (new DateOnly(2026, 9, 9), new DateOnly(2026, 9, 11)) };
        var free = FlexibleDates.FirstFree(windows, booked);

        Assert.NotNull(free);
        Assert.Equal(new DateOnly(2026, 9, 11), free!.Value.CheckIn);
        Assert.False(free.Value.Overlaps(booked[0].Item1, booked[0].Item2));
    }

    [Fact]
    public void A_listing_booked_across_every_option_is_not_offered_at_all()
    {
        var windows = Windows(new FlexibleRequest
        {
            CheckIn = new DateOnly(2026, 9, 10),
            CheckOut = new DateOnly(2026, 9, 12),
            FlexDays = 2
        });

        var booked = new[] { (new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)) };

        Assert.Null(FlexibleDates.FirstFree(windows, booked));
    }

    [Fact]
    public void A_stay_ending_on_the_day_the_next_one_starts_is_not_an_overlap()
    {
        var window = new StayWindow(new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 12));

        Assert.False(window.Overlaps(new DateOnly(2026, 9, 12), new DateOnly(2026, 9, 14)));
        Assert.False(window.Overlaps(new DateOnly(2026, 9, 8), new DateOnly(2026, 9, 10)));
        Assert.True(window.Overlaps(new DateOnly(2026, 9, 11), new DateOnly(2026, 9, 14)));
    }
}
