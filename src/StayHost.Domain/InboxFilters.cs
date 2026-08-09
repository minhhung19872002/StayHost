namespace StayHost.Domain;

/// <summary>docs/01 TN-05 — the ways a person narrows their message list.</summary>
public enum InboxFilter
{
    All = 0,
    Unread = 1,
    /// <summary>The other side spoke last; the ball is in the viewer's court.</summary>
    NeedsReply = 2,
    Archived = 3
}

public static class InboxFilters
{
    public static bool TryParse(string? value, out InboxFilter filter)
    {
        filter = InboxFilter.All;
        if (string.IsNullOrWhiteSpace(value)) return true;
        return Enum.TryParse(value, ignoreCase: true, out filter) && Enum.IsDefined(filter);
    }

    /// <summary>
    /// Whether a thread belongs in the current view.
    ///
    /// Archived threads are hidden from every filter except Archived itself, so a
    /// tidied-away conversation does not resurface under "unread" the moment a new
    /// message lands — the whole point of archiving is that the viewer chose to
    /// stop seeing it in the main list.
    /// </summary>
    public static bool Matches(InboxFilter filter, int unreadCount, bool lastMessageFromOther, bool isArchived)
    {
        if (filter == InboxFilter.Archived) return isArchived;
        if (isArchived) return false;

        return filter switch
        {
            InboxFilter.Unread => unreadCount > 0,
            InboxFilter.NeedsReply => lastMessageFromOther,
            _ => true
        };
    }
}
