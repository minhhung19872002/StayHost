namespace StayHost.Domain;

/// <summary>
/// docs/01 XH-01 — a connection between two members. A request goes one way and is
/// accepted or declined; once accepted the two are friends, which is a symmetric
/// relationship stored as a single directed row (requester → addressee) plus a
/// status. The pair is normalised so the same two people can only have one row.
/// </summary>
public class Friendship
{
    public int Id { get; set; }
    public int RequesterId { get; set; }
    public User? Requester { get; set; }
    public int AddresseeId { get; set; }
    public User? Addressee { get; set; }
    public FriendshipStatus Status { get; set; } = FriendshipStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
}

public enum FriendshipStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2
}

/// <summary>docs/01 XH-02 — who may see a member's journey map.</summary>
public enum JourneyVisibility
{
    Private = 0,
    Friends = 1,
    Public = 2
}

/// <summary>docs/01 XH-01 — the pure rules for connecting two members.</summary>
public static class Friendships
{
    public static string? ValidateRequest(int fromUserId, int toUserId)
    {
        if (toUserId <= 0) return "Không tìm thấy người dùng.";
        if (fromUserId == toUserId) return "Bạn không thể tự kết bạn với chính mình.";
        return null;
    }

    /// <summary>
    /// Whether <paramref name="viewerId"/> is on either side of an accepted
    /// friendship — the check every friends-only view runs.
    /// </summary>
    public static bool AreFriends(Friendship f) => f.Status == FriendshipStatus.Accepted;

    /// <summary>The other person in a friendship, from one side's point of view.</summary>
    public static int Other(Friendship f, int meId) => f.RequesterId == meId ? f.AddresseeId : f.RequesterId;

    /// <summary>Only the addressee of a still-pending request may accept or decline it.</summary>
    public static bool CanRespond(Friendship f, int userId) =>
        f.Status == FriendshipStatus.Pending && f.AddresseeId == userId;

    /// <summary>
    /// docs/01 XH-02 — whether a viewer may see someone's journey map. The owner
    /// always can; otherwise it depends on the owner's chosen visibility.
    /// </summary>
    public static bool CanSeeJourney(JourneyVisibility visibility, bool isSelf, bool areFriends) =>
        isSelf
        || visibility == JourneyVisibility.Public
        || (visibility == JourneyVisibility.Friends && areFriends);
}
