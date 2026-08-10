namespace StayHost.Domain.Tests;

/// <summary>docs/01 CĐ-10, CĐ-11 — trip plan permissions and validation.</summary>
public class TripPlanTests
{
    [Fact]
    public void The_owner_and_members_may_edit_nobody_else()
    {
        int[] members = [7, 9];
        Assert.True(TripPlans.CanEdit(3, members, 3));   // owner
        Assert.True(TripPlans.CanEdit(3, members, 7));   // a companion
        Assert.False(TripPlans.CanEdit(3, members, 15)); // a stranger
    }

    [Fact]
    public void Only_the_owner_manages_membership_and_bookings()
    {
        Assert.True(TripPlans.IsOwner(3, 3));
        Assert.False(TripPlans.IsOwner(3, 7));
    }

    [Fact]
    public void An_item_needs_a_real_title()
    {
        Assert.NotNull(TripPlans.ValidateItem(" "));
        Assert.NotNull(TripPlans.ValidateItem("x"));
        Assert.Null(TripPlans.ValidateItem("Bãi biển Mỹ Khê"));
    }
}
