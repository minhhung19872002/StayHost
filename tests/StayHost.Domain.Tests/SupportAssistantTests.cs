namespace StayHost.Domain.Tests;

/// <summary>docs/01 AT-08 — the contextual support assistant.</summary>
public class SupportAssistantTests
{
    private static SupportAssistant.Context Ctx(
        bool loggedIn = true, bool arrival = false, bool balance = false, bool pending = false,
        bool unreviewed = false, bool dispute = false, bool host = false, bool hostReqs = false) =>
        new(loggedIn, arrival, balance, pending, unreviewed, dispute, host, hostReqs);

    [Fact]
    public void A_signed_out_visitor_is_pointed_to_login_and_help()
    {
        var s = SupportAssistant.Suggest(Ctx(loggedIn: false));
        Assert.Contains(s, x => x.ActionLink.StartsWith("/?login"));
        Assert.Contains(s, x => x.ActionLink == "/help");
    }

    [Fact]
    public void An_open_dispute_comes_before_softer_nudges()
    {
        var s = SupportAssistant.Suggest(Ctx(dispute: true, unreviewed: true));
        Assert.Equal("/resolutions", s[0].ActionLink);
    }

    [Fact]
    public void Balance_due_outranks_an_upcoming_arrival()
    {
        var s = SupportAssistant.Suggest(Ctx(balance: true, arrival: true));
        var balanceIdx = s.ToList().FindIndex(x => x.Text.Contains("số dư"));
        var arrivalIdx = s.ToList().FindIndex(x => x.Text.Contains("hướng dẫn nhận phòng"));
        Assert.True(balanceIdx >= 0 && arrivalIdx >= 0 && balanceIdx < arrivalIdx);
    }

    [Fact]
    public void A_host_with_requests_sees_the_hosting_action()
    {
        var s = SupportAssistant.Suggest(Ctx(host: true, hostReqs: true));
        Assert.Contains(s, x => x.ActionLink == "/hosting");
    }

    [Fact]
    public void There_is_always_a_help_and_a_human_option_when_signed_in()
    {
        var s = SupportAssistant.Suggest(Ctx());   // nothing specific true
        Assert.Contains(s, x => x.ActionLink == "/help");
        Assert.Contains(s, x => x.ActionLink.Contains("support=1"));
    }
}
