namespace StayHost.Domain.Tests;

/// <summary>docs/01 AT-09 — support tickets: topics, validation, queue order.</summary>
public class SupportTicketsTests
{
    [Fact]
    public void A_safety_topic_is_urgent_the_rest_are_normal()
    {
        Assert.Equal(SupportPriority.Urgent, SupportTickets.PriorityFor("safety"));
        Assert.Equal(SupportPriority.Normal, SupportTickets.PriorityFor("payment"));
        Assert.Equal(SupportPriority.Normal, SupportTickets.PriorityFor("unknown"));
    }

    [Fact]
    public void A_complete_ticket_passes()
    {
        Assert.Null(SupportTickets.Validate("Không vào được tài khoản", "Tôi quên mật khẩu và không nhận được mã."));
    }

    [Theory]
    [InlineData(null, "có nội dung")]
    [InlineData("", "có nội dung")]
    [InlineData("Chỉ có tiêu đề", null)]
    [InlineData("Chỉ có tiêu đề", "")]
    public void A_ticket_missing_subject_or_message_is_refused(string? subject, string? message)
    {
        Assert.NotNull(SupportTickets.Validate(subject, message));
    }

    [Fact]
    public void Over_length_subject_or_message_is_refused()
    {
        Assert.NotNull(SupportTickets.Validate(new string('x', SupportTickets.SubjectMax + 1), "ok"));
        Assert.NotNull(SupportTickets.Validate("ok", new string('x', SupportTickets.MessageMax + 1)));
    }

    [Fact]
    public void The_queue_puts_urgent_first_then_newest()
    {
        var t0 = new DateTime(2026, 8, 10, 8, 0, 0, DateTimeKind.Utc);
        var oldNormal = new SupportTicket { Id = 1, Priority = SupportPriority.Normal, CreatedAt = t0 };
        var newNormal = new SupportTicket { Id = 2, Priority = SupportPriority.Normal, CreatedAt = t0.AddHours(2) };
        var oldUrgent = new SupportTicket { Id = 3, Priority = SupportPriority.Urgent, CreatedAt = t0.AddHours(1) };

        var order = SupportTickets.Queue([oldNormal, newNormal, oldUrgent]).Select(t => t.Id).ToList();

        Assert.Equal(3, order[0]);   // urgent first, even though a normal one is newer
        Assert.Equal(2, order[1]);   // then newest normal
        Assert.Equal(1, order[2]);
    }
}
