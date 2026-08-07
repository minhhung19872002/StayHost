namespace StayHost.Domain.Tests;

/// <summary>docs/08 §5 — how far you may go with somebody's account, and in what order.</summary>
public class SanctionsTests
{
    /* ---- §5, one step at a time ---- */

    [Fact]
    public void A_clean_record_starts_at_a_warning()
    {
        Assert.Equal(SanctionLevel.Warning, Sanctions.NextStep(null));
        Assert.True(Sanctions.IsInOrder(null, SanctionLevel.Warning, severe: false));
    }

    [Fact]
    public void A_first_offence_cannot_go_straight_to_a_suspension()
    {
        // Skipping the ladder is how somebody loses their livelihood over
        // something nobody ever told them about.
        Assert.False(Sanctions.IsInOrder(null, SanctionLevel.Suspension, severe: false));
        Assert.Contains("Chưa được nhảy thẳng", Sanctions.OutOfOrderMessage(null, SanctionLevel.Suspension));
    }

    [Fact]
    public void Each_step_unlocks_the_next_one()
    {
        Assert.True(Sanctions.IsInOrder(SanctionLevel.Warning, SanctionLevel.Restriction, false));
        Assert.True(Sanctions.IsInOrder(SanctionLevel.Restriction, SanctionLevel.Suspension, false));
        Assert.True(Sanctions.IsInOrder(SanctionLevel.Suspension, SanctionLevel.Ban, false));
    }

    [Fact]
    public void Going_lighter_than_last_time_is_always_allowed()
    {
        // A warning after a suspension is not a skipped step.
        Assert.True(Sanctions.IsInOrder(SanctionLevel.Suspension, SanctionLevel.Warning, false));
    }

    [Fact]
    public void A_severe_case_may_jump_to_a_suspension_but_not_to_a_ban()
    {
        // docs/08 §5.6 lists suspension as the ceiling for jumping.
        Assert.True(Sanctions.IsInOrder(null, SanctionLevel.Suspension, severe: true));
        Assert.False(Sanctions.IsInOrder(null, SanctionLevel.Ban, severe: true));
    }

    [Fact]
    public void The_severe_grounds_are_written_out_rather_than_left_to_judgement()
    {
        // "Nghiêm trọng" is exactly the word that stretches when somebody is in
        // a hurry, so the list is closed.
        Assert.Equal(6, Sanctions.SevereGrounds.Count);
        Assert.True(Sanctions.IsSevereGround("Giả mạo giấy tờ"));
        Assert.False(Sanctions.IsSevereGround("Khách phàn nàn nhiều"));
    }

    [Fact]
    public void A_severe_suspension_goes_back_to_the_top_within_a_day()
    {
        var at = new DateTime(2026, 8, 7, 9, 0, 0, DateTimeKind.Utc);
        Assert.Equal(at.AddHours(24), Sanctions.SevereReviewDueBy(at));
    }

    /* ---- §5.2, a restriction says what it leaves alone ---- */

    [Fact]
    public void Every_restriction_names_what_it_does_not_touch()
    {
        // A person told only what they have lost assumes they have lost
        // everything. Only "no reviews" has nothing left to reassure about.
        foreach (var kind in Enum.GetValues<RestrictionKind>())
        {
            Assert.NotEqual("", Sanctions.RestrictionLabel(kind));
            if (kind != RestrictionKind.NoReviews)
                Assert.NotEqual("", Sanctions.RestrictionKeeps(kind));
        }
    }

    [Fact]
    public void The_notice_carries_the_reason_the_policy_and_the_way_out()
    {
        var s = new Sanction
        {
            Level = SanctionLevel.Restriction,
            Restriction = RestrictionKind.NoNewListings,
            Policy = "docs/03 §9",
            Reason = "Ảnh tin đăng không phải của chỗ nghỉ",
            LiftedWhen = "Thay ảnh thật và gửi lại để duyệt"
        };

        var notice = Sanctions.Notice(s);

        Assert.Contains("Ảnh tin đăng không phải", notice);
        Assert.Contains("docs/03 §9", notice);
        Assert.Contains("Tin cũ vẫn hiển thị", notice);
        Assert.Contains("Thay ảnh thật", notice);
    }

    /* ---- being lifted ---- */

    [Fact]
    public void A_sanction_with_a_date_stops_on_its_own()
    {
        var now = new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc);
        var s = new Sanction { Level = SanctionLevel.Suspension, ExpiresAt = now.AddDays(-1) };

        Assert.False(s.IsActive(now));
    }

    [Fact]
    public void An_open_ended_sanction_runs_until_somebody_lifts_it()
    {
        var now = new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc);
        var s = new Sanction { Level = SanctionLevel.Suspension };

        Assert.True(s.IsActive(now));

        s.LiftedAt = now;
        Assert.False(s.IsActive(now));
    }

    [Fact]
    public void A_sanction_overturned_on_appeal_stops_counting_against_the_person()
    {
        // docs/08 §8 — "Gỡ bỏ thì xoá luôn khỏi hồ sơ vi phạm." The row survives
        // for the audit trail; it just stops being held against them.
        var now = new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc);
        var s = new Sanction { Level = SanctionLevel.Suspension, OverturnedOnAppeal = true };

        Assert.False(s.IsActive(now));
    }
}
