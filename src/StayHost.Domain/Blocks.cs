namespace StayHost.Domain;

/// <summary>docs/01 AT-10 — the plain rules and messages for the block list.</summary>
public static class Blocks
{
    public static string BlockedMessage() =>
        "Không thể gửi tin nhắn: một trong hai người đã chặn người kia.";

    public static string CannotBlockSelf() => "Bạn không thể tự chặn chính mình.";

    public static string Blocked() => "Đã chặn người dùng này.";

    public static string Unblocked() => "Đã bỏ chặn.";
}
