namespace StayHost.Domain;

/// <summary>
/// Every row of the permission matrix in docs/08 §2. One entry per thing an
/// admin can do, so "may they?" is asked in one place rather than re-decided in
/// each controller.
/// </summary>
public enum AdminAction
{
    /* ---------------------------------------------------------- reading */
    FindUser = 0,
    ViewBookingHistory = 1,
    /// <summary>Messages on one specific booking, never the rest of an inbox.</summary>
    ViewBookingThread = 2,
    ViewIdentityDocuments = 3,
    /// <summary>The last four digits of where a host's money goes. Never more.</summary>
    ViewPayoutAccount = 4,
    ViewOtherAdminLog = 5,

    /* --------------------------------------------------------- deciding */
    EditProfile = 10,
    Warn = 11,
    Restrict = 12,
    Suspend = 13,
    Ban = 14,
    Restore = 15,
    TakeDownContent = 16,
    ForcePasswordReset = 17,
    ForceIdentityRecheck = 18,
    ManualRefund = 19,
    AdjustPayout = 20,
    Impersonate = 21,
    MergeAccounts = 22,
    EraseData = 23,
    ManageAdmins = 24
}

/// <summary>
/// docs/08 §2 — who may do what, and §1 — the four principles that hang off it.
///
/// The matrix is data, not a pile of if-statements: the API asks it before
/// acting and the console asks it before offering a button, so a role can never
/// see an action it would be refused.
/// </summary>
public static class AdminActions
{
    /// <param name="Scopes">Union of the roles allowed. Super is always allowed on top.</param>
    /// <param name="Sensitive">
    /// The ✓* rows of §2: permitted, but each use needs a reason and gets its own
    /// audit line even though it only reads.
    /// </param>
    public readonly record struct Rule(
        AdminAction Action, string Label, AdminScope Scopes, bool Decides, bool Sensitive = false);

    private static readonly Rule[] Matrix =
    [
        /* ---- reading ---- */
        new(AdminAction.FindUser, "Tìm và xem hồ sơ người dùng",
            AdminScope.Support | AdminScope.Moderation | AdminScope.Finance, Decides: false),
        new(AdminAction.ViewBookingHistory, "Xem lịch sử đơn đặt của người dùng",
            AdminScope.Support | AdminScope.Moderation | AdminScope.Finance, Decides: false),
        new(AdminAction.ViewBookingThread, "Xem nội dung tin nhắn của một đơn cụ thể",
            AdminScope.Support | AdminScope.Moderation, Decides: false, Sensitive: true),
        new(AdminAction.ViewIdentityDocuments, "Xem ảnh giấy tờ tuỳ thân",
            AdminScope.Moderation, Decides: false, Sensitive: true),
        new(AdminAction.ViewPayoutAccount, "Xem thông tin tài khoản nhận tiền",
            AdminScope.Finance, Decides: false),
        new(AdminAction.ViewOtherAdminLog, "Xem nhật ký thao tác của admin khác",
            AdminScope.None, Decides: false),

        /* ---- deciding ---- */
        new(AdminAction.EditProfile, "Sửa thông tin hồ sơ người dùng",
            AdminScope.Support, Decides: true, Sensitive: true),
        new(AdminAction.Warn, "Gửi cảnh cáo",
            AdminScope.Support | AdminScope.Moderation, Decides: true),
        new(AdminAction.Restrict, "Hạn chế một phần", AdminScope.Moderation, Decides: true),
        new(AdminAction.Suspend, "Tạm khoá tài khoản", AdminScope.Moderation, Decides: true),
        new(AdminAction.Ban, "Khoá vĩnh viễn", AdminScope.None, Decides: true),
        new(AdminAction.Restore, "Khôi phục tài khoản đã khoá",
            AdminScope.Moderation, Decides: true, Sensitive: true),
        new(AdminAction.TakeDownContent, "Gỡ tin đăng, ảnh hoặc đánh giá",
            AdminScope.Moderation, Decides: true),
        new(AdminAction.ForcePasswordReset, "Buộc đổi mật khẩu, huỷ mọi phiên đăng nhập",
            AdminScope.Support | AdminScope.Moderation, Decides: true),
        new(AdminAction.ForceIdentityRecheck, "Buộc xác minh lại danh tính",
            AdminScope.Moderation, Decides: true),
        new(AdminAction.ManualRefund, "Hoàn tiền thủ công", AdminScope.Finance, Decides: true),
        new(AdminAction.AdjustPayout, "Điều chỉnh khoản trả cho chủ nhà", AdminScope.Finance, Decides: true),
        new(AdminAction.Impersonate, "Đăng nhập thay mặt người dùng",
            AdminScope.Support, Decides: true, Sensitive: true),
        new(AdminAction.MergeAccounts, "Hợp nhất tài khoản trùng", AdminScope.None, Decides: true),
        new(AdminAction.EraseData, "Xoá dữ liệu theo yêu cầu người dùng", AdminScope.None, Decides: true),
        new(AdminAction.ManageAdmins, "Tạo và phân quyền tài khoản admin", AdminScope.None, Decides: true)
    ];

    public static Rule Of(AdminAction action) => Matrix.First(r => r.Action == action);

    public static IReadOnlyList<Rule> All => Matrix;

    /// <summary>
    /// docs/08 §2 — "Một người có thể mang nhiều vai. Quyền là hợp của các vai."
    /// A row with no roles listed is Super's alone.
    /// </summary>
    public static bool Allows(AdminScope held, AdminAction action)
    {
        if (held.HasFlag(AdminScope.Super)) return true;

        var rule = Of(action);
        return rule.Scopes != AdminScope.None && (held & rule.Scopes) != 0;
    }

    /// <summary>
    /// docs/08 §1.4 — "Không cho lưu quyết định mà bỏ trống ô lý do", plus the
    /// ✓* reads of §2, which need one even though they change nothing.
    /// </summary>
    public static bool RequiresReason(AdminAction action)
    {
        var rule = Of(action);
        return rule.Decides || rule.Sensitive;
    }

    /// <summary>The shortest reason worth recording. Below this it is not a reason.</summary>
    public const int ReasonMinLength = 10;

    public static bool ReasonIsUsable(string? reason) =>
        (reason ?? "").Trim().Length >= ReasonMinLength;

    public static string MissingReasonMessage(AdminAction action) =>
        $"\"{Of(action).Label}\" cần ghi lý do ít nhất {ReasonMinLength} ký tự trước khi lưu.";

    public static string DeniedMessage(AdminAction action) =>
        $"Tài khoản của bạn không có quyền: {Of(action).Label.ToLowerInvariant()}.";

    /// <summary>Roles a person would need, for the refusal message and the console.</summary>
    public static string RequiredRoles(AdminAction action)
    {
        var rule = Of(action);
        if (rule.Scopes == AdminScope.None) return "Quản trị tối cao";

        var names = new List<string>();
        if (rule.Scopes.HasFlag(AdminScope.Support)) names.Add("Hỗ trợ");
        if (rule.Scopes.HasFlag(AdminScope.Moderation)) names.Add("Kiểm duyệt");
        if (rule.Scopes.HasFlag(AdminScope.Finance)) names.Add("Tài chính");
        if (rule.Scopes.HasFlag(AdminScope.Arbitration)) names.Add("Phân xử");
        names.Add("Quản trị tối cao");
        return string.Join(", ", names);
    }

    /* ------------------------------------------------------- §3, sessions */

    /// <summary>docs/08 §12 QT-A — an idle admin session ends by itself.</summary>
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);

    public static bool SessionExpired(DateTime lastSeenAt, DateTime now) =>
        now - lastSeenAt > IdleTimeout;

    /// <summary>
    /// docs/08 §3 — "Bắt buộc bảo mật 2 lớp. Không bật thì không đăng nhập được,
    /// không có ngoại lệ." The exception-free wording is the point: this is not a
    /// nag, it is a gate.
    /// </summary>
    public static bool MayHoldAdminSession(bool twoFactorEnabled) => twoFactorEnabled;

    public static string TwoFactorRequiredMessage() =>
        "Tài khoản quản trị bắt buộc bật bảo mật 2 lớp mới đăng nhập được. " +
        "Hãy bật trong phần Bảo mật của tài khoản.";

    /* --------------------------------------------- §3, the quarterly review */

    /// <summary>docs/08 §3 — a permission nobody has used in this long is worth taking back.</summary>
    public const int StaleScopeDays = 90;

    public static bool ScopeLooksUnused(DateTime? lastUsedAt, DateTime now) =>
        lastUsedAt is null || (now - lastUsedAt.Value).TotalDays > StaleScopeDays;

    public static DateOnly NextAccessReview(DateOnly lastReview) => lastReview.AddMonths(3);

    public static bool AccessReviewDue(DateOnly? lastReview, DateOnly today) =>
        lastReview is null || today >= NextAccessReview(lastReview.Value);
}
