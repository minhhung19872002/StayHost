namespace StayHost.Domain;

/// <summary>
/// docs/01 TĐ-22 — what kind of place the host is pointing a guest at.
///
/// The list is short on purpose: a guest scanning a guidebook wants four or five
/// headings, not a taxonomy. Anything that does not fit is <see cref="Tip"/>.
/// </summary>
public enum GuidebookCategory
{
    Food = 0,
    Cafe = 1,
    Sightseeing = 2,
    Shopping = 3,
    Nature = 4,
    Nightlife = 5,
    Transport = 6,
    /// <summary>Advice with no address — "đổi tiền ở ngân hàng, đừng đổi ở sân bay".</summary>
    Tip = 7
}

/// <summary>
/// docs/01 TĐ-22 — one entry in a host's local guidebook.
///
/// This is the host's own recommendation, not platform data: it is written by a
/// person, so it goes through <c>TranslatedText</c> on the way to a guest who
/// reads another language rather than into the interface dictionary.
/// </summary>
public class GuidebookPlace
{
    public int Id { get; set; }

    public int ListingId { get; set; }
    public Listing? Listing { get; set; }

    public GuidebookCategory Category { get; set; } = GuidebookCategory.Food;

    /// <summary>The name a guest would type into a map — "Bánh mì Phượng".</summary>
    public string Name { get; set; } = "";

    /// <summary>Why the host sends people there. This is the whole point of the feature.</summary>
    public string? Note { get; set; }

    public string? Address { get; set; }

    /// <summary>
    /// Both set or both null. A pin with only one half of a coordinate would
    /// land in the sea off Africa, so <see cref="Guidebooks.HasPin"/> is the
    /// only thing allowed to decide whether an entry is mappable.
    /// </summary>
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// docs/01 TĐ-22 — the rules of a guidebook that need no database.
/// </summary>
public static class Guidebooks
{
    public const int NameMax = 160;
    public const int NoteMax = 600;
    public const int AddressMax = 240;

    /// <summary>
    /// How many entries one listing may carry. A guidebook is a shortlist; past
    /// this it stops being one, and the listing page stops being readable.
    /// </summary>
    public const int MaxPerListing = 30;

    /// <summary>Vietnamese headings for the guest-facing groups.</summary>
    public static string Label(GuidebookCategory c) => c switch
    {
        GuidebookCategory.Food => "Quán ăn",
        GuidebookCategory.Cafe => "Cà phê",
        GuidebookCategory.Sightseeing => "Tham quan",
        GuidebookCategory.Shopping => "Mua sắm",
        GuidebookCategory.Nature => "Thiên nhiên",
        GuidebookCategory.Nightlife => "Về đêm",
        GuidebookCategory.Transport => "Đi lại",
        _ => "Lời khuyên"
    };

    /// <summary>
    /// The order the groups appear in on the listing page. Not the enum order:
    /// food and coffee are what guests ask about first, and untethered advice
    /// belongs at the end.
    /// </summary>
    public static readonly GuidebookCategory[] DisplayOrder =
    [
        GuidebookCategory.Food,
        GuidebookCategory.Cafe,
        GuidebookCategory.Sightseeing,
        GuidebookCategory.Nature,
        GuidebookCategory.Shopping,
        GuidebookCategory.Nightlife,
        GuidebookCategory.Transport,
        GuidebookCategory.Tip
    ];

    /// <summary>
    /// An entry is mappable only when it carries a whole coordinate. Callers must
    /// ask this rather than testing one half; see <see cref="GuidebookPlace.Latitude"/>.
    /// </summary>
    public static bool HasPin(double? latitude, double? longitude) =>
        latitude is { } lat && longitude is { } lng
        && lat is >= -90 and <= 90 && lng is >= -180 and <= 180
        // 0,0 is Null Island: what an empty form posts, never a real recommendation.
        && (lat != 0 || lng != 0);

    /// <summary>Null when the entry is fine, otherwise the message shown to the host.</summary>
    public static string? Validate(string? name, string? note, string? address)
    {
        var trimmed = (name ?? "").Trim();
        if (trimmed.Length < 2) return "Tên địa điểm cần tối thiểu 2 ký tự.";
        if (trimmed.Length > NameMax) return $"Tên địa điểm tối đa {NameMax} ký tự.";
        if ((note ?? "").Trim().Length > NoteMax) return $"Ghi chú tối đa {NoteMax} ký tự.";
        if ((address ?? "").Trim().Length > AddressMax) return $"Địa chỉ tối đa {AddressMax} ký tự.";
        return null;
    }

    /// <summary>Null when there is room for one more, otherwise why there is not.</summary>
    public static string? ValidateCount(int existing) =>
        existing >= MaxPerListing
            ? $"Mỗi chỗ nghỉ chỉ giữ tối đa {MaxPerListing} địa điểm trong cẩm nang."
            : null;

    /// <summary>
    /// Straight-line kilometres between the listing and one entry, for the
    /// "cách 1,2 km" line. Same haversine as <see cref="Landmarks"/> uses; a
    /// guidebook entry across town should read as far away, and 400 metres of
    /// projection error over a walkable distance would not change that.
    /// </summary>
    public static double? DistanceKm(
        double listingLat, double listingLng, double? placeLat, double? placeLng)
    {
        if (!HasPin(placeLat, placeLng)) return null;

        const double earthKm = 6371;
        var dLat = (placeLat!.Value - listingLat) * Math.PI / 180;
        var dLng = (placeLng!.Value - listingLng) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(listingLat * Math.PI / 180) * Math.Cos(placeLat.Value * Math.PI / 180)
                  * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return earthKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
