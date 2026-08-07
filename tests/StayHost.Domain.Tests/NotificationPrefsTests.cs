using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>docs/01 TK-10 and docs/03 §11 — the notification matrix.</summary>
public class NotificationPrefsTests
{
    [Fact]
    public void The_matrix_has_a_cell_for_every_topic_and_channel()
    {
        Assert.Equal(6, NotificationPrefs.Topics.Count);
        Assert.Equal(4, NotificationPrefs.Channels.Count);
    }

    [Fact]
    public void Turning_one_cell_off_leaves_every_other_cell_alone()
    {
        var mask = NotificationPrefs.Defaults();
        var after = NotificationPrefs.With(mask, NotificationTopic.Message, NotificationChannel.Email, false);

        Assert.False(NotificationPrefs.IsOn(after, NotificationTopic.Message, NotificationChannel.Email));

        foreach (var topic in NotificationPrefs.Topics)
            foreach (var channel in NotificationPrefs.Channels)
            {
                if (topic == NotificationTopic.Message && channel == NotificationChannel.Email) continue;
                Assert.Equal(
                    NotificationPrefs.IsOn(mask, topic, channel),
                    NotificationPrefs.IsOn(after, topic, channel));
            }
    }

    [Fact]
    public void A_cell_turned_off_and_back_on_is_where_it_started()
    {
        var mask = NotificationPrefs.Defaults();
        var off = NotificationPrefs.With(mask, NotificationTopic.Review, NotificationChannel.Email, false);
        var on = NotificationPrefs.With(off, NotificationTopic.Review, NotificationChannel.Email, true);
        Assert.Equal(mask, on);
    }

    /* -------------------------------------------------------- docs/03 §11 */

    [Fact]
    public void A_transactional_notice_cannot_be_switched_off()
    {
        // "Thông báo giao dịch (xác nhận đơn, huỷ, thanh toán) luôn gửi, không cho tắt."
        foreach (var topic in new[] { NotificationTopic.Booking, NotificationTopic.Payment })
            foreach (var channel in NotificationPrefs.Channels)
            {
                var mask = NotificationPrefs.With(NotificationPrefs.Defaults(), topic, channel, false);
                Assert.True(NotificationPrefs.IsOn(mask, topic, channel),
                    $"{topic}/{channel} was silenced");
            }
    }

    [Fact]
    public void A_mask_of_zero_still_sends_what_may_not_be_silenced()
    {
        // Whatever is in the column — an older build's number, a hand-edited row —
        // a cancellation notice still goes out.
        Assert.True(NotificationPrefs.IsOn(0, NotificationTopic.Booking, NotificationChannel.Email));
        Assert.True(NotificationPrefs.IsOn(0, NotificationTopic.Payment, NotificationChannel.InApp));
        Assert.False(NotificationPrefs.IsOn(0, NotificationTopic.Marketing, NotificationChannel.Email));
    }

    [Fact]
    public void Marketing_must_be_switchable_off_on_every_channel_that_leaves_the_app()
    {
        // "Thông báo tiếp thị phải cho tắt."
        foreach (var channel in NotificationPrefs.Channels)
        {
            var mask = NotificationPrefs.With(NotificationPrefs.Defaults(), NotificationTopic.Marketing, channel, false);
            if (channel == NotificationChannel.InApp) continue;
            Assert.False(NotificationPrefs.IsOn(mask, NotificationTopic.Marketing, channel));
        }
    }

    [Fact]
    public void The_bell_is_never_switched_off_because_it_is_the_record()
    {
        foreach (var topic in NotificationPrefs.Topics)
            Assert.False(NotificationPrefs.CanTurnOff(topic, NotificationChannel.InApp));
    }

    [Fact]
    public void Nobody_starts_out_being_texted()
    {
        var mask = NotificationPrefs.Defaults();
        foreach (var topic in NotificationPrefs.Topics)
        {
            if (!NotificationPrefs.CanTurnOff(topic, NotificationChannel.Sms)) continue;
            Assert.False(NotificationPrefs.IsOn(mask, topic, NotificationChannel.Sms), $"{topic} defaults to SMS");
        }
    }

    [Fact]
    public void Marketing_email_is_off_until_somebody_asks_for_it()
    {
        Assert.False(NotificationPrefs.IsOn(
            NotificationPrefs.Defaults(), NotificationTopic.Marketing, NotificationChannel.Email));
    }

    /* ------------------------------------- which row a notification lands in */

    [Fact]
    public void Every_kind_of_notification_has_a_row_to_land_in()
    {
        foreach (NotificationKind kind in Enum.GetValues<NotificationKind>())
            Assert.Contains(NotificationPrefs.TopicOf(kind), NotificationPrefs.Topics);
    }

    [Fact]
    public void Money_and_stays_are_classified_as_such()
    {
        Assert.Equal(NotificationTopic.Payment, NotificationPrefs.TopicOf(NotificationKind.PayoutSent));
        Assert.Equal(NotificationTopic.Booking, NotificationPrefs.TopicOf(NotificationKind.BookingCancelled));
        Assert.Equal(NotificationTopic.Message, NotificationPrefs.TopicOf(NotificationKind.MessageReceived));
        Assert.Equal(NotificationTopic.Review, NotificationPrefs.TopicOf(NotificationKind.ReviewReceived));
        Assert.Equal(NotificationTopic.Marketing, NotificationPrefs.TopicOf(NotificationKind.PriceDrop));
        Assert.Equal(NotificationTopic.Reminder, NotificationPrefs.TopicOf(NotificationKind.StayReminder));
    }

    [Fact]
    public void An_unclassified_notification_is_treated_as_one_that_matters()
    {
        // Better a notice somebody did not need than a cancellation they never saw.
        Assert.Equal(NotificationTopic.Booking, NotificationPrefs.TopicOf(NotificationKind.System));
        Assert.False(NotificationPrefs.CanTurnOff(NotificationTopic.Booking));
    }

    [Fact]
    public void Every_row_and_column_has_wording_of_its_own()
    {
        var topics = new HashSet<string>();
        foreach (var topic in NotificationPrefs.Topics)
        {
            Assert.True(topics.Add(NotificationPrefs.TopicLabel(topic)), $"{topic} reuses a label");
            Assert.NotEmpty(NotificationPrefs.TopicNote(topic));
        }

        var channels = new HashSet<string>();
        foreach (var channel in NotificationPrefs.Channels)
            Assert.True(channels.Add(NotificationPrefs.ChannelLabel(channel)), $"{channel} reuses a label");
    }
}
