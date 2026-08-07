namespace StayHost.Domain;

/// <summary>docs/01 TK-10 — the four ways a notification can reach somebody.</summary>
public enum NotificationChannel
{
    InApp = 0,
    Email = 1,
    Push = 2,
    Sms = 3
}

/// <summary>
/// docs/01 TK-10 — the rows of the matrix. Coarser than <see cref="NotificationKind"/>
/// on purpose: somebody wants to decide about "đơn đặt", not about
/// BookingCreated and BookingConfirmed separately.
/// </summary>
public enum NotificationTopic
{
    /// <summary>Created, confirmed, declined, cancelled — the stay itself.</summary>
    Booking = 0,
    /// <summary>Money in and money out.</summary>
    Payment = 1,
    /// <summary>7 days and 24 hours before check-in, and check-out morning.</summary>
    Reminder = 2,
    Message = 3,
    Review = 4,
    /// <summary>Saved-listing price drops and anything else the platform is selling.</summary>
    Marketing = 5
}

/// <summary>
/// docs/01 TK-10 and docs/03 §11 — which kinds of notification reach somebody
/// through which channels.
///
/// The whole matrix is one integer: six topics × four channels is 24 bits, and
/// a preferences table with 24 rows per user would be 24 rows per user of
/// nothing. Reading a bit is <see cref="IsOn"/>, and nothing else should be
/// reading the number.
/// </summary>
public static class NotificationPrefs
{
    private static readonly NotificationTopic[] AllTopics = Enum.GetValues<NotificationTopic>();
    private static readonly NotificationChannel[] AllChannels = Enum.GetValues<NotificationChannel>();

    public static IReadOnlyList<NotificationTopic> Topics => AllTopics;
    public static IReadOnlyList<NotificationChannel> Channels => AllChannels;

    /// <summary>
    /// docs/03 §11 — "Thông báo giao dịch luôn gửi, không cho tắt." A confirmation,
    /// a cancellation or a payment is not marketing somebody may opt out of; it
    /// is the record of something that happened to their money.
    /// </summary>
    public static bool CanTurnOff(NotificationTopic topic) =>
        topic is not (NotificationTopic.Booking or NotificationTopic.Payment);

    /// <summary>
    /// In-app is where the record lives, so it is never switched off either —
    /// the bell is the one place somebody can always go back and look.
    /// </summary>
    public static bool CanTurnOff(NotificationTopic topic, NotificationChannel channel) =>
        channel != NotificationChannel.InApp && CanTurnOff(topic);

    private static int Bit(NotificationTopic topic, NotificationChannel channel) =>
        1 << ((int)topic * AllChannels.Length + (int)channel);

    /// <summary>
    /// What a new account gets: everything in the app, the things that matter by
    /// email, nothing by SMS, and marketing only where it is cheapest to ignore.
    /// </summary>
    public static int Defaults()
    {
        var mask = 0;
        foreach (var topic in AllTopics)
        {
            mask |= Bit(topic, NotificationChannel.InApp);
            if (topic != NotificationTopic.Marketing) mask |= Bit(topic, NotificationChannel.Email);
            if (topic is NotificationTopic.Booking or NotificationTopic.Reminder)
                mask |= Bit(topic, NotificationChannel.Push);
        }
        return mask;
    }

    /// <summary>
    /// Whether this notification goes out on this channel. A cell somebody may
    /// not turn off reads as on whatever the stored bit says, so a mask written
    /// by an older build — or by hand — cannot silence a cancellation notice.
    /// </summary>
    public static bool IsOn(int mask, NotificationTopic topic, NotificationChannel channel) =>
        !CanTurnOff(topic, channel) || (mask & Bit(topic, channel)) != 0;

    /// <summary>Flips one cell, ignoring the request when that cell is not the guest's to flip.</summary>
    public static int With(int mask, NotificationTopic topic, NotificationChannel channel, bool on)
    {
        if (!CanTurnOff(topic, channel)) return mask | Bit(topic, channel);
        return on ? mask | Bit(topic, channel) : mask & ~Bit(topic, channel);
    }

    /// <summary>
    /// docs/03 §11 — which row of the matrix a given notification belongs to.
    /// Everything unrecognised counts as a booking notice: a message nobody
    /// classified is more likely to matter than not.
    /// </summary>
    public static NotificationTopic TopicOf(NotificationKind kind) => kind switch
    {
        NotificationKind.MessageReceived => NotificationTopic.Message,
        NotificationKind.ReviewReceived => NotificationTopic.Review,
        NotificationKind.ListingApproved or NotificationKind.ListingRejected => NotificationTopic.Reminder,
        NotificationKind.PayoutSent => NotificationTopic.Payment,
        NotificationKind.StayReminder => NotificationTopic.Reminder,
        NotificationKind.PriceDrop => NotificationTopic.Marketing,
        _ => NotificationTopic.Booking
    };

    public static string TopicLabel(NotificationTopic topic) => topic switch
    {
        NotificationTopic.Booking => "Đơn đặt",
        NotificationTopic.Payment => "Thanh toán & chuyển tiền",
        NotificationTopic.Reminder => "Nhắc lịch chuyến đi",
        NotificationTopic.Message => "Tin nhắn",
        NotificationTopic.Review => "Đánh giá",
        _ => "Ưu đãi & gợi ý"
    };

    public static string TopicNote(NotificationTopic topic) => topic switch
    {
        NotificationTopic.Booking => "Xác nhận, từ chối, huỷ đơn. Luôn gửi.",
        NotificationTopic.Payment => "Trừ tiền, hoàn tiền, chuyển tiền cho chủ nhà. Luôn gửi.",
        NotificationTopic.Reminder => "Trước ngày nhận phòng và sáng ngày trả phòng.",
        NotificationTopic.Message => "Khi có tin nhắn mới trong hộp thư.",
        NotificationTopic.Review => "Khi được mời đánh giá và khi đánh giá được công khai.",
        _ => "Chỗ đã lưu giảm giá, gợi ý chuyến đi."
    };

    public static string ChannelLabel(NotificationChannel channel) => channel switch
    {
        NotificationChannel.InApp => "Trong ứng dụng",
        NotificationChannel.Email => "Email",
        NotificationChannel.Push => "Đẩy",
        _ => "SMS"
    };
}
