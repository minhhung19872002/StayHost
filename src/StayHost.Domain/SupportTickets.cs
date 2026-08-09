namespace StayHost.Domain;

public enum SupportPriority
{
    Normal = 0,
    /// <summary>docs/04 §5 — a safety issue jumps the queue, ahead of everything else.</summary>
    Urgent = 1
}

public enum SupportTicketStatus
{
    Open = 0,
    Resolved = 1
}

/// <summary>
/// docs/01 AT-09 — a request handed to a human support agent. It is the escalation
/// path the help centre and the auto-assistant fall back to: when self-service
/// does not settle something, this reaches a person.
/// </summary>
public class SupportTicket
{
    public int Id { get; set; }

    public int? UserId { get; set; }
    public User? User { get; set; }
    public string SessionId { get; set; } = "";

    /// <summary>docs/01 CĐ-12 — the booking this is about, when there is one.</summary>
    public int? BookingId { get; set; }
    public Booking? Booking { get; set; }

    public string Subject { get; set; } = "";
    public string Message { get; set; } = "";
    public SupportPriority Priority { get; set; } = SupportPriority.Normal;
    public SupportTicketStatus Status { get; set; } = SupportTicketStatus.Open;

    public string? AdminReply { get; set; }
    public int? HandledByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}

/// <summary>docs/01 AT-09 — the pure rules around a support ticket.</summary>
public static class SupportTickets
{
    public const int SubjectMax = 150;
    public const int MessageMax = 4000;

    /// <summary>The topics a guest can raise, and whether each is treated as urgent.</summary>
    public static IReadOnlyList<(string Key, string Label, SupportPriority Priority)> Topics =>
    [
        ("safety", "Vấn đề an toàn khẩn cấp", SupportPriority.Urgent),
        ("payment", "Vấn đề thanh toán", SupportPriority.Normal),
        ("booking", "Vấn đề với đơn đặt", SupportPriority.Normal),
        ("account", "Tài khoản và đăng nhập", SupportPriority.Normal),
        ("other", "Vấn đề khác", SupportPriority.Normal)
    ];

    public static SupportPriority PriorityFor(string? topicKey) =>
        Topics.FirstOrDefault(t => t.Key == topicKey).Priority;

    public static string? Validate(string? subject, string? message)
    {
        if (string.IsNullOrWhiteSpace(subject)) return "Vui lòng nêu vấn đề bạn gặp.";
        if (subject.Trim().Length > SubjectMax) return $"Tiêu đề tối đa {SubjectMax} ký tự.";
        if (string.IsNullOrWhiteSpace(message)) return "Vui lòng mô tả chi tiết để nhân viên hỗ trợ nắm được.";
        if (message.Trim().Length > MessageMax) return $"Mô tả tối đa {MessageMax} ký tự.";
        return null;
    }

    /// <summary>Newest urgent first, then newest — how a support desk should see the queue.</summary>
    public static IOrderedEnumerable<SupportTicket> Queue(IEnumerable<SupportTicket> open) =>
        open.OrderByDescending(t => t.Priority == SupportPriority.Urgent)
            .ThenByDescending(t => t.CreatedAt);
}
