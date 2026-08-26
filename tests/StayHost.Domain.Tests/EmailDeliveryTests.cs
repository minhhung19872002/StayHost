namespace StayHost.Domain.Tests;

/// <summary>When a queued email is tried again, and when it is given up on.</summary>
public class EmailDeliveryTests
{
    private static readonly DateTime Failed = new(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void The_first_retry_comes_quickly()
    {
        // Somebody is sitting at a sign-in screen waiting for six digits.
        Assert.Equal(Failed.AddMinutes(1), EmailDelivery.NextAttemptAt(Failed, 1));
    }

    [Fact]
    public void The_gaps_widen_as_hope_fades()
    {
        Assert.Equal(Failed.AddMinutes(5), EmailDelivery.NextAttemptAt(Failed, 2));
        Assert.Equal(Failed.AddMinutes(15), EmailDelivery.NextAttemptAt(Failed, 3));
        Assert.Equal(Failed.AddMinutes(60), EmailDelivery.NextAttemptAt(Failed, 4));
        Assert.Equal(Failed.AddMinutes(240), EmailDelivery.NextAttemptAt(Failed, 5));
    }

    [Fact]
    public void After_the_last_retry_nothing_more_is_scheduled()
    {
        Assert.Null(EmailDelivery.NextAttemptAt(Failed, EmailDelivery.MaxAttempts));
        Assert.True(EmailDelivery.OutOfAttempts(EmailDelivery.MaxAttempts));
        Assert.False(EmailDelivery.OutOfAttempts(EmailDelivery.MaxAttempts - 1));
    }

    /* ---- the two kinds of no ---- */

    [Fact]
    public void A_mailbox_that_does_not_exist_is_not_tried_again()
    {
        // Retrying a 5xx is how a sender ends up on a blocklist, which would take
        // the sign-in codes down with it.
        Assert.True(EmailDelivery.IsPermanent(550));
        Assert.False(EmailDelivery.ShouldRetry(550, attemptsSoFar: 1));
    }

    [Fact]
    public void A_connection_that_dropped_is_worth_another_go()
    {
        Assert.False(EmailDelivery.IsPermanent(451));
        Assert.True(EmailDelivery.ShouldRetry(451, attemptsSoFar: 1));
    }

    [Fact]
    public void A_failure_with_no_code_at_all_is_treated_as_temporary()
    {
        // A socket timeout has no SMTP status. Guessing "permanent" would throw
        // away a message the server never even saw.
        Assert.False(EmailDelivery.IsPermanent(null));
        Assert.True(EmailDelivery.ShouldRetry(null, attemptsSoFar: 1));
    }

    [Fact]
    public void Even_a_temporary_failure_stops_once_the_attempts_run_out()
    {
        Assert.False(EmailDelivery.ShouldRetry(451, EmailDelivery.MaxAttempts));
    }

    /* ---- codes do not belong in subject lines ---- */

    [Fact]
    public void The_code_subject_carries_no_code()
    {
        // It shows on a lock screen, in a mail list, and in every log on the way.
        Assert.False(EmailDelivery.SubjectLeaksCode(EmailDelivery.CodeSubject()));
    }

    [Fact]
    public void A_subject_with_the_code_in_it_is_caught()
    {
        Assert.True(EmailDelivery.SubjectLeaksCode("Mã xác thực Staylio: 123456"));
    }

    [Fact]
    public void An_ordinary_subject_with_a_number_in_it_is_left_alone()
    {
        // "2 đêm" is not a leak; six digits in a row is.
        Assert.False(EmailDelivery.SubjectLeaksCode("Đặt chỗ đã được xác nhận · 2 đêm"));
    }
}
