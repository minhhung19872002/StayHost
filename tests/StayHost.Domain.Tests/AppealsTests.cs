namespace StayHost.Domain.Tests;

/// <summary>docs/08 §8 — one appeal, a different reader, a real answer.</summary>
public class AppealsTests
{
    private static readonly DateTime Decided = new(2026, 8, 7, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void The_window_is_thirty_days_from_the_decision()
    {
        Assert.True(Appeals.WithinWindow(Decided, Decided.AddDays(29)));
        Assert.False(Appeals.WithinWindow(Decided, Decided.AddDays(31)));
    }

    [Fact]
    public void Nobody_appeals_the_same_decision_twice()
    {
        Assert.True(Appeals.MayFile(alreadyFiled: false, Decided, Decided.AddDays(1)));
        Assert.False(Appeals.MayFile(alreadyFiled: true, Decided, Decided.AddDays(1)));
        Assert.Contains("một lần rồi", Appeals.CannotFileMessage(true, Decided));
    }

    [Fact]
    public void A_late_appeal_is_told_when_the_door_closed()
    {
        Assert.Contains("06/09/2026", Appeals.CannotFileMessage(false, Decided));
    }

    /* ---- §8, seven working days ---- */

    [Fact]
    public void The_answer_is_due_in_seven_working_days_not_seven_days()
    {
        // Friday 7 August 2026 + 7 working days = Tuesday 18 August.
        Assert.Equal(DayOfWeek.Friday, Decided.DayOfWeek);

        var due = Appeals.DueBy(Decided);

        Assert.Equal(new DateTime(2026, 8, 18, 9, 0, 0, DateTimeKind.Utc), due);
    }

    [Fact]
    public void An_unanswered_appeal_shows_as_overdue()
    {
        var a = new Appeal { CreatedAt = Decided, DueBy = Appeals.DueBy(Decided) };

        Assert.False(Appeals.Overdue(a, Decided.AddDays(3)));
        Assert.True(Appeals.Overdue(a, Decided.AddDays(20)));

        a.Status = AppealStatus.Upheld;
        Assert.False(Appeals.Overdue(a, Decided.AddDays(20)));
    }

    /* ---- §8 and §13 scenario 7 ---- */

    [Fact]
    public void The_person_who_made_the_call_cannot_review_their_own_call()
    {
        Assert.False(Appeals.MayReview(reviewerUserId: 7, originalDeciderUserId: 7));
        Assert.True(Appeals.MayReview(reviewerUserId: 8, originalDeciderUserId: 7));
        Assert.Contains("chuyển hồ sơ cho người khác", Appeals.SameReviewerMessage());
    }

    /* ---- the outcome ---- */

    [Fact]
    public void An_overturned_decision_comes_off_the_record()
    {
        // Which matters, because the ladder in §5 counts what came before.
        Assert.True(Appeals.ClearsTheRecord(AppealStatus.Overturned));
        Assert.False(Appeals.ClearsTheRecord(AppealStatus.Reduced));
        Assert.False(Appeals.ClearsTheRecord(AppealStatus.Upheld));
    }

    [Fact]
    public void A_curt_answer_is_not_an_answer()
    {
        // docs/08 §8 — "không được trả lời cụt lủn".
        Assert.False(Appeals.OutcomeIsUsable("Giữ nguyên."));
        Assert.False(Appeals.OutcomeIsUsable(null));
        Assert.True(Appeals.OutcomeIsUsable(
            "Chúng tôi giữ nguyên quyết định vì ảnh tin đăng vẫn không phải của chỗ nghỉ như đã nêu."));
    }

    [Fact]
    public void Every_outcome_has_a_name_a_person_would_recognise()
    {
        foreach (var status in Enum.GetValues<AppealStatus>())
            Assert.NotEqual("", Appeals.StatusLabel(status));
    }
}
