namespace StayHost.Domain;

public enum AppealStatus
{
    Open = 0,
    /// <summary>docs/08 §8 — the decision stands.</summary>
    Upheld = 1,
    /// <summary>Reduced to a lighter step.</summary>
    Reduced = 2,
    /// <summary>Removed outright, and off the record with it.</summary>
    Overturned = 3
}

/// <summary>docs/08 §8 — somebody says a decision about them was wrong.</summary>
public class Appeal
{
    public int Id { get; set; }

    public int SanctionId { get; set; }
    public Sanction? Sanction { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public string Argument { get; set; } = "";

    public AppealStatus Status { get; set; } = AppealStatus.Open;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime DueBy { get; set; }

    /* -------------------------------------------------------- the review */

    public int? ReviewedByUserId { get; set; }
    public User? ReviewedByUser { get; set; }
    public DateTime? ReviewedAt { get; set; }

    /// <summary>docs/08 §8 — "không được trả lời cụt lủn".</summary>
    public string? Outcome { get; set; }

    /// <summary>When reduced, what it became.</summary>
    public SanctionLevel? ReducedTo { get; set; }
}

/// <summary>docs/08 §8 — one appeal, a different reader, a real answer.</summary>
public static class Appeals
{
    /// <summary>docs/08 §12 QT-C.</summary>
    public const int WindowDays = 30;

    /// <summary>docs/08 §8 — "Trả lời trong 7 ngày làm việc."</summary>
    public const int AnswerWorkingDays = 7;

    public static DateTime WindowClosesAt(DateTime decidedAt) => decidedAt.AddDays(WindowDays);

    public static bool WithinWindow(DateTime decidedAt, DateTime now) => now <= WindowClosesAt(decidedAt);

    /// <summary>Seven working days, counted the way a person counts them.</summary>
    public static DateTime DueBy(DateTime filedAt)
    {
        var at = filedAt;
        var left = AnswerWorkingDays;

        while (left > 0)
        {
            at = at.AddDays(1);
            if (at.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) left--;
        }

        return at;
    }

    public static bool Overdue(Appeal a, DateTime now) => a.Status == AppealStatus.Open && now > a.DueBy;

    /* --------------------------------------------------- who may look at it */

    /// <summary>
    /// docs/08 §8 — "Người xét lại phải khác người ra quyết định ban đầu."
    ///
    /// Not a formality: an appeal read by the person who made the call is the
    /// same call twice, and the appellant can tell.
    /// </summary>
    public static bool MayReview(int reviewerUserId, int originalDeciderUserId) =>
        reviewerUserId != originalDeciderUserId;

    public static string SameReviewerMessage() =>
        "Bạn là người đã ra quyết định này, nên không được xét lại nó. " +
        "Hãy chuyển hồ sơ cho người khác.";

    /* --------------------------------------------------------- one attempt */

    /// <summary>docs/08 §8 — "khiếu nại một lần".</summary>
    public static bool MayFile(bool alreadyFiled, DateTime decidedAt, DateTime now) =>
        !alreadyFiled && WithinWindow(decidedAt, now);

    public static string CannotFileMessage(bool alreadyFiled, DateTime decidedAt) =>
        alreadyFiled
            ? "Bạn đã khiếu nại quyết định này một lần rồi. Mỗi quyết định chỉ được khiếu nại một lần."
            : $"Hạn khiếu nại quyết định này là {WindowClosesAt(decidedAt):dd/MM/yyyy} và đã qua.";

    /// <summary>
    /// The least an argument can be and still argue something. Shorter than the
    /// answer minimum on purpose: the person appealing is not the professional.
    /// </summary>
    public const int ArgumentMinLength = 20;

    public static bool ArgumentIsUsable(string? argument) =>
        (argument ?? "").Trim().Length >= ArgumentMinLength;

    public static string CurtArgumentMessage() =>
        $"Hãy nêu lý do bạn cho rằng quyết định sai, ít nhất {ArgumentMinLength} ký tự.";

    /* ------------------------------------------------------------ results */

    public static string StatusLabel(AppealStatus status) => status switch
    {
        AppealStatus.Open => "Đang xét",
        AppealStatus.Upheld => "Giữ nguyên quyết định",
        AppealStatus.Reduced => "Giảm mức",
        _ => "Gỡ bỏ hoàn toàn"
    };

    /// <summary>
    /// docs/08 §8 — an overturned decision comes off the record entirely, which
    /// matters because the ladder in §5 counts what came before.
    /// </summary>
    public static bool ClearsTheRecord(AppealStatus status) => status == AppealStatus.Overturned;

    /// <summary>The minimum an answer can be and still be an answer.</summary>
    public const int OutcomeMinLength = 40;

    public static bool OutcomeIsUsable(string? outcome) =>
        (outcome ?? "").Trim().Length >= OutcomeMinLength;

    public static string CurtOutcomeMessage() =>
        $"Kết quả khiếu nại phải nêu lý do, ít nhất {OutcomeMinLength} ký tự. " +
        "Trả lời cụt lủn không phải là trả lời.";
}
