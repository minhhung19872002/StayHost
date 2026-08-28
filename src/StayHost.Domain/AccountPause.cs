namespace StayHost.Domain;

/// <summary>
/// docs/01 TK-12 — "tạm vô hiệu hoá hoặc xoá tài khoản". The erase half has
/// existed since the data-request work; this is the other half, and it had
/// nothing at all: no column, no endpoint, no button. The code was ticked
/// because half of an "hoặc" was done, which is the same way YT-08 was ticked
/// while its second clause had never been built.
///
/// A pause is not a sanction. docs/08 §5 suspensions are something the platform
/// does to somebody, with a policy, an appeal and a record; this is somebody
/// stepping away from their own account and coming back when they want. They
/// must never be stored in the same flag: an admin reading the console has to be
/// able to tell "we stopped this person" from "this person stopped".
/// </summary>
public static class AccountPause
{
    /// <summary>Why a pause is being refused, if it is.</summary>
    public enum Refusal
    {
        None = 0,
        /// <summary>Somebody is staying, or about to. Vanishing mid-trip is the other side's problem.</summary>
        HasLiveBookings,
        /// <summary>docs/08 §5 — a suspended account is not the owner's to pause or resume.</summary>
        UnderSanction,
        /// <summary>docs/01 TC-01 — money the platform is holding has to land somewhere first.</summary>
        HasMoneyInFlight
    }

    public readonly record struct Check(bool Ok, Refusal Reason, string Message)
    {
        public static Check Pass => new(true, Refusal.None, "");
        public static Check Fail(Refusal reason, string message) => new(false, reason, message);
    }

    /// <summary>
    /// Whether this account may step away right now.
    ///
    /// <paramref name="liveBookings"/> counts stays that are confirmed, in
    /// progress or waiting on this person — as guest or as host. A pause is
    /// reversible and quiet, so it is allowed generously; the one thing it must
    /// not do is leave a booked guest without a host, or a host without the
    /// guest who is arriving on Friday.
    /// </summary>
    public static Check CanPause(
        bool isSuspended, bool isBanned, int liveBookings, decimal moneyOwedToUser)
    {
        if (isSuspended || isBanned)
            return Check.Fail(Refusal.UnderSanction,
                "Tài khoản đang bị hạn chế; hãy dùng mục khiếu nại thay vì tạm dừng.");

        if (liveBookings > 0)
            return Check.Fail(Refusal.HasLiveBookings,
                $"Còn {liveBookings} đơn đang hiệu lực. Hoàn tất hoặc huỷ trước khi tạm dừng tài khoản.");

        if (moneyOwedToUser > 0)
            return Check.Fail(Refusal.HasMoneyInFlight,
                "Sàn còn khoản phải chuyển cho bạn. Đợi chuyển xong rồi hãy tạm dừng.");

        return Check.Pass;
    }

    /// <summary>
    /// docs/01 TK-12 — a pause ends by coming back. Signing in is the whole
    /// gesture: an account nobody can reach to un-pause is a deletion wearing a
    /// friendlier word, and the document says the two are different things.
    /// </summary>
    public static bool ResumesOnSignIn => true;

    /// <summary>What the person is told while it lasts.</summary>
    public const string Notice =
        "Tài khoản của bạn đang tạm dừng. Tin đăng được ẩn khỏi tìm kiếm và không ai đặt được. "
        + "Đăng nhập lại bất cứ lúc nào để mở lại — dữ liệu của bạn vẫn nguyên.";
}
