using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>docs/07 §11 — the guest has gone to their bank.</summary>
public class ChargebacksTests
{
    private static readonly DateTime Received = new(2026, 8, 7, 9, 0, 0, DateTimeKind.Utc);

    private static Chargeback Case(ChargebackStatus status = ChargebackStatus.Received, bool hostAtFault = false) =>
        new() { ReceivedAt = Received, Status = status, HostAtFault = hostAtFault, Amount = 3_000_000m };

    [Fact]
    public void There_are_seven_days_to_answer_the_bank()
    {
        Assert.Equal(new DateTime(2026, 8, 14, 9, 0, 0, DateTimeKind.Utc), Chargebacks.EvidenceDueBy(Received));

        Assert.False(Chargebacks.EvidenceOverdue(Case(), Received.AddDays(6)));
        Assert.True(Chargebacks.EvidenceOverdue(Case(), Received.AddDays(8)));
    }

    [Fact]
    public void A_case_already_answered_cannot_go_overdue()
    {
        Assert.False(Chargebacks.EvidenceOverdue(Case(ChargebackStatus.Contested), Received.AddDays(30)));
        Assert.False(Chargebacks.EvidenceOverdue(Case(ChargebackStatus.Won), Received.AddDays(30)));
    }

    [Fact]
    public void The_evidence_list_is_the_one_the_spec_names()
    {
        Assert.Equal(5, Chargebacks.EvidenceChecklist.Count);
        Assert.Contains(Chargebacks.EvidenceChecklist, e => e.Contains("Tin nhắn"));
        Assert.Contains(Chargebacks.EvidenceChecklist, e => e.Contains("Chính sách huỷ"));
    }

    [Fact]
    public void A_live_case_keeps_the_hosts_money_where_it_is()
    {
        Assert.True(Chargebacks.HoldsPayout(ChargebackStatus.Received));
        Assert.True(Chargebacks.HoldsPayout(ChargebackStatus.Contested));
    }

    [Fact]
    public void A_decided_case_stops_holding_it_whichever_way_it_went()
    {
        Assert.False(Chargebacks.HoldsPayout(ChargebackStatus.Won));
        Assert.False(Chargebacks.HoldsPayout(ChargebackStatus.Lost));
        Assert.False(Chargebacks.HoldsPayout(ChargebackStatus.Expired));
    }

    /* ------------------------------------------------------ who pays for it */

    [Fact]
    public void The_platform_wears_a_lost_case_by_default()
    {
        // "Chủ nhà không bị mất tiền vì khiếu nại của khách..."
        var lost = Case(ChargebackStatus.Lost);
        Assert.True(Chargebacks.PlatformBearsLoss(lost));
        Assert.False(Chargebacks.HostBearsLoss(lost));
    }

    [Fact]
    public void The_host_pays_only_when_arbitration_found_them_at_fault()
    {
        // "...trừ khi phân xử cho thấy lỗi thuộc về chủ nhà."
        var theirFault = Case(ChargebackStatus.Lost, hostAtFault: true);
        Assert.True(Chargebacks.HostBearsLoss(theirFault));
        Assert.False(Chargebacks.PlatformBearsLoss(theirFault));
    }

    [Fact]
    public void Letting_the_clock_run_out_costs_the_same_as_losing()
    {
        var expired = Case(ChargebackStatus.Expired);
        Assert.True(Chargebacks.PlatformBearsLoss(expired));
    }

    [Fact]
    public void A_case_still_running_costs_nobody_anything_yet()
    {
        foreach (var status in new[] { ChargebackStatus.Received, ChargebackStatus.Contested, ChargebackStatus.Won })
        {
            Assert.False(Chargebacks.HostBearsLoss(Case(status, hostAtFault: true)));
            Assert.False(Chargebacks.PlatformBearsLoss(Case(status)));
        }
    }

    [Fact]
    public void A_guest_who_keeps_doing_this_gets_watched()
    {
        Assert.False(Chargebacks.GuestNeedsWatching(1));
        Assert.True(Chargebacks.GuestNeedsWatching(Chargebacks.SuspiciousCount));
    }

    [Fact]
    public void Every_status_has_something_to_show_an_operator()
    {
        var seen = new HashSet<string>();
        foreach (ChargebackStatus status in Enum.GetValues<ChargebackStatus>())
            Assert.True(seen.Add(Chargebacks.StatusLabel(status)), $"{status} reuses a label");
    }
}
