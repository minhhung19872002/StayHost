namespace StayHost.Domain.Tests;

/// <summary>docs/08 §9 — what leaves, what stays, and what stops either.</summary>
public class DataRequestsTests
{
    private static readonly DateTime Asked = new(2026, 8, 7, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_request_is_due_within_thirty_days()
    {
        Assert.Equal(Asked.AddDays(30), DataRequests.DueBy(Asked));
    }

    [Fact]
    public void An_unanswered_request_shows_as_overdue()
    {
        var r = new DataRequest { CreatedAt = Asked, DueBy = DataRequests.DueBy(Asked) };

        Assert.False(DataRequests.Overdue(r, Asked.AddDays(20)));
        Assert.True(DataRequests.Overdue(r, Asked.AddDays(40)));

        r.Status = DataRequestStatus.Done;
        Assert.False(DataRequests.Overdue(r, Asked.AddDays(40)));
    }

    /* ---- §9, what stops an erasure ---- */

    [Fact]
    public void An_account_with_nothing_outstanding_can_be_erased()
    {
        Assert.True(DataRequests.MayErase(new DataRequests.Blockers(false, false, false, false)));
    }

    [Fact]
    public void All_four_blockers_are_named_to_the_person_asking()
    {
        var blockers = new DataRequests.Blockers(true, true, true, true);
        var message = DataRequests.BlockedMessage(blockers);

        Assert.False(DataRequests.MayErase(blockers));
        Assert.Contains("còn đơn chưa hoàn tất", message);
        Assert.Contains("còn tranh chấp đang mở", message);
        Assert.Contains("còn nợ StayHost", message);
        Assert.Contains("đang bị điều tra", message);
    }

    [Fact]
    public void One_blocker_is_enough_to_stop_it()
    {
        Assert.False(DataRequests.MayErase(new DataRequests.Blockers(false, true, false, false)));
        Assert.Single(DataRequests.Reasons(new DataRequests.Blockers(false, true, false, false)));
    }

    /* ---- §9 and §13 scenario 9 ---- */

    [Fact]
    public void The_money_records_survive_the_person()
    {
        // docs/08 §9 — kept "vì nghĩa vụ kế toán và pháp lý".
        Assert.Contains("Đơn đặt và lịch sử trạng thái của đơn", DataRequests.Kept);
        Assert.Contains("Sổ ghi tiền", DataRequests.Kept);
        Assert.Contains("Nhật ký thao tác quản trị", DataRequests.Kept);
    }

    [Fact]
    public void The_person_does_not()
    {
        Assert.Contains("Email", DataRequests.Erased);
        Assert.Contains("Giấy tờ tuỳ thân", DataRequests.Erased);
        Assert.Contains("Ảnh đại diện", DataRequests.Erased);
    }

    [Fact]
    public void The_summary_says_both_halves_plainly()
    {
        // Somebody asking to be forgotten deserves to know what will not be.
        var summary = DataRequests.ErasureSummary();

        Assert.Contains("đã xoá", summary.ToLowerInvariant());
        Assert.Contains("giữ lại", summary.ToLowerInvariant());
        Assert.Contains("sổ ghi tiền", summary.ToLowerInvariant());
    }

    [Fact]
    public void A_review_keeps_its_words_and_loses_its_name()
    {
        // docs/08 §9 — "đánh giá thuộc về cộng đồng. Chỉ ẩn tên người viết."
        Assert.Contains("đã rời StayHost", DataRequests.AnonymousReviewerName());
    }

    [Fact]
    public void The_email_left_behind_cannot_receive_anything()
    {
        // The column is unique, so something has to go there — but nothing should
        // ever be delivered to a deleted account.
        var left = DataRequests.AnonymisedEmail(42);

        Assert.Contains("42", left);
        Assert.EndsWith(".invalid", left);
    }
}
