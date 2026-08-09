namespace StayHost.Domain.Tests;

/// <summary>
/// docs/01 AT-02 — "Báo cáo tin đăng, người dùng, tin nhắn, đánh giá". Only the
/// listing half existed before; these cover the rules the other three brought.
/// </summary>
public class ReportsTests
{
    /* ---- what may be reported ---- */

    [Theory]
    [InlineData(ReportTarget.Listing)]
    [InlineData(ReportTarget.User)]
    [InlineData(ReportTarget.Message)]
    [InlineData(ReportTarget.Review)]
    public void Every_subject_the_spec_names_offers_reasons_and_a_label(ReportTarget target)
    {
        // A subject with an empty reason list would render a report dialog with
        // nothing to click, which is the same as not shipping it.
        Assert.NotEmpty(Reports.ReasonsFor(target));
        Assert.NotEqual("Không rõ", Reports.TargetLabel(target));
    }

    [Fact]
    public void Reasons_are_written_for_the_subject_being_reported()
    {
        // Reporting a review is not the same complaint as reporting a listing;
        // sharing one list would ask somebody to accuse a review of not existing.
        Assert.Contains("Chỗ ở không tồn tại", Reports.ReasonsFor(ReportTarget.Listing));
        Assert.DoesNotContain("Chỗ ở không tồn tại", Reports.ReasonsFor(ReportTarget.Review));
        Assert.Contains("Đòi giao dịch ngoài sàn", Reports.ReasonsFor(ReportTarget.Message));
    }

    [Theory]
    [InlineData("listing", ReportTarget.Listing)]
    [InlineData("USER", ReportTarget.User)]
    [InlineData("Message", ReportTarget.Message)]
    public void Target_parses_whatever_case_the_client_sends(string sent, ReportTarget expected)
    {
        Assert.True(Reports.TryParseTarget(sent, out var parsed));
        Assert.Equal(expected, parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("booking")]
    [InlineData("7")]
    public void An_unknown_target_is_refused_rather_than_guessed(string? sent)
    {
        Assert.False(Reports.TryParseTarget(sent, out _));
    }

    /* ---- what makes a report worth opening ---- */

    [Fact]
    public void A_complete_report_passes()
    {
        Assert.Null(Reports.Validate(ReportTarget.User, 12, "Quấy rối hoặc xúc phạm", null));
    }

    [Fact]
    public void A_report_without_a_subject_is_refused()
    {
        Assert.NotNull(Reports.Validate(ReportTarget.Message, 0, "Spam hoặc quảng cáo", null));
        Assert.NotNull(Reports.Validate(ReportTarget.Message, -3, "Spam hoặc quảng cáo", null));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_report_without_a_reason_is_refused(string? reason)
    {
        Assert.NotNull(Reports.Validate(ReportTarget.Listing, 1, reason, null));
    }

    [Fact]
    public void Reason_and_detail_are_bounded_to_what_the_column_holds()
    {
        // The database truncates silently; refusing here is what keeps a report
        // saying the same thing the reporter typed.
        Assert.NotNull(Reports.Validate(
            ReportTarget.Listing, 1, new string('x', Reports.ReasonMax + 1), null));
        Assert.NotNull(Reports.Validate(
            ReportTarget.Listing, 1, "Nội dung không phù hợp", new string('x', Reports.DetailMax + 1)));

        Assert.Null(Reports.Validate(
            ReportTarget.Listing, 1, new string('x', Reports.ReasonMax), new string('x', Reports.DetailMax)));
    }

    /* ---- reporting yourself ---- */

    [Fact]
    public void Nobody_reports_themselves()
    {
        Assert.True(Reports.IsSelfReport(reporterUserId: 5, subjectOwnerUserId: 5));
        Assert.False(Reports.IsSelfReport(reporterUserId: 5, subjectOwnerUserId: 6));
    }

    [Fact]
    public void An_anonymous_reporter_is_never_the_owner()
    {
        // Two unknowns are not a match. Treating null == null as "same person"
        // would block every signed-out visitor from reporting a seeded review,
        // whose author is also null.
        Assert.False(Reports.IsSelfReport(null, null));
        Assert.False(Reports.IsSelfReport(null, 4));
        Assert.False(Reports.IsSelfReport(4, null));
    }

    /* ---- one row, one subject ---- */

    [Fact]
    public void A_report_answers_what_it_is_about_from_its_target()
    {
        Assert.Equal(9, new AbuseReport { Target = ReportTarget.Listing, ListingId = 9 }.SubjectId);
        Assert.Equal(9, new AbuseReport { Target = ReportTarget.User, ReportedUserId = 9 }.SubjectId);
        Assert.Equal(9, new AbuseReport { Target = ReportTarget.Message, MessageId = 9 }.SubjectId);
        Assert.Equal(9, new AbuseReport { Target = ReportTarget.Review, ReviewId = 9 }.SubjectId);
    }

    [Fact]
    public void A_stray_id_on_another_column_is_not_read_as_the_subject()
    {
        // Target decides, not whichever column happens to be filled — otherwise a
        // report about a person could be moderated as a report about a listing.
        var report = new AbuseReport { Target = ReportTarget.User, ReportedUserId = 3, ListingId = 88 };
        Assert.Equal(3, report.SubjectId);
    }
}
