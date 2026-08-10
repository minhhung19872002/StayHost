namespace StayHost.Domain.Tests;

/// <summary>docs/01 XH-01 — the friend-connection rules.</summary>
public class FriendshipTests
{
    [Fact]
    public void Cannot_friend_yourself_or_a_missing_user()
    {
        Assert.NotNull(Friendships.ValidateRequest(5, 5));
        Assert.NotNull(Friendships.ValidateRequest(5, 0));
        Assert.Null(Friendships.ValidateRequest(5, 6));
    }

    [Fact]
    public void The_other_side_is_symmetric()
    {
        var f = new Friendship { RequesterId = 3, AddresseeId = 7 };
        Assert.Equal(7, Friendships.Other(f, 3));
        Assert.Equal(3, Friendships.Other(f, 7));
    }

    [Fact]
    public void Only_the_addressee_of_a_pending_request_may_respond()
    {
        var f = new Friendship { RequesterId = 3, AddresseeId = 7, Status = FriendshipStatus.Pending };
        Assert.True(Friendships.CanRespond(f, 7));
        Assert.False(Friendships.CanRespond(f, 3));   // the requester cannot accept their own
    }

    [Fact]
    public void An_answered_request_can_no_longer_be_responded_to()
    {
        var f = new Friendship { RequesterId = 3, AddresseeId = 7, Status = FriendshipStatus.Accepted };
        Assert.False(Friendships.CanRespond(f, 7));
        Assert.True(Friendships.AreFriends(f));
    }

    [Theory]
    // visibility, isSelf, areFriends, expected
    [InlineData(JourneyVisibility.Private, true, false, true)]    // owner always sees own
    [InlineData(JourneyVisibility.Private, false, true, false)]   // private hides even from friends
    [InlineData(JourneyVisibility.Friends, false, true, true)]    // friends see friends-only
    [InlineData(JourneyVisibility.Friends, false, false, false)]  // strangers do not
    [InlineData(JourneyVisibility.Public, false, false, true)]    // public is open
    public void Journey_visibility_follows_the_owners_choice(
        JourneyVisibility v, bool isSelf, bool friends, bool expected)
    {
        Assert.Equal(expected, Friendships.CanSeeJourney(v, isSelf, friends));
    }
}
