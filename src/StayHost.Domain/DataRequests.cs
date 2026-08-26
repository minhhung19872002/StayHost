namespace StayHost.Domain;

public enum DataRequestKind
{
    Export = 0,
    Erase = 1
}

public enum DataRequestStatus
{
    Open = 0,
    Done = 1,
    /// <summary>Cannot be done yet — see <see cref="DataRequests.Blockers"/>.</summary>
    Blocked = 2,
    Withdrawn = 3
}

/// <summary>docs/08 §9 — somebody asking for their data out, or gone.</summary>
public class DataRequest
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public DataRequestKind Kind { get; set; }
    public DataRequestStatus Status { get; set; } = DataRequestStatus.Open;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime DueBy { get; set; }
    public DateTime? CompletedAt { get; set; }

    public int? HandledByUserId { get; set; }
    public User? HandledByUser { get; set; }

    /// <summary>Why it could not be done, in words the person asking will understand.</summary>
    public string? Note { get; set; }

    /// <summary>
    /// docs/08 §9 — an export is delivered "qua đường dẫn có hạn". The token is
    /// the whole link; when <see cref="LinkExpiresAt"/> passes it stops opening.
    /// </summary>
    public string? LinkToken { get; set; }
    public DateTime? LinkExpiresAt { get; set; }
}

/// <summary>
/// docs/08 §9 — what leaves, what stays, and what stops either.
///
/// The hard part is not deletion. It is that an account carries other people's
/// records too: a host's tax position, a guest's refund, a ledger that has to
/// balance. So erasure here means the person disappears and the money does not.
/// </summary>
public static class DataRequests
{
    /// <summary>docs/08 §12 QT-D.</summary>
    public const int DueDays = 30;

    public static DateTime DueBy(DateTime askedAt) => askedAt.AddDays(DueDays);

    /// <summary>
    /// docs/08 §9 — "gửi qua đường dẫn có hạn". The link is the credential, so it
    /// is short-lived: long enough to be read from an email at the weekend, short
    /// enough that a forwarded message stops working.
    /// </summary>
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromDays(7);

    public static string ExportReadyNotice(DateTime expiresAt) =>
        $"Bản sao dữ liệu cá nhân của bạn đã sẵn sàng. Liên kết tải có hạn tới " +
        $"{expiresAt:HH:mm dd/MM/yyyy}, sau đó bạn cần yêu cầu lại.";

    public static bool Overdue(DataRequest r, DateTime now) =>
        r.Status == DataRequestStatus.Open && now > r.DueBy;

    /* ------------------------------------------------- §9, what stops it */

    /// <summary>Reasons an account cannot be erased yet.</summary>
    public readonly record struct Blockers(
        bool HasUnfinishedBooking,
        bool HasOpenDispute,
        bool OwesThePlatform,
        bool UnderInvestigation);

    public static IReadOnlyList<string> Reasons(Blockers b)
    {
        var list = new List<string>();
        if (b.HasUnfinishedBooking) list.Add("còn đơn chưa hoàn tất");
        if (b.HasOpenDispute) list.Add("còn tranh chấp đang mở");
        if (b.OwesThePlatform) list.Add("còn nợ Staylio");
        if (b.UnderInvestigation) list.Add("đang bị điều tra");
        return list;
    }

    public static bool MayErase(Blockers b) => Reasons(b).Count == 0;

    public static string BlockedMessage(Blockers b) =>
        $"Chưa xoá được tài khoản vì {string.Join(", ", Reasons(b))}. " +
        "Xử lý xong những việc này rồi yêu cầu lại.";

    /* -------------------------------------------- §9, erasure means what */

    /// <summary>docs/08 §9 — the fields that actually go.</summary>
    public static readonly IReadOnlyList<string> Erased =
    [
        "Tên", "Ảnh đại diện", "Email", "Số điện thoại", "Giấy tờ tuỳ thân",
        "Giới thiệu bản thân", "Nơi ở", "Nghề nghiệp", "Sở thích"
    ];

    /// <summary>
    /// docs/08 §9 — "Giữ lại đơn đặt, giao dịch, ghi sổ tiền và nhật ký vì nghĩa
    /// vụ kế toán và pháp lý." Told plainly, because somebody asking to be
    /// forgotten deserves to know exactly what will not be.
    /// </summary>
    public static readonly IReadOnlyList<string> Kept =
    [
        "Đơn đặt và lịch sử trạng thái của đơn",
        "Giao dịch thanh toán và hoàn tiền",
        "Sổ ghi tiền",
        "Nhật ký thao tác quản trị"
    ];

    /// <summary>
    /// docs/08 §9 — "Xoá đánh giá đã viết: không xoá — đánh giá thuộc về cộng
    /// đồng. Chỉ ẩn tên người viết."
    /// </summary>
    public static string AnonymousReviewerName() => "Người dùng đã rời Staylio";

    public static string AnonymisedName(int userId) => $"Người dùng #{userId}";

    /// <summary>
    /// The email left behind. It has to be unique, because the column is, and it
    /// must not be reachable — nothing should ever be sent to a deleted account.
    /// </summary>
    public static string AnonymisedEmail(int userId) => $"deleted-{userId}@removed.stayhost.invalid";

    public static string ErasureSummary() =>
        $"Đã xoá: {string.Join(", ", Erased).ToLowerInvariant()}. " +
        $"Giữ lại theo nghĩa vụ kế toán và pháp lý: {string.Join(", ", Kept).ToLowerInvariant()}.";

    public static string KindLabel(DataRequestKind kind) =>
        kind == DataRequestKind.Export ? "Xuất toàn bộ dữ liệu" : "Xoá tài khoản";

    public static string StatusLabel(DataRequestStatus status) => status switch
    {
        DataRequestStatus.Open => "Đang xử lý",
        DataRequestStatus.Done => "Đã xong",
        DataRequestStatus.Blocked => "Chưa xử lý được",
        _ => "Đã rút lại"
    };
}
