namespace StayHost.Domain;

/// <summary>
/// docs/01 AT-08 — the automated helper that reads someone's current situation and
/// offers the next useful thing to do, each with a button that goes straight there.
///
/// It is a rule over facts, not a chatbot: given what is true about the person's
/// trips right now, it returns the handful of actions that actually apply, most
/// urgent first. When nothing specific applies it falls back to the help centre and
/// a human (AT-09), so the panel is never a dead end.
/// </summary>
public static class SupportAssistant
{
    /// <summary>What the assistant needs to know, all pre-computed so this stays pure.</summary>
    public readonly record struct Context(
        bool LoggedIn,
        bool HasArrivalSoon,      // a confirmed trip checking in within 48h
        bool HasBalanceDue,       // a partial payment with a balance still owed
        bool HasPendingRequest,   // a request-to-book awaiting the host
        bool HasUnreviewedStay,   // a completed stay the guest has not reviewed
        bool HasOpenDispute,      // an open resolution case
        bool IsHost,
        bool HasHostRequestsToAnswer);

    public readonly record struct Suggestion(string Text, string ActionLabel, string ActionLink);

    public static IReadOnlyList<Suggestion> Suggest(Context c)
    {
        var list = new List<Suggestion>();

        if (!c.LoggedIn)
        {
            list.Add(new("Đăng nhập để chúng tôi trợ giúp theo đúng đơn của bạn.", "Đăng nhập", "/?login=1"));
            list.Add(new("Tìm câu trả lời nhanh trong Trung tâm trợ giúp.", "Mở trợ giúp", "/help"));
            return list;
        }

        // Most time-sensitive first.
        if (c.HasOpenDispute)
            list.Add(new("Bạn có một hồ sơ giải quyết đang mở.", "Xem hồ sơ", "/resolutions"));
        if (c.HasBalanceDue)
            list.Add(new("Một đơn của bạn còn số dư cần thanh toán trước ngày nhận phòng.", "Thanh toán nốt", "/trips"));
        if (c.HasArrivalSoon)
            list.Add(new("Sắp tới ngày nhận phòng — xem hướng dẫn nhận phòng và địa chỉ.", "Xem chuyến đi", "/trips"));
        if (c.HasPendingRequest)
            list.Add(new("Bạn có yêu cầu đặt đang chờ chủ nhà phản hồi.", "Xem yêu cầu", "/trips"));
        if (c.HasUnreviewedStay)
            list.Add(new("Bạn có chuyến đã hoàn tất chưa đánh giá.", "Viết đánh giá", "/trips"));
        if (c.HasHostRequestsToAnswer)
            list.Add(new("Có yêu cầu đặt đang chờ bạn duyệt.", "Mở trang chủ nhà", "/hosting"));

        // Always available, so the panel is never empty and a human is one tap away.
        list.Add(new("Không thấy điều bạn cần? Xem Trung tâm trợ giúp.", "Mở trợ giúp", "/help"));
        list.Add(new("Cần người thật hỗ trợ? Gửi yêu cầu cho đội StayHost.", "Liên hệ hỗ trợ", "/help?support=1"));

        return list;
    }
}
