namespace StayHost.Domain;

/// <summary>
/// docs/01 ĐP-10 — what a guest must satisfy before a booking is allowed at all.
/// Unlike the instant-book conditions (ĐP-03), failing one of these is a hard
/// stop, not a fall back to request-to-book: the host asked that nobody stay
/// without meeting them. Pure, so the gate is tested without a database.
/// </summary>
public static class BookingPreconditions
{
    public sealed record Result(bool Ok, string? Error = null);

    /// <summary>
    /// <paramref name="hasHouseRules"/> is whether the listing has any rules to
    /// agree to; when it does, the guest must actually have ticked the box.
    /// </summary>
    public static Result Check(
        bool requirePhoto, bool requireVerified,
        bool guestHasPhoto, bool guestVerified,
        bool hasHouseRules, bool agreedToRules,
        bool platformRequiresVerified = false)
    {
        if (requirePhoto && !guestHasPhoto)
            return new(false, "Chủ nhà yêu cầu khách có ảnh đại diện trước khi đặt. "
                              + "Hãy thêm ảnh trong phần Hồ sơ.");

        // docs/07 §11 step 6 — this one is the platform's demand, not the host's,
        // so it must not be phrased as "chủ nhà yêu cầu": the guest would go and
        // argue with a host who had nothing to do with it.
        if (platformRequiresVerified && !guestVerified)
            return new(false, "Tài khoản của bạn cần xác minh danh tính trước khi đặt tiếp. "
                              + "Hãy xác minh trong phần Tài khoản.");

        if (requireVerified && !guestVerified)
            return new(false, "Chủ nhà yêu cầu khách xác minh danh tính trước khi đặt. "
                              + "Hãy xác minh trong phần Tài khoản.");

        if (hasHouseRules && !agreedToRules)
            return new(false, "Vui lòng đọc và đồng ý nội quy nhà trước khi đặt.");

        return new(true);
    }
}
