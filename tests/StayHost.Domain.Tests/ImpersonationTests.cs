namespace StayHost.Domain.Tests;

/// <summary>docs/08 §7 — a support session spent inside somebody else's account.</summary>
public class ImpersonationTests
{
    private static readonly DateTime Now = new(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void The_session_runs_out_after_half_an_hour()
    {
        var s = new ImpersonationSession { StartedAt = Now, ExpiresAt = Impersonation.ExpiryFor(Now) };

        Assert.Equal(Now.AddMinutes(30), s.ExpiresAt);
        Assert.True(s.IsLive(Now.AddMinutes(29)));
        Assert.False(s.IsLive(Now.AddMinutes(31)));
        Assert.True(Impersonation.Expired(s, Now.AddMinutes(31)));
    }

    [Fact]
    public void Time_left_never_goes_negative()
    {
        var s = new ImpersonationSession { StartedAt = Now, ExpiresAt = Now.AddMinutes(30) };

        Assert.Equal(TimeSpan.Zero, Impersonation.Remaining(s, Now.AddHours(2)));
    }

    /* ---- §7.6, the hard nos ---- */

    [Fact]
    public void Nothing_that_changes_who_controls_the_account_is_allowed()
    {
        // docs/08 §13 scenario 4.
        Assert.True(Impersonation.IsForbidden("change-payout-account"));
        Assert.True(Impersonation.IsForbidden("change-password"));
        Assert.True(Impersonation.IsForbidden("change-email"));
        Assert.True(Impersonation.IsForbidden("delete-account"));
    }

    [Fact]
    public void Nothing_that_moves_money_is_allowed_either()
    {
        Assert.True(Impersonation.IsForbidden("create-booking"));
        Assert.True(Impersonation.IsForbidden("cancel-booking"));
        Assert.True(Impersonation.IsForbidden("withdraw"));
        Assert.True(Impersonation.IsForbidden("manage-payment-methods"));
    }

    [Fact]
    public void All_nine_prohibitions_of_the_spec_are_present()
    {
        Assert.Equal(9, Impersonation.ForbiddenLabels.Count);
    }

    [Fact]
    public void Ordinary_support_work_still_goes_through()
    {
        Assert.False(Impersonation.IsForbidden("view-trip"));
        Assert.False(Impersonation.IsForbidden(null));
    }

    [Fact]
    public void The_server_blocks_the_routes_behind_those_prohibitions()
    {
        // The label list is what a person would say; this is what the server
        // matches on, and the two have to mean the same thing.
        Assert.True(Impersonation.BlocksPath("/api/host/payout"));
        Assert.True(Impersonation.BlocksPath("/api/payment-methods"));
        Assert.True(Impersonation.BlocksPath("/api/account/change-password"));
        Assert.True(Impersonation.BlocksPath("/api/bookings/12/cancel"));
        Assert.True(Impersonation.BlocksPath("/api/bookings"));

        Assert.False(Impersonation.BlocksPath("/api/bookings/12"));
        Assert.False(Impersonation.BlocksPath("/api/listings"));
    }

    /// <summary>
    /// The paths here are the application's real routes, copied from the
    /// controllers. Asserting against the labels instead is what let "đổi số
    /// điện thoại" sit on the forbidden list for months while
    /// PUT /api/account/profile — the endpoint that actually changes a phone
    /// number — walked straight past the guard.
    /// </summary>
    [Theory]
    [InlineData("/api/account/profile")]        // AccountController.UpdateProfile: phone
    [InlineData("/api/account/send-code")]      // attaching a new email or phone
    [InlineData("/api/account/confirm-code")]
    [InlineData("/api/account/two-factor")]
    [InlineData("/api/account/two-factor/disable")]
    [InlineData("/api/account/change-password")]
    [InlineData("/api/account/reset-password")]
    [InlineData("/api/account/data-requests")]  // §9 erasure intake
    [InlineData("/api/host/payout")]
    [InlineData("/api/payment-methods")]
    [InlineData("/api/payment-methods/4")]
    [InlineData("/api/wallet/redeem")]
    [InlineData("/api/wallet/gift-cards")]
    [InlineData("/api/bookings")]
    [InlineData("/api/bookings/9/cancel")]
    [InlineData("/api/experiences/bookings/3/cancel")]
    public void Every_route_that_carries_a_prohibition_is_matched(string path)
    {
        Assert.True(Impersonation.BlocksPath(path), $"{path} phải bị chặn trong chế độ thay mặt.");
    }

    [Theory]
    [InlineData("/api/account/profile-options")]   // reading the language list
    [InlineData("/api/bookings/9")]
    [InlineData("/api/messages/4/reply")]          // answering the guest IS the support work
    [InlineData("/api/host/listings/7")]
    [InlineData("/api/resolutions")]
    public void Ordinary_support_routes_are_left_alone(string path)
    {
        Assert.False(Impersonation.BlocksPath(path), $"{path} không nên bị chặn.");
    }

    [Fact]
    public void A_trailing_slash_does_not_walk_past_the_guard()
    {
        Assert.True(Impersonation.BlocksPath("/api/account/profile/"));
        Assert.True(Impersonation.BlocksPath("/API/Account/Profile"));
    }

    [Fact]
    public void A_refusal_names_the_thing_that_was_refused()
    {
        Assert.Contains("đổi tài khoản nhận tiền", Impersonation.ForbiddenMessage("change-payout-account"));
    }

    /* ---- §7.5 and §7.7, being seen ---- */

    [Fact]
    public void The_banner_says_whose_account_this_is_and_how_long_is_left()
    {
        var text = Impersonation.BannerText("Lan (hỗ trợ)", "Nguyễn Văn An", TimeSpan.FromMinutes(12));

        Assert.Contains("THAY MẶT", text);
        Assert.Contains("Nguyễn Văn An", text);
        Assert.Contains("Lan (hỗ trợ)", text);
        Assert.Contains("12 phút", text);
    }

    [Fact]
    public void The_audit_actor_carries_both_names()
    {
        // docs/08 §7.7 — "không được ghi như thể người dùng tự làm".
        var actor = Impersonation.ActorTag("Lan", "An");

        Assert.Contains("Lan", actor);
        Assert.Contains("thay mặt", actor);
        Assert.Contains("An", actor);
    }

    [Fact]
    public void The_account_holder_is_told_who_came_in_and_why()
    {
        var notice = Impersonation.TargetNotice("Lan", Now, "Khách báo không thấy đơn của mình");

        Assert.Contains("Lan", notice);
        Assert.Contains("Khách báo không thấy", notice);
        Assert.Contains("liên hệ StayHost", notice);
    }
}
