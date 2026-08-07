namespace StayHost.Domain.Tests;

/// <summary>docs/08 §1 and §2 — the permission matrix, and the principles on it.</summary>
public class AdminActionsTests
{
    /* ---- §2, the matrix ---- */

    [Fact]
    public void Support_can_answer_tickets_but_cannot_lock_anyone_out()
    {
        // docs/08 §13 scenario 1.
        Assert.True(AdminActions.Allows(AdminScope.Support, AdminAction.FindUser));
        Assert.True(AdminActions.Allows(AdminScope.Support, AdminAction.Warn));
        Assert.False(AdminActions.Allows(AdminScope.Support, AdminAction.Suspend));
        Assert.False(AdminActions.Allows(AdminScope.Support, AdminAction.Ban));
    }

    [Fact]
    public void Moderation_may_suspend_but_never_ban()
    {
        Assert.True(AdminActions.Allows(AdminScope.Moderation, AdminAction.Suspend));
        Assert.True(AdminActions.Allows(AdminScope.Moderation, AdminAction.Restrict));
        Assert.False(AdminActions.Allows(AdminScope.Moderation, AdminAction.Ban));
    }

    [Fact]
    public void Finance_touches_money_and_nothing_else()
    {
        Assert.True(AdminActions.Allows(AdminScope.Finance, AdminAction.ManualRefund));
        Assert.True(AdminActions.Allows(AdminScope.Finance, AdminAction.ViewPayoutAccount));
        Assert.False(AdminActions.Allows(AdminScope.Finance, AdminAction.Suspend));
        Assert.False(AdminActions.Allows(AdminScope.Finance, AdminAction.ViewIdentityDocuments));
    }

    [Fact]
    public void Only_the_top_role_can_ban_merge_erase_or_make_admins()
    {
        foreach (var action in new[]
                 {
                     AdminAction.Ban, AdminAction.MergeAccounts,
                     AdminAction.EraseData, AdminAction.ManageAdmins, AdminAction.ViewOtherAdminLog
                 })
        {
            Assert.False(AdminActions.Allows(AdminScope.Support, action));
            Assert.False(AdminActions.Allows(AdminScope.Moderation, action));
            Assert.False(AdminActions.Allows(AdminScope.Finance, action));
            Assert.True(AdminActions.Allows(AdminScope.Super, action));
        }
    }

    [Fact]
    public void Holding_two_roles_gives_the_union_of_both()
    {
        // docs/08 §2 — "Quyền là hợp của các vai."
        var both = AdminScope.Support | AdminScope.Finance;

        Assert.True(AdminActions.Allows(both, AdminAction.Warn));
        Assert.True(AdminActions.Allows(both, AdminAction.ManualRefund));
        Assert.False(AdminActions.Allows(both, AdminAction.Suspend));
    }

    /* ---- §1.4, a decision without a reason is not saved ---- */

    [Fact]
    public void Every_decision_needs_a_reason()
    {
        foreach (var rule in AdminActions.All.Where(r => r.Decides))
            Assert.True(AdminActions.RequiresReason(rule.Action), rule.Label);
    }

    [Fact]
    public void The_sensitive_reads_need_a_reason_even_though_they_change_nothing()
    {
        // The ✓* rows of §2: looking at somebody's identity card is an act.
        Assert.True(AdminActions.RequiresReason(AdminAction.ViewIdentityDocuments));
        Assert.True(AdminActions.RequiresReason(AdminAction.ViewBookingThread));
        Assert.True(AdminActions.RequiresReason(AdminAction.Impersonate));
    }

    [Fact]
    public void An_ordinary_lookup_does_not_stop_to_ask_why()
    {
        // Support cannot work if every search needs a paragraph.
        Assert.False(AdminActions.RequiresReason(AdminAction.FindUser));
        Assert.False(AdminActions.RequiresReason(AdminAction.ViewBookingHistory));
    }

    [Fact]
    public void A_reason_too_short_to_mean_anything_is_not_a_reason()
    {
        Assert.False(AdminActions.ReasonIsUsable(null));
        Assert.False(AdminActions.ReasonIsUsable("   "));
        Assert.False(AdminActions.ReasonIsUsable("spam"));
        Assert.True(AdminActions.ReasonIsUsable("Khách báo cáo lừa đảo, có ảnh chụp màn hình"));
    }

    /* ---- §3, admin accounts ---- */

    [Fact]
    public void An_admin_without_two_factor_cannot_hold_a_session()
    {
        // docs/08 §3 — "không có ngoại lệ".
        Assert.False(AdminActions.MayHoldAdminSession(false));
        Assert.True(AdminActions.MayHoldAdminSession(true));
    }

    [Fact]
    public void An_idle_admin_session_ends_by_itself()
    {
        var seen = new DateTime(2026, 8, 7, 9, 0, 0, DateTimeKind.Utc);

        Assert.False(AdminActions.SessionExpired(seen, seen.AddMinutes(29)));
        Assert.True(AdminActions.SessionExpired(seen, seen.AddMinutes(31)));
    }

    [Fact]
    public void A_permission_nobody_has_used_for_a_quarter_is_worth_taking_back()
    {
        var now = new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc);

        Assert.True(AdminActions.ScopeLooksUnused(null, now));
        Assert.True(AdminActions.ScopeLooksUnused(now.AddDays(-100), now));
        Assert.False(AdminActions.ScopeLooksUnused(now.AddDays(-10), now));
    }

    [Fact]
    public void Access_is_reviewed_every_three_months()
    {
        var last = new DateOnly(2026, 5, 1);

        Assert.False(AdminActions.AccessReviewDue(last, new DateOnly(2026, 7, 31)));
        Assert.True(AdminActions.AccessReviewDue(last, new DateOnly(2026, 8, 1)));
        Assert.True(AdminActions.AccessReviewDue(null, new DateOnly(2026, 8, 1)));
    }

    [Fact]
    public void A_refusal_says_which_role_would_have_been_needed()
    {
        // docs/08 §13 scenario 1 — "bị từ chối, nêu rõ thiếu quyền".
        Assert.Contains("Kiểm duyệt", AdminActions.RequiredRoles(AdminAction.Suspend));
        Assert.Equal("Quản trị tối cao", AdminActions.RequiredRoles(AdminAction.Ban));
    }
}
