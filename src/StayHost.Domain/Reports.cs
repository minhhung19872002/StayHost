namespace StayHost.Domain;

/// <summary>
/// docs/01 AT-02 — what may be reported and what makes a report worth opening.
/// Until now only a listing could be reported (`TĐ-19`); the other three subjects
/// the spec asks for had no way in at all.
/// </summary>
public static class Reports
{
    public const int ReasonMax = 120;
    public const int DetailMax = 2000;

    /// <summary>The reasons offered per subject. Free text goes in the detail box.</summary>
    public static IReadOnlyList<string> ReasonsFor(ReportTarget target) => target switch
    {
        ReportTarget.Listing =>
        [
            "Thông tin sai sự thật", "Ảnh không đúng chỗ ở", "Chỗ ở không tồn tại",
            "Giá hoặc phí gây hiểu lầm", "Nội dung không phù hợp", "Nghi ngờ lừa đảo"
        ],
        ReportTarget.User =>
        [
            "Quấy rối hoặc xúc phạm", "Phân biệt đối xử", "Mạo danh người khác",
            "Nghi ngờ lừa đảo", "Hành vi không an toàn"
        ],
        ReportTarget.Message =>
        [
            "Quấy rối hoặc xúc phạm", "Spam hoặc quảng cáo", "Đòi giao dịch ngoài sàn",
            "Nội dung không phù hợp", "Nghi ngờ lừa đảo"
        ],
        ReportTarget.Review =>
        [
            "Sai sự thật", "Xúc phạm cá nhân", "Không nói về kỳ lưu trú",
            "Lộ thông tin riêng tư", "Nghi ngờ đánh giá giả"
        ],
        _ => []
    };

    public static string TargetLabel(ReportTarget target) => target switch
    {
        ReportTarget.Listing => "Tin đăng",
        ReportTarget.User => "Người dùng",
        ReportTarget.Message => "Tin nhắn",
        ReportTarget.Review => "Đánh giá",
        _ => "Không rõ"
    };

    public static bool TryParseTarget(string? value, out ReportTarget target) =>
        Enum.TryParse(value, ignoreCase: true, out target) && Enum.IsDefined(target);

    /// <summary>
    /// Everything that can be judged without touching the database. The caller
    /// still has to prove the subject exists and that the reporter is allowed to
    /// see it — a private message is not reportable by somebody outside the thread.
    /// </summary>
    public static string? Validate(ReportTarget target, int subjectId, string? reason, string? detail)
    {
        if (!Enum.IsDefined(target)) return "Loại báo cáo không hợp lệ.";
        if (subjectId <= 0) return $"Thiếu {TargetLabel(target).ToLowerInvariant()} cần báo cáo.";

        var trimmed = (reason ?? "").Trim();
        if (trimmed.Length == 0) return "Vui lòng chọn lý do báo cáo.";
        if (trimmed.Length > ReasonMax) return $"Lý do tối đa {ReasonMax} ký tự.";

        if ((detail ?? "").Trim().Length > DetailMax)
            return $"Mô tả tối đa {DetailMax} ký tự.";

        return null;
    }

    /// <summary>
    /// Reporting yourself is never a moderation signal, and a report against your
    /// own account would sit in the queue drawing a moderator away from a real one.
    /// Covers the review and message cases too, where the subject belongs to
    /// somebody: the author is the person being reported.
    /// </summary>
    public static bool IsSelfReport(int? reporterUserId, int? subjectOwnerUserId) =>
        reporterUserId is not null && subjectOwnerUserId is not null
        && reporterUserId == subjectOwnerUserId;
}
