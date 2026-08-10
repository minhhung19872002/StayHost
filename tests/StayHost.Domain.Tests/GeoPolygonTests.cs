namespace StayHost.Domain.Tests;

/// <summary>docs/01 TM-24 — point-in-polygon for a hand-drawn search area.</summary>
public class GeoPolygonTests
{
    // A unit square from (0,0) to (10,10).
    private static readonly GeoPolygon.Point[] Square =
    [
        new(0, 0), new(0, 10), new(10, 10), new(10, 0)
    ];

    [Fact]
    public void A_point_inside_the_square_is_contained()
    {
        Assert.True(GeoPolygon.Contains(Square, 5, 5));
    }

    [Fact]
    public void A_point_outside_the_square_is_not()
    {
        Assert.False(GeoPolygon.Contains(Square, 15, 5));
        Assert.False(GeoPolygon.Contains(Square, 5, -1));
    }

    [Fact]
    public void Fewer_than_three_points_contains_nothing()
    {
        Assert.False(GeoPolygon.Contains([new(0, 0), new(10, 10)], 5, 5));
    }

    [Fact]
    public void A_concave_polygon_excludes_the_notch()
    {
        // An L-shape: the top-right quadrant is cut out.
        GeoPolygon.Point[] ell =
        [
            new(0, 0), new(0, 10), new(5, 10), new(5, 5), new(10, 5), new(10, 0)
        ];
        Assert.True(GeoPolygon.Contains(ell, 2, 2));    // in the stem
        Assert.False(GeoPolygon.Contains(ell, 8, 8));   // in the cut-out notch
    }

    [Fact]
    public void Bounds_wrap_the_polygon()
    {
        var (s, w, n, e) = GeoPolygon.Bounds(Square);
        Assert.Equal(0, s); Assert.Equal(0, w); Assert.Equal(10, n); Assert.Equal(10, e);
    }

    [Fact]
    public void Parse_reads_pairs_and_skips_junk()
    {
        var pts = GeoPolygon.Parse("16.05,108.2; 16.06,108.21; bad; 16.07,108.22");
        Assert.Equal(3, pts.Count);
        Assert.Equal(16.05, pts[0].Lat, 3);
        Assert.Equal(108.22, pts[2].Lng, 3);
    }
}
