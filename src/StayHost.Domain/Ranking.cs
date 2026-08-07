namespace StayHost.Domain;

/// <summary>
/// docs/03 §6 — the score that orders search results.
///
/// The spec gives seven weighted factors, a set of penalties and a
/// diversification rule. Before this, the default sort was
/// "guest favourite, then rating", which meant a single host could hold every
/// slot on the first page of a city.
///
/// Every factor is normalised to 0–1 before it is weighted, so the weights in
/// the table below are the only thing that decides how much each one matters.
/// </summary>
public static class Ranking
{
    /* -------------------------------------------------- the weights of §6 */

    public const double NearWeight = 0.30;
    public const double QualityWeight = 0.25;
    public const double ConversionWeight = 0.15;
    public const double PriceWeight = 0.10;
    public const double ServiceWeight = 0.10;
    public const double PhotoWeight = 0.05;
    public const double FreshWeight = 0.05;

    /// <summary>They are shares of one whole; if they stop adding up, the table is wrong.</summary>
    public static double TotalWeight =>
        NearWeight + QualityWeight + ConversionWeight + PriceWeight + ServiceWeight + PhotoWeight + FreshWeight;

    /* ------------------------------------------------------- the penalties */

    /// <summary>docs/03 §6 — "điểm đánh giá dưới 4.0".</summary>
    public const double PoorRating = 4.0;
    public const double PoorRatingPenalty = 0.25;

    /// <summary>"tỉ lệ tự huỷ cao" — read as the Superhost bar times five.</summary>
    public const double HighCancelRate = 5;
    public const double HighCancelPenalty = 0.20;

    /// <summary>docs/01 CN-07 makes five photos the floor; below it is "thiếu ảnh".</summary>
    public const int MinPhotos = 5;
    public const double FewPhotosPenalty = 0.10;

    /// <summary>"thông tin không đầy đủ" — the listing never finished its wizard.</summary>
    public const double IncompletePenalty = 0.15;

    /* ---------------------------------------------------- diversification */

    /// <summary>docs/03 §6 — "trong 12 kết quả đầu, không quá 2 chỗ của cùng một chủ nhà".</summary>
    public const int DiverseWindow = 12;
    public const int DiverseMaxPerHost = 2;

    /* ------------------------------------------------------------- inputs */

    /// <summary>
    /// Everything §6 scores one listing on. Assembled by the search; this class
    /// does no counting of its own, so the rule stays readable and testable.
    /// </summary>
    public readonly record struct Candidate(
        int Id,
        int HostId,
        /// <summary>From the centre of the area being searched.</summary>
        double DistanceKm,
        /// <summary>How far out still counts as "in the area"; beyond it scores zero.</summary>
        double RadiusKm,
        double Rating,
        int ReviewCount,
        /// <summary>Detail-page views in the recent window.</summary>
        int Views,
        /// <summary>Bookings made in the same window.</summary>
        int Bookings,
        decimal Price,
        /// <summary>Median nightly rate of comparable places in the same area.</summary>
        decimal MedianPrice,
        double ResponseRate,
        bool InstantBook,
        int PhotoCount,
        int DaysSincePublished,
        double HostCancelRate,
        bool IsComplete);

    /* ------------------------------------------------------- each factor */

    private static double Clamp01(double v) => Math.Clamp(v, 0, 1);

    /// <summary>Right on the centre scores 1; at the edge of the area, 0.</summary>
    public static double Nearness(double distanceKm, double radiusKm) =>
        radiusKm <= 0 ? 1 : Clamp01(1 - distanceKm / radiusKm);

    /// <summary>
    /// The score, pulled toward the middle by how few reviews back it up: five
    /// fives should not outrank a hundred reviews averaging 4.8. Ten reviews is
    /// the point at which a listing's own average counts for half.
    /// </summary>
    public const int QualityConfidence = 10;
    public const double QualityBaseline = 4.5;

    public static double Quality(double rating, int reviewCount)
    {
        if (reviewCount <= 0) return Clamp01((QualityBaseline - 3) / 2);

        var weighted = (reviewCount * rating + QualityConfidence * QualityBaseline)
                       / (reviewCount + QualityConfidence);

        // 3 stars and below is the floor, 5 is the ceiling: the band that matters.
        return Clamp01((weighted - 3) / 2);
    }

    /// <summary>
    /// docs/03 §6 — "chỗ được nhiều người chốt thì đẩy lên". One booking in five
    /// views is treated as the top of the scale; nobody converts better than that.
    /// </summary>
    public const double TopConversion = 0.2;

    public static double Conversion(int views, int bookings)
    {
        if (views <= 0) return 0;
        return Clamp01(bookings / (double)views / TopConversion);
    }

    /// <summary>
    /// Against the median of comparable places nearby. At the median a listing
    /// scores half; at twice the median, nothing; below half the median it stops
    /// earning more, because cheap past a point signals something else.
    /// </summary>
    public static double PriceFit(decimal price, decimal medianPrice)
    {
        if (medianPrice <= 0) return 0.5;
        var ratio = (double)(price / medianPrice);
        return Clamp01(1.5 - ratio);
    }

    /// <summary>docs/03 §6 — "tỉ lệ phản hồi + có bật đặt ngay".</summary>
    public static double Service(double responseRate, bool instantBook) =>
        0.7 * Clamp01(responseRate / 100) + 0.3 * (instantBook ? 1 : 0);

    /// <summary>Ten photos is a full set; the spec asks for at least five.</summary>
    public const int FullPhotoSet = 10;

    public static double Photos(int count) => Clamp01(count / (double)FullPhotoSet);

    /// <summary>docs/03 §6 — "ưu đãi hiển thị trong 30 ngày đầu", fading across them.</summary>
    public const int FreshDays = 30;

    public static double Freshness(int daysSincePublished) =>
        daysSincePublished < 0 ? 1 : Clamp01(1 - daysSincePublished / (double)FreshDays);

    /* -------------------------------------------------------- the penalty */

    public static double Penalty(Candidate c)
    {
        var penalty = 0.0;
        if (c.ReviewCount > 0 && c.Rating < PoorRating) penalty += PoorRatingPenalty;
        if (c.HostCancelRate > HighCancelRate) penalty += HighCancelPenalty;
        if (c.PhotoCount < MinPhotos) penalty += FewPhotosPenalty;
        if (!c.IsComplete) penalty += IncompletePenalty;
        return penalty;
    }

    /* --------------------------------------------------------- the score */

    /// <summary>
    /// docs/03 §6, the whole table. Never negative: an order is what this
    /// produces, and a listing that survived the filters is still a result.
    /// </summary>
    public static double Score(Candidate c)
    {
        var score =
            NearWeight * Nearness(c.DistanceKm, c.RadiusKm)
            + QualityWeight * Quality(c.Rating, c.ReviewCount)
            + ConversionWeight * Conversion(c.Views, c.Bookings)
            + PriceWeight * PriceFit(c.Price, c.MedianPrice)
            + ServiceWeight * Service(c.ResponseRate, c.InstantBook)
            + PhotoWeight * Photos(c.PhotoCount)
            + FreshWeight * Freshness(c.DaysSincePublished);

        return Math.Max(0, score - Penalty(c));
    }

    /* --------------------------------------------------- diversification */

    /// <summary>
    /// docs/03 §6 — no more than two listings from one host in the first twelve.
    /// A third is pushed past the window rather than dropped: it is still a
    /// result somebody filtered for, just not one that gets to crowd page one.
    /// </summary>
    public static List<T> Diversify<T>(
        IReadOnlyList<T> ordered,
        Func<T, int> hostOf,
        int window = DiverseWindow,
        int maxPerHost = DiverseMaxPerHost)
    {
        if (ordered.Count <= maxPerHost) return [.. ordered];

        var head = new List<T>(Math.Min(window, ordered.Count));
        var pushed = new List<T>();
        var seen = new Dictionary<int, int>();

        foreach (var item in ordered)
        {
            if (head.Count == window) { pushed.Add(item); continue; }

            var host = hostOf(item);
            var used = seen.GetValueOrDefault(host);

            if (used >= maxPerHost) { pushed.Add(item); continue; }

            seen[host] = used + 1;
            head.Add(item);
        }

        // Whatever was held back keeps its own order behind the window.
        head.AddRange(pushed);
        return head;
    }

    /// <summary>Great-circle distance in kilometres, near enough for ordering.</summary>
    public static double DistanceKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double earthRadiusKm = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLng = (lng2 - lng1) * Math.PI / 180;

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
                * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);

        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
