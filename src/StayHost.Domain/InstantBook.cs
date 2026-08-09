namespace StayHost.Domain;

/// <summary>
/// docs/01 ĐP-03 — a host's conditions for instant booking. A host may require the
/// guest be identity-verified, and/or have good reviews. A guest who fails a
/// condition is not turned away: their booking falls back to request-to-book, so
/// the host still decides rather than the door being shut.
///
/// The rules are pure so the fallback decision can be tested without a database.
/// </summary>
public static class InstantBook
{
    /// <summary>A guest's average must be at least this to count as "good reviews".</summary>
    public const double GoodReviewThreshold = 4.5;

    public sealed record Eligibility(bool MayInstantBook, string? Reason);

    /// <summary>
    /// Whether this guest may instant-book this listing.
    ///
    /// A guest with no reviews yet passes the "good reviews" condition: they have
    /// no poor history, and blocking every new guest would make the marketplace
    /// unusable for anyone starting out. Only a demonstrated poor average fails
    /// it. This is the lenient reading of docs/01 ĐP-03 and is called out in
    /// docs/PLAN §9.4 as a choice the customer may want to revisit.
    /// </summary>
    public static Eligibility Check(
        bool requiresVerified, bool requiresGoodReviews,
        bool guestVerified, double? guestRating, int guestReviewCount)
    {
        if (requiresVerified && !guestVerified)
            return new(false, "Chủ nhà chỉ nhận Đặt ngay từ khách đã xác minh danh tính. "
                              + "Bạn có thể xác minh trong phần Tài khoản, hoặc gửi yêu cầu đặt.");

        if (requiresGoodReviews && guestReviewCount > 0
            && (guestRating ?? 5.0) < GoodReviewThreshold)
            return new(false, "Chủ nhà chỉ nhận Đặt ngay từ khách có đánh giá tốt. "
                              + "Bạn vẫn có thể gửi yêu cầu đặt để chủ nhà xem xét.");

        return new(true, null);
    }
}
