using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>docs/07 §7 and §8 — taking money once, and saying why when it fails.</summary>
public class PaymentsTests
{
    /* ------------------------------------------------ §7, the anti-double key */

    [Fact]
    public void The_same_payment_twice_is_one_key()
    {
        Assert.Equal(Payments.KeyFor(7, 1_200_000m, "card"), Payments.KeyFor(7, 1_200_000m, "card"));
    }

    [Fact]
    public void A_different_booking_amount_or_method_is_a_different_attempt()
    {
        var baseline = Payments.KeyFor(7, 1_200_000m, "card");
        Assert.NotEqual(baseline, Payments.KeyFor(8, 1_200_000m, "card"));
        Assert.NotEqual(baseline, Payments.KeyFor(7, 600_000m, "card"));
        Assert.NotEqual(baseline, Payments.KeyFor(7, 1_200_000m, "momo"));
    }

    [Fact]
    public void Case_of_the_method_does_not_make_a_second_attempt()
    {
        Assert.Equal(Payments.KeyFor(7, 100m, "CARD"), Payments.KeyFor(7, 100m, "card"));
    }

    [Fact]
    public void One_guests_key_cannot_reach_another_guests_booking()
    {
        // A client-supplied key is namespaced by the booking, so replaying it
        // against a different booking is simply a different key.
        var mine = Payments.NamespaceKey(7, "abc-123", 100m, "card");
        var theirs = Payments.NamespaceKey(8, "abc-123", 100m, "card");

        Assert.NotEqual(mine, theirs);
        Assert.Contains("booking:7:", mine);
    }

    [Fact]
    public void A_missing_or_absurd_client_key_falls_back_to_one_the_server_derives()
    {
        var derived = Payments.KeyFor(7, 100m, "card");

        Assert.Equal(derived, Payments.NamespaceKey(7, null, 100m, "card"));
        Assert.Equal(derived, Payments.NamespaceKey(7, "   ", 100m, "card"));
        Assert.Equal(derived, Payments.NamespaceKey(7, new string('x', 200), 100m, "card"));
        Assert.Equal(derived, Payments.NamespaceKey(7, "!!!", 100m, "card"));
    }

    [Fact]
    public void A_client_key_keeps_only_characters_that_cannot_be_used_for_anything_else()
    {
        var key = Payments.NamespaceKey(7, "ab c/d'e-f_1", 100m, "card");
        Assert.Equal("booking:7:abcde-f_1", key);
    }

    /* ------------------------------------------------------- §8, the limit */

    [Fact]
    public void Five_failures_in_an_hour_closes_the_door()
    {
        Assert.False(Payments.LockedOut(4));
        Assert.True(Payments.LockedOut(Payments.MaxFailuresPerHour));
        Assert.True(Payments.LockedOut(99));
    }

    [Fact]
    public void The_lockout_tells_the_guest_what_to_do_next()
    {
        Assert.Contains("liên hệ hỗ trợ", Payments.LockedOutMessage());
    }

    /* -------------------------------------------------------- §8, the table */

    [Fact]
    public void Every_reason_has_wording_of_its_own()
    {
        var seen = new HashSet<string>();
        foreach (DeclineReason reason in Enum.GetValues<DeclineReason>())
        {
            var message = Payments.Message(reason);
            Assert.NotEmpty(message);
            Assert.True(seen.Add(message), $"{reason} reuses another message");
        }
    }

    [Fact]
    public void No_message_leaks_a_bank_code_at_the_guest()
    {
        // docs/07 §8 — "Không bao giờ hiển thị mã lỗi kỹ thuật của ngân hàng."
        foreach (DeclineReason reason in Enum.GetValues<DeclineReason>())
        {
            var message = Payments.Message(reason);
            Assert.DoesNotContain("code", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(reason.ToString(), message, StringComparison.OrdinalIgnoreCase);
            Assert.False(message.Any(char.IsDigit), message);
        }
    }

    [Fact]
    public void A_card_that_will_not_start_working_is_not_offered_a_retry()
    {
        Assert.False(Payments.Retryable(DeclineReason.ExpiredCard));
        Assert.False(Payments.Retryable(DeclineReason.SuspectedFraud));

        Assert.True(Payments.Retryable(DeclineReason.InsufficientFunds));
        Assert.True(Payments.Retryable(DeclineReason.GatewayError));
        Assert.True(Payments.Retryable(DeclineReason.Unknown));
    }

    [Fact]
    public void A_refusal_the_same_card_cannot_fix_asks_for_a_different_one()
    {
        Assert.True(Payments.NeedsDifferentMethod(DeclineReason.ExpiredCard));
        Assert.True(Payments.NeedsDifferentMethod(DeclineReason.InsufficientFunds));
        Assert.False(Payments.NeedsDifferentMethod(DeclineReason.GatewayError));
    }

    [Fact]
    public void An_unexplained_refusal_is_treated_as_worth_another_go()
    {
        // Guessing "no" strands a guest whose payment would have worked.
        Assert.True(Payments.Retryable(DeclineReason.Unknown));
    }

    /// <summary>
    /// docs/02 F1 — the state names on the guest's own payment history. Sent
    /// through t() on the client, so each of these strings needs a dictionary
    /// pair; a new state added here without one renders as Vietnamese in seven
    /// languages and no test goes red over it.
    /// </summary>
    [Fact]
    public void Every_payment_state_has_a_name()
    {
        foreach (var status in Enum.GetValues<PaymentStatus>())
            Assert.False(string.IsNullOrWhiteSpace(Payments.StatusLabel(status)), status.ToString());

        Assert.Equal("Đã thanh toán", Payments.StatusLabel(PaymentStatus.Captured));
        Assert.Equal("Đã hoàn tiền", Payments.StatusLabel(PaymentStatus.Refunded));
    }
}
