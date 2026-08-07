namespace StayHost.Domain.Tests;

/// <summary>docs/07 §5 — the trip to the bank's OTP page and back.</summary>
public class CardAuthTests
{
    private static readonly DateTime Now = new(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc);

    /* ---- §5.2, the room must not disappear mid-payment ---- */

    [Fact]
    public void A_hold_with_plenty_left_is_not_touched()
    {
        var expires = Now.AddMinutes(12);

        Assert.False(CardAuth.NeedsExtension(expires, Now));
        Assert.Equal(expires, CardAuth.ExtendedTo(expires, Now));
    }

    [Fact]
    public void A_hold_about_to_run_out_gets_ten_more_minutes()
    {
        // docs/07 §5.2 — the guest is on their bank's page; the timer expiring
        // is the platform's problem, not theirs.
        var expires = Now.AddMinutes(3);

        Assert.True(CardAuth.NeedsExtension(expires, Now));
        Assert.Equal(expires.AddMinutes(10), CardAuth.ExtendedTo(expires, Now));
    }

    [Fact]
    public void A_hold_that_already_lapsed_is_extended_from_now_not_from_the_past()
    {
        // Extending from the old expiry would hand back a deadline in the past.
        var expires = Now.AddMinutes(-4);

        Assert.Equal(Now.AddMinutes(10), CardAuth.ExtendedTo(expires, Now));
    }

    /* ---- §5.4, three goes at the code ---- */

    [Fact]
    public void A_wrong_code_can_be_tried_three_times()
    {
        Assert.True(CardAuth.CanTryCodeAgain(0));
        Assert.True(CardAuth.CanTryCodeAgain(2));
        Assert.False(CardAuth.CanTryCodeAgain(3));
    }

    [Fact]
    public void The_guest_is_told_how_many_goes_are_left()
    {
        Assert.Contains("còn 2 lần", CardAuth.OutcomeMessage(AuthOutcome.WrongCode, 1));
    }

    [Fact]
    public void Running_out_of_codes_does_not_lose_the_booking()
    {
        // The bank refused a code, not the guest. The room is still theirs until
        // the hold runs out.
        var message = CardAuth.OutcomeMessage(AuthOutcome.WrongCode, CardAuth.MaxCodeAttempts);

        Assert.Contains("vẫn đang được giữ", message);
    }

    [Fact]
    public void Closing_the_tab_says_the_booking_is_still_waiting()
    {
        Assert.Contains("tiếp tục", CardAuth.OutcomeMessage(AuthOutcome.Abandoned, 0));
    }

    [Fact]
    public void A_bank_refusal_points_somewhere_else_to_go()
    {
        Assert.True(CardAuth.SuggestAnotherMethod(AuthOutcome.BankRefused));
        Assert.False(CardAuth.SuggestAnotherMethod(AuthOutcome.Succeeded));
    }

    /* ---- §5, the browser is not a source of truth ---- */

    [Fact]
    public void Money_taken_while_the_guest_lost_their_connection_still_counts()
    {
        // The case docs/07 §5 names outright: charged, then the guest dropped.
        var settled = CardAuth.Reconcile(AuthOutcome.Abandoned, AuthOutcome.Succeeded);

        Assert.Equal(AuthOutcome.Succeeded, settled);
        Assert.True(CardAuth.Disagreed(AuthOutcome.Abandoned, AuthOutcome.Succeeded));
    }

    [Fact]
    public void A_browser_claiming_success_the_gateway_never_saw_is_not_believed()
    {
        var settled = CardAuth.Reconcile(AuthOutcome.Succeeded, AuthOutcome.BankRefused);

        Assert.Equal(AuthOutcome.BankRefused, settled);
    }

    [Fact]
    public void With_no_word_from_the_gateway_yet_the_browser_stands_in()
    {
        // Provisional, and NeedsGatewayCheck keeps it on the list to settle.
        Assert.Equal(AuthOutcome.Succeeded, CardAuth.Reconcile(AuthOutcome.Succeeded, null));
        Assert.False(CardAuth.Disagreed(AuthOutcome.Succeeded, null));
    }

    [Fact]
    public void An_authentication_never_checked_with_the_gateway_is_not_finished()
    {
        var auth = new CardAuthentication { Outcome = AuthOutcome.Succeeded };
        Assert.True(CardAuth.NeedsGatewayCheck(auth));

        auth.ConfirmedWithGatewayAt = Now;
        Assert.False(CardAuth.NeedsGatewayCheck(auth));
    }
}
