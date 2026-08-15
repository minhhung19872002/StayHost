namespace StayHost.Domain.Tests;

/// <summary>docs/01 TĐ-22 — the host's local guidebook.</summary>
public class GuidebooksTests
{
    [Fact]
    public void A_name_of_one_character_is_not_a_place()
    {
        Assert.NotNull(Guidebooks.Validate("A", null, null));
        Assert.NotNull(Guidebooks.Validate("  ", null, null));
        Assert.Null(Guidebooks.Validate("Phở Bát Đàn", null, null));
    }

    [Fact]
    public void Surrounding_whitespace_does_not_make_a_short_name_long_enough()
    {
        Assert.NotNull(Guidebooks.Validate("   x   ", null, null));
    }

    [Fact]
    public void Each_field_has_its_own_ceiling()
    {
        Assert.NotNull(Guidebooks.Validate(new string('a', Guidebooks.NameMax + 1), null, null));
        Assert.NotNull(Guidebooks.Validate("Quán ngon", new string('a', Guidebooks.NoteMax + 1), null));
        Assert.NotNull(Guidebooks.Validate("Quán ngon", null, new string('a', Guidebooks.AddressMax + 1)));

        Assert.Null(Guidebooks.Validate(
            new string('a', Guidebooks.NameMax),
            new string('a', Guidebooks.NoteMax),
            new string('a', Guidebooks.AddressMax)));
    }

    [Fact]
    public void A_guidebook_stops_being_a_shortlist_past_the_cap()
    {
        Assert.Null(Guidebooks.ValidateCount(Guidebooks.MaxPerListing - 1));
        Assert.NotNull(Guidebooks.ValidateCount(Guidebooks.MaxPerListing));
    }

    [Fact]
    public void Half_a_coordinate_is_no_coordinate()
    {
        Assert.False(Guidebooks.HasPin(16.05, null));
        Assert.False(Guidebooks.HasPin(null, 108.22));
        Assert.False(Guidebooks.HasPin(null, null));
        Assert.True(Guidebooks.HasPin(16.05, 108.22));
    }

    [Fact]
    public void Null_island_is_what_an_empty_form_posts_not_a_recommendation()
    {
        Assert.False(Guidebooks.HasPin(0, 0));
        // A real place on one axis of zero is still a real place.
        Assert.True(Guidebooks.HasPin(0, 108.22));
    }

    [Fact]
    public void A_coordinate_outside_the_globe_is_refused()
    {
        Assert.False(Guidebooks.HasPin(91, 108.22));
        Assert.False(Guidebooks.HasPin(16.05, 181));
    }

    [Fact]
    public void Distance_is_null_without_a_whole_pin()
    {
        Assert.Null(Guidebooks.DistanceKm(16.05, 108.22, 16.06, null));
        Assert.Null(Guidebooks.DistanceKm(16.05, 108.22, null, null));
    }

    [Fact]
    public void Distance_matches_the_ground()
    {
        // Mỹ Khê beach to the Dragon Bridge in Đà Nẵng — about 2.1 km apart.
        var km = Guidebooks.DistanceKm(16.0605, 108.2470, 16.0614, 108.2270);
        Assert.NotNull(km);
        Assert.InRange(km!.Value, 1.8, 2.5);
    }

    [Fact]
    public void The_same_point_is_zero_away_from_itself()
    {
        Assert.Equal(0, Guidebooks.DistanceKm(16.05, 108.22, 16.05, 108.22)!.Value, 6);
    }

    [Fact]
    public void Every_category_has_a_heading_and_a_place_in_the_order()
    {
        var all = Enum.GetValues<GuidebookCategory>();

        Assert.Equal(all.Length, Guidebooks.DisplayOrder.Length);
        Assert.Equal(all.Length, Guidebooks.DisplayOrder.Distinct().Count());

        foreach (var c in all)
        {
            Assert.Contains(c, Guidebooks.DisplayOrder);
            Assert.False(string.IsNullOrWhiteSpace(Guidebooks.Label(c)));
        }
    }

    [Fact]
    public void Food_and_coffee_come_before_the_untethered_advice()
    {
        var order = Guidebooks.DisplayOrder.ToList();

        Assert.True(order.IndexOf(GuidebookCategory.Food) < order.IndexOf(GuidebookCategory.Tip));
        Assert.True(order.IndexOf(GuidebookCategory.Cafe) < order.IndexOf(GuidebookCategory.Tip));
        Assert.Equal(GuidebookCategory.Tip, order[^1]);
    }
}
