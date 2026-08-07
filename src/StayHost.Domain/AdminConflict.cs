namespace StayHost.Domain;

/// <summary>Why an admin must not touch this particular account.</summary>
public enum ConflictKind
{
    None = 0,
    /// <summary>It is their own account.</summary>
    Self = 1,
    /// <summary>They are the other party on a booking with this person.</summary>
    SharedBooking = 2,
    /// <summary>They already ruled on this person's case, and this is the appeal.</summary>
    AlreadyDecided = 3,
    /// <summary>Same phone, device or payout account — the platform's own "related accounts" signal.</summary>
    LinkedAccount = 4
}

/// <summary>
/// docs/08 §1.3 — "Không ai tự xử lý việc của mình. Hệ thống phải tự phát hiện
/// và chặn."
///
/// The last sentence is the requirement: this cannot be a line in a handbook,
/// because the person it applies to is the one deciding whether it applies.
/// </summary>
public static class AdminConflict
{
    /// <summary>Everything known about how an admin and a user are connected.</summary>
    public readonly record struct Links(
        bool SameAccount,
        bool ShareABooking,
        bool AdminDecidedAgainstThem,
        bool ShareContactOrDevice);

    public static ConflictKind Check(Links links)
    {
        if (links.SameAccount) return ConflictKind.Self;
        if (links.ShareABooking) return ConflictKind.SharedBooking;
        if (links.ShareContactOrDevice) return ConflictKind.LinkedAccount;
        if (links.AdminDecidedAgainstThem) return ConflictKind.AlreadyDecided;
        return ConflictKind.None;
    }

    public static bool Blocks(ConflictKind kind) => kind != ConflictKind.None;

    public static string Message(ConflictKind kind) => kind switch
    {
        ConflictKind.Self =>
            "Bạn không được thao tác lên chính tài khoản của mình. Hãy chuyển việc này cho admin khác.",
        ConflictKind.SharedBooking =>
            "Bạn có liên quan tới người này trong một đơn đặt, nên không được xử lý hồ sơ của họ. " +
            "Hãy chuyển cho admin khác.",
        ConflictKind.LinkedAccount =>
            "Tài khoản này trùng số điện thoại, thiết bị hoặc tài khoản nhận tiền với bạn. " +
            "Hãy chuyển cho admin khác.",
        ConflictKind.AlreadyDecided =>
            "Bạn là người đã ra quyết định trước đó với người này, nên không được xét lại. " +
            "Hãy chuyển cho admin khác.",
        _ => ""
    };

    /// <summary>
    /// docs/08 §1.3 covers reading as well as deciding — but blocking a support
    /// agent from opening a profile they happen to share a device fingerprint
    /// with would stop them working. Reading is watched; deciding is blocked.
    /// </summary>
    public static bool AppliesTo(AdminAction action) => AdminActions.Of(action).Decides;
}
