using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>docs/07 §7 — "Lệch một giao dịch là báo động, không được bỏ qua."</summary>
public class ReconciliationTests
{
    private static readonly DateOnly Day = new(2026, 8, 7);

    private static Reconciliation.Record R(string reference, decimal amount) => new(reference, amount);

    [Fact]
    public void Two_lists_that_agree_balance()
    {
        var report = Reconciliation.Compare(Day,
            [R("PAY1", 1_000_000m), R("PAY2", 2_000_000m)],
            [R("PAY1", 1_000_000m), R("PAY2", 2_000_000m)]);

        Assert.True(report.Balanced);
        Assert.Empty(report.Discrepancies);
        Assert.Equal(0m, report.Difference);
    }

    [Fact]
    public void The_order_the_two_sides_list_things_in_does_not_matter()
    {
        var report = Reconciliation.Compare(Day,
            [R("PAY1", 100m), R("PAY2", 200m)],
            [R("PAY2", 200m), R("PAY1", 100m)]);

        Assert.True(report.Balanced);
    }

    [Fact]
    public void A_charge_only_we_know_about_is_an_alarm()
    {
        var report = Reconciliation.Compare(Day, [R("PAY1", 500_000m)], []);

        var line = Assert.Single(report.Discrepancies);
        Assert.Equal(DiscrepancyKind.MissingAtGateway, line.Kind);
        Assert.Equal(500_000m, line.Difference);
        Assert.False(report.Balanced);
    }

    [Fact]
    public void A_charge_only_the_gateway_knows_about_is_a_worse_one()
    {
        var report = Reconciliation.Compare(Day, [], [R("PAY9", 700_000m)]);

        var line = Assert.Single(report.Discrepancies);
        Assert.Equal(DiscrepancyKind.MissingAtPlatform, line.Kind);
        Assert.Equal(-700_000m, line.Difference);
    }

    [Fact]
    public void The_same_charge_at_two_amounts_is_reported_with_both()
    {
        var report = Reconciliation.Compare(Day, [R("PAY1", 1_000_000m)], [R("PAY1", 900_000m)]);

        var line = Assert.Single(report.Discrepancies);
        Assert.Equal(DiscrepancyKind.AmountMismatch, line.Kind);
        Assert.Equal(1_000_000m, line.Ours);
        Assert.Equal(900_000m, line.Theirs);
    }

    [Fact]
    public void Two_errors_that_cancel_out_are_two_errors_not_none()
    {
        // The totals match exactly. Netting them would report a clean day.
        var report = Reconciliation.Compare(Day,
            [R("PAY1", 500_000m)],
            [R("PAY2", 500_000m)]);

        Assert.Equal(report.OursTotal, report.TheirsTotal);
        Assert.False(report.Balanced);
        Assert.Equal(2, report.Discrepancies.Count);
    }

    [Fact]
    public void Both_sides_are_counted_so_a_quiet_day_is_visibly_quiet()
    {
        var report = Reconciliation.Compare(Day, [], []);
        Assert.True(report.Balanced);
        Assert.Equal(0, report.OursCount);
        Assert.Equal(0, report.TheirsCount);
    }

    [Fact]
    public void A_clean_day_says_how_many_it_checked()
    {
        var report = Reconciliation.Compare(Day, [R("PAY1", 100m)], [R("PAY1", 100m)]);
        Assert.Contains("khớp 1 giao dịch", Reconciliation.Summary(report));
    }

    [Fact]
    public void A_day_that_does_not_balance_says_so_loudly()
    {
        var report = Reconciliation.Compare(Day, [R("PAY1", 100m)], []);
        var summary = Reconciliation.Summary(report);

        Assert.Contains("LỆCH", summary);
        Assert.Contains("Cần kiểm tra ngay", summary);
    }

    [Fact]
    public void Every_kind_of_disagreement_can_be_explained_to_an_operator()
    {
        var seen = new HashSet<string>();
        foreach (DiscrepancyKind kind in Enum.GetValues<DiscrepancyKind>())
            Assert.True(seen.Add(Reconciliation.KindLabel(kind)), $"{kind} reuses a label");
    }
}
