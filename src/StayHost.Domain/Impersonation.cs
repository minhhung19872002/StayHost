namespace StayHost.Domain;

/// <summary>
/// docs/08 §7 — one support session spent inside somebody else's account.
///
/// "Rủi ro lạm dụng rất cao nên phải siết." Everything about this record exists
/// to make the session bounded, visible to the person whose account it is, and
/// impossible to mistake for their own activity afterwards.
/// </summary>
public class ImpersonationSession
{
    public int Id { get; set; }

    public int AdminUserId { get; set; }
    public User? AdminUser { get; set; }

    public int TargetUserId { get; set; }
    public User? TargetUser { get; set; }

    /// <summary>
    /// docs/08 §7.1 — "Chỉ mở được từ một ticket hỗ trợ đang mở, không mở tự do
    /// từ trang hồ sơ." Without a ticket there is nothing to justify it against.
    /// </summary>
    public int TicketId { get; set; }

    public string Reason { get; set; } = "";

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? EndedAt { get; set; }

    /// <summary>
    /// docs/08 §7.4 — the person is told, unless a fraud investigation was
    /// separately approved to stay quiet. Approved by a name, not a checkbox.
    /// </summary>
    public bool NotifiedTarget { get; set; }
    public int? SilenceApprovedByUserId { get; set; }

    public bool IsLive(DateTime now) => EndedAt is null && now < ExpiresAt;
}

/// <summary>docs/08 §7 — the seven constraints, as rules rather than intentions.</summary>
public static class Impersonation
{
    /// <summary>docs/08 §12 QT-B.</summary>
    public static readonly TimeSpan MaxDuration = TimeSpan.FromMinutes(30);

    public static DateTime ExpiryFor(DateTime startedAt) => startedAt + MaxDuration;

    public static bool Expired(ImpersonationSession s, DateTime now) => now >= s.ExpiresAt;

    public static TimeSpan Remaining(ImpersonationSession s, DateTime now)
    {
        var left = s.ExpiresAt - now;
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }

    /* ------------------------------------------------- §7.6, the hard nos */

    /// <summary>
    /// docs/08 §7.6 — "Cấm tuyệt đối trong chế độ này". Everything on this list
    /// either changes who controls the account or moves money; a support session
    /// has no business doing either, and a compromised admin account must not be
    /// able to either.
    /// </summary>
    private static readonly Dictionary<string, string> Forbidden = new()
    {
        ["change-password"] = "đổi mật khẩu",
        ["change-email"] = "đổi email",
        ["change-phone"] = "đổi số điện thoại",
        ["change-payout-account"] = "đổi tài khoản nhận tiền",
        ["manage-payment-methods"] = "thêm hoặc xoá phương thức thanh toán",
        ["create-booking"] = "tạo đơn mới",
        ["cancel-booking"] = "huỷ đơn",
        ["withdraw"] = "rút tiền",
        ["delete-account"] = "xoá tài khoản"
    };

    public static bool IsForbidden(string? capability) =>
        capability is not null && Forbidden.ContainsKey(capability);

    public static IReadOnlyList<string> ForbiddenLabels => Forbidden.Values.ToList();

    public static string ForbiddenMessage(string capability) =>
        $"Không được {Forbidden.GetValueOrDefault(capability, "làm việc này")} khi đang ở chế độ thay mặt người dùng.";

    /// <summary>
    /// The route prefixes a write during a session may not reach (reads are the
    /// point of the mode and pass). The list above is what a person would say;
    /// this is what the server matches on, and the tests pin this method to the
    /// application's real routes — not to the labels, which is how "đổi số điện
    /// thoại" once sat on the list while PUT /api/account/profile walked past it.
    /// </summary>
    public static bool BlocksPath(string path)
    {
        var p = (path ?? "").ToLowerInvariant().TrimEnd('/');

        return p.Contains("/api/account/change-password")     // §7.6 password
               || p.EndsWith("/reset-password") || p.EndsWith("/forgot-password")
               || p == "/api/account/profile"                 // phone lives here
               || p.EndsWith("/send-code") || p.EndsWith("/confirm-code")  // attaching identifiers
               || p.StartsWith("/api/account/two-factor")     // the door behind the password
               || p.Contains("/api/host/payout")              // payout account
               || p.Contains("/api/payment-methods")          // cards
               || p.Contains("/api/account/delete")
               || p.StartsWith("/api/account/data-requests")  // §9 erase intake
               || p.StartsWith("/api/wallet")                 // credits and gift cards are money
               || p.EndsWith("/cancel")                       // cancelling anything
               || p == "/api/bookings";                       // creating a booking
    }

    /* ------------------------------------------- §7.5 and §7.7, being seen */

    public static string BannerText(string adminName, string targetName, TimeSpan left) =>
        $"Bạn đang thao tác THAY MẶT {targetName}. Admin: {adminName}. " +
        $"Còn {Math.Max(0, (int)left.TotalMinutes)} phút.";

    /// <summary>
    /// docs/08 §7.7 — "không được ghi như thể người dùng tự làm". The audit
    /// actor is always both names, never just the account's.
    /// </summary>
    public static string ActorTag(string adminName, string targetName) =>
        $"{adminName} thay mặt {targetName}";

    /// <summary>docs/08 §7.4 — what the account holder is told, and when.</summary>
    public static string TargetNotice(string adminName, DateTime at, string reason) =>
        $"Nhân viên hỗ trợ {adminName} đã truy cập tài khoản của bạn lúc {at:HH:mm dd/MM/yyyy}. " +
        $"Lý do: {reason}. Nếu bạn thấy bất thường, hãy liên hệ Staylio ngay.";
}
