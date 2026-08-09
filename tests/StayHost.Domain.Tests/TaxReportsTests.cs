namespace StayHost.Domain.Tests;

/// <summary>docs/01 TC-04, docs/02 G7 — the host's tax report for a year.</summary>
public class TaxReportsTests
{
    private static TaxReports.Stay Stay(
        string reference, DateOnly checkOut, decimal guestPaid = 5_000_000m,
        decimal tax = 500_000m, string taxName = "Thuế lưu trú",
        DateOnly? paidOutOn = null, decimal hostFee = 150_000m, decimal payout = 4_350_000m) =>
        new(reference, "Nhà thử", checkOut.AddDays(-2), checkOut, paidOutOn,
            guestPaid, guestPaid - tax, 0m, hostFee, payout,
            tax > 0 ? [new PriceLine("tax-1", taxName, tax)] : []);

    /* ---- which year a stay belongs to ---- */

    [Fact]
    public void A_stay_counts_in_the_year_it_ended()
    {
        var newYearsEve = Stay("A", new DateOnly(2025, 12, 31));
        var newYearsDay = Stay("B", new DateOnly(2026, 1, 1));

        Assert.Equal(1, TaxReports.Build([newYearsEve, newYearsDay], 2025).Stays);
        Assert.Equal(1, TaxReports.Build([newYearsEve, newYearsDay], 2026).Stays);
    }

    [Fact]
    public void When_the_money_arrived_does_not_move_the_stay_between_years()
    {
        // A December checkout paid out in January stays in December. The payout
        // date rides along on the row so a cash-basis reader can re-cut it.
        var stay = Stay("A", new DateOnly(2025, 12, 28), paidOutOn: new DateOnly(2026, 1, 4));

        Assert.Equal(1, TaxReports.Build([stay], 2025).Stays);
        Assert.Equal(0, TaxReports.Build([stay], 2026).Stays);
        Assert.Equal(new DateOnly(2026, 1, 4), stay.PaidOutOn);
    }

    [Fact]
    public void Years_covered_lists_the_newest_first_and_each_one_once()
    {
        var stays = new[]
        {
            Stay("A", new DateOnly(2024, 5, 1)),
            Stay("B", new DateOnly(2026, 2, 1)),
            Stay("C", new DateOnly(2024, 9, 1))
        };

        Assert.Equal([2026, 2024], TaxReports.YearsCovered(stays));
    }

    /* ---- the shape of the year ---- */

    [Fact]
    public void Every_month_is_present_even_the_empty_ones()
    {
        // A year with a gap in it must still read as twelve rows; a table that
        // skips March makes the reader count instead of read.
        var report = TaxReports.Build([Stay("A", new DateOnly(2026, 3, 10))], 2026);

        Assert.Equal(12, report.Months.Count);
        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12], report.Months.Select(m => m.Month));
        Assert.Equal(0, report.Months.Single(m => m.Month == 4).Stays);
    }

    [Fact]
    public void A_month_adds_up_what_happened_in_it()
    {
        var report = TaxReports.Build(
        [
            Stay("A", new DateOnly(2026, 6, 3), guestPaid: 4_000_000m, tax: 400_000m, hostFee: 100_000m, payout: 3_500_000m),
            Stay("B", new DateOnly(2026, 6, 20), guestPaid: 6_000_000m, tax: 600_000m, hostFee: 200_000m, payout: 5_200_000m),
            Stay("C", new DateOnly(2026, 7, 2), guestPaid: 1_000_000m, tax: 100_000m)
        ], 2026);

        var june = report.Months.Single(m => m.Month == 6);

        Assert.Equal(2, june.Stays);
        Assert.Equal(10_000_000m, june.GuestPaid);
        Assert.Equal(1_000_000m, june.Tax);
        Assert.Equal(300_000m, june.HostServiceFee);
        Assert.Equal(8_700_000m, june.HostPayout);
    }

    [Fact]
    public void The_year_total_is_the_sum_of_its_months()
    {
        var report = TaxReports.Build(
        [
            Stay("A", new DateOnly(2026, 2, 3)),
            Stay("B", new DateOnly(2026, 8, 9)),
            Stay("C", new DateOnly(2026, 8, 25))
        ], 2026);

        Assert.Equal(report.Months.Sum(m => m.GuestPaid), report.GuestPaid);
        Assert.Equal(report.Months.Sum(m => m.Tax), report.Tax);
        Assert.Equal(report.Months.Sum(m => m.HostPayout), report.HostPayout);
        Assert.Equal(report.Months.Sum(m => m.Stays), report.Stays);
    }

    /* ---- taxes broken out by name ---- */

    [Fact]
    public void Each_tax_is_totalled_under_the_name_the_guest_was_shown()
    {
        // docs/03 §1 step 8 allows several taxes stacked on one stay, and a rule
        // renamed later must not retitle what was already charged.
        var stay = new TaxReports.Stay(
            "A", "Nhà thử", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 4), null,
            8_000_000m, 7_000_000m, 900_000m, 210_000m, 6_790_000m,
            [new PriceLine("tax-1", "Thuế lưu trú", 500_000m), new PriceLine("tax-2", "Phí môi trường", 100_000m)]);

        var report = TaxReports.Build([stay], 2026);

        Assert.Equal(600_000m, report.Tax);
        Assert.Equal(500_000m, report.Taxes.Single(t => t.Name == "Thuế lưu trú").Amount);
        Assert.Equal(100_000m, report.Taxes.Single(t => t.Name == "Phí môi trường").Amount);
    }

    [Fact]
    public void The_same_tax_across_several_stays_is_one_line()
    {
        var report = TaxReports.Build(
        [
            Stay("A", new DateOnly(2026, 1, 5), tax: 300_000m),
            Stay("B", new DateOnly(2026, 5, 5), tax: 200_000m)
        ], 2026);

        var line = Assert.Single(report.Taxes);
        Assert.Equal(500_000m, line.Amount);
        Assert.Equal(2, line.Stays);
    }

    [Fact]
    public void Taxes_are_listed_largest_first()
    {
        var stay = new TaxReports.Stay(
            "A", "Nhà thử", new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 4), null,
            8_000_000m, 7_000_000m, 900_000m, 210_000m, 6_790_000m,
            [new PriceLine("tax-1", "Nhỏ", 50_000m), new PriceLine("tax-2", "Lớn", 900_000m)]);

        Assert.Equal("Lớn", TaxReports.Build([stay], 2026).Taxes.First().Name);
    }

    [Fact]
    public void A_year_with_no_stays_is_an_empty_report_rather_than_nothing()
    {
        // The host asked for 2019 and is entitled to a page saying zero, not a
        // blank screen that reads like the report is broken.
        var report = TaxReports.Build([Stay("A", new DateOnly(2026, 4, 4))], 2019);

        Assert.Equal(2019, report.Year);
        Assert.Equal(0, report.Stays);
        Assert.Equal(0m, report.Tax);
        Assert.Empty(report.Taxes);
        Assert.Equal(12, report.Months.Count);
    }

    [Fact]
    public void A_stay_that_carried_no_tax_adds_no_tax_line()
    {
        var report = TaxReports.Build([Stay("A", new DateOnly(2026, 4, 4), tax: 0m)], 2026);

        Assert.Equal(1, report.Stays);
        Assert.Equal(0m, report.Tax);
        Assert.Empty(report.Taxes);
    }

    [Fact]
    public void The_report_says_who_already_paid_the_tax()
    {
        // Without this a host reads a tax total as a bill they still owe.
        Assert.Contains("đã nộp thay", TaxReports.RemittanceNote);
        Assert.Contains("ngày trả phòng", TaxReports.RemittanceNote);
    }
}
