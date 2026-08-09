namespace StayHost.Domain.Tests;

/// <summary>docs/01 TN-05 — filtering the message list.</summary>
public class InboxFiltersTests
{
    [Theory]
    [InlineData(null, InboxFilter.All)]
    [InlineData("", InboxFilter.All)]
    [InlineData("unread", InboxFilter.Unread)]
    [InlineData("NeedsReply", InboxFilter.NeedsReply)]
    [InlineData("archived", InboxFilter.Archived)]
    public void Known_filters_parse(string? value, InboxFilter expected)
    {
        Assert.True(InboxFilters.TryParse(value, out var f));
        Assert.Equal(expected, f);
    }

    [Fact]
    public void An_unknown_filter_is_rejected()
    {
        Assert.False(InboxFilters.TryParse("nonsense", out _));
    }

    [Fact]
    public void All_shows_every_live_thread_but_not_archived_ones()
    {
        Assert.True(InboxFilters.Matches(InboxFilter.All, 0, false, isArchived: false));
        Assert.False(InboxFilters.Matches(InboxFilter.All, 3, true, isArchived: true));
    }

    [Fact]
    public void Unread_needs_an_unread_message()
    {
        Assert.True(InboxFilters.Matches(InboxFilter.Unread, 2, false, false));
        Assert.False(InboxFilters.Matches(InboxFilter.Unread, 0, true, false));
    }

    [Fact]
    public void Needs_reply_is_when_the_other_side_spoke_last()
    {
        Assert.True(InboxFilters.Matches(InboxFilter.NeedsReply, 0, lastMessageFromOther: true, false));
        Assert.False(InboxFilters.Matches(InboxFilter.NeedsReply, 0, lastMessageFromOther: false, false));
    }

    [Fact]
    public void Archived_shows_only_archived_and_the_others_hide_them()
    {
        Assert.True(InboxFilters.Matches(InboxFilter.Archived, 0, false, isArchived: true));
        Assert.False(InboxFilters.Matches(InboxFilter.Archived, 0, false, isArchived: false));

        // An archived thread with a new unread message stays out of Unread — the
        // point of archiving is that it does not come back on its own.
        Assert.False(InboxFilters.Matches(InboxFilter.Unread, 5, true, isArchived: true));
    }
}
