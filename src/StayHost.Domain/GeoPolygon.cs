namespace StayHost.Domain;

/// <summary>
/// docs/01 TM-24 — deciding whether a listing falls inside an area the guest drew
/// on the map. A hand-drawn shape is not a rectangle, so the bounding box the map
/// already filters by is not enough; this is the exact point-in-polygon test that
/// runs over the box's survivors.
/// </summary>
public static class GeoPolygon
{
    public readonly record struct Point(double Lat, double Lng);

    /// <summary>
    /// Ray-casting: a point is inside when a ray to the east crosses the polygon's
    /// edges an odd number of times. Works for any simple polygon, convex or not.
    /// A polygon of fewer than three points contains nothing.
    /// </summary>
    public static bool Contains(IReadOnlyList<Point> polygon, double lat, double lng)
    {
        if (polygon.Count < 3) return false;

        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var (yi, xi) = (polygon[i].Lat, polygon[i].Lng);
            var (yj, xj) = (polygon[j].Lat, polygon[j].Lng);

            var crosses = (yi > lat) != (yj > lat)
                && lng < (xj - xi) * (lat - yi) / (yj - yi) + xi;
            if (crosses) inside = !inside;
        }
        return inside;
    }

    /// <summary>The bounding box of a polygon, for a cheap SQL pre-filter.</summary>
    public static (double South, double West, double North, double East) Bounds(IReadOnlyList<Point> polygon)
    {
        var south = double.MaxValue; var west = double.MaxValue;
        var north = double.MinValue; var east = double.MinValue;
        foreach (var p in polygon)
        {
            if (p.Lat < south) south = p.Lat;
            if (p.Lat > north) north = p.Lat;
            if (p.Lng < west) west = p.Lng;
            if (p.Lng > east) east = p.Lng;
        }
        return (south, west, north, east);
    }

    /// <summary>
    /// Parse "lat,lng;lat,lng;…" as the client sends it. Bad points are skipped so
    /// one malformed pair does not throw away the whole shape.
    /// </summary>
    public static IReadOnlyList<Point> Parse(string? encoded)
    {
        var points = new List<Point>();
        foreach (var pair in (encoded ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split(',');
            if (parts.Length == 2
                && double.TryParse(parts[0], System.Globalization.CultureInfo.InvariantCulture, out var lat)
                && double.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out var lng))
                points.Add(new Point(lat, lng));
        }
        return points;
    }
}
