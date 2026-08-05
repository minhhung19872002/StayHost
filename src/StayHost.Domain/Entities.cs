namespace StayHost.Domain;

/// <summary>Kind of place, mirrors the category strip on the browse page.</summary>
public enum PlaceType
{
    Villa = 1,
    Apartment = 2,
    Homestay = 3,
    House = 4,
    Cabin = 5,
    Boutique = 6
}

/// <summary>Whether the guest books the whole place or shares it.</summary>
public enum RoomType
{
    EntirePlace = 1,
    PrivateRoom = 2,
    SharedRoom = 3
}

public enum BookingStatus
{
    Pending = 0,
    Confirmed = 1,
    Cancelled = 2
}

/// <summary>Public-facing host identity. The login account itself is <see cref="User"/>.</summary>
public class HostProfile
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Initials { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public bool IsSuperhost { get; set; }
    public int YearsHosting { get; set; }
    public string? Bio { get; set; }
    public string ResponseRate { get; set; } = "100%";
    public string ResponseTime { get; set; } = "trong vòng 1 giờ";
    public DateTime JoinedAt { get; set; } = new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Null for the seeded demo hosts; set for every host created from a real account.</summary>
    public int? UserId { get; set; }
    public User? User { get; set; }

    public List<Listing> Listings { get; set; } = [];
}

public class Amenity
{
    public int Id { get; set; }
    /// <summary>Stable slug used by the filter query string, e.g. "pool".</summary>
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Group { get; set; } = "Tiện nghi";
    /// <summary>Shown in the filter panel when true; every amenity still renders on the detail page.</summary>
    public bool IsFilterable { get; set; }
    public int SortOrder { get; set; }

    public List<ListingAmenity> Listings { get; set; } = [];
}

public class Listing
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string City { get; set; } = "";
    public string Country { get; set; } = "Việt Nam";
    public PlaceType Type { get; set; }
    public RoomType RoomType { get; set; } = RoomType.EntirePlace;

    public int Bedrooms { get; set; }
    public int Beds { get; set; }
    public int Bathrooms { get; set; }
    public int MaxGuests { get; set; }

    public decimal PricePerNight { get; set; }
    /// <summary>0–60. When set, the card shows the pre-discount price struck through.</summary>
    public int DiscountPercent { get; set; }
    public decimal CleaningFee { get; set; } = 350_000m;
    /// <summary>Fraction of the nightly subtotal charged as the StayHost service fee.</summary>
    public decimal ServiceFeeRate { get; set; } = 0.09m;

    public double Rating { get; set; }
    public int ReviewCount { get; set; }
    public bool IsSuperhost { get; set; }
    public bool IsGuestFavorite { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public string Description { get; set; } = "";
    public string? SpaceHighlight { get; set; }
    public string CancellationPolicy { get; set; } = "Huỷ miễn phí trước 48 giờ. Sau đó hoàn 50% tiền phòng.";
    public string HouseRules { get; set; } = "Nhận phòng sau 14:00|Trả phòng trước 12:00|Không hút thuốc trong nhà|Không tổ chức tiệc";
    public string SafetyInfo { get; set; } = "Có thiết bị báo khói|Có bình chữa cháy|Có bộ sơ cứu";

    /// <summary>Drafts are visible only to their host until published.</summary>
    public bool IsPublished { get; set; } = true;
    public bool InstantBook { get; set; } = true;
    public int MinNights { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public int HostId { get; set; }
    public HostProfile? Host { get; set; }

    public List<ListingImage> Images { get; set; } = [];
    public List<CalendarBlock> Blocks { get; set; } = [];
    public List<ListingAmenity> Amenities { get; set; } = [];
    public List<Review> Reviews { get; set; } = [];
}

public class ListingImage
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public Listing? Listing { get; set; }
    public string Url { get; set; } = "";
    public string Caption { get; set; } = "";
    public int SortOrder { get; set; }
}

public class ListingAmenity
{
    public int ListingId { get; set; }
    public Listing? Listing { get; set; }
    public int AmenityId { get; set; }
    public Amenity? Amenity { get; set; }
}

public class Review
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public Listing? Listing { get; set; }

    /// <summary>Set when a real guest reviewed a completed stay; null for seeded reviews.</summary>
    public int? AuthorUserId { get; set; }
    public User? AuthorUser { get; set; }
    public int? BookingId { get; set; }
    public Booking? Booking { get; set; }

    public string AuthorName { get; set; } = "";
    public string AuthorInitials { get; set; } = "";
    public string? AuthorLocation { get; set; }
    public string When { get; set; } = "";
    public string Text { get; set; } = "";
    public double Rating { get; set; } = 5;

    // Airbnb-style per-category scores, averaged into the ratings breakdown.
    public double Cleanliness { get; set; } = 5;
    public double Accuracy { get; set; } = 5;
    public double CheckIn { get; set; } = 5;
    public double Communication { get; set; } = 5;
    public double Location { get; set; } = 5;
    public double Value { get; set; } = 5;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Wishlist entry keyed by an anonymous browser session id.</summary>
public class Favorite
{
    public int Id { get; set; }
    public string SessionId { get; set; } = "";
    /// <summary>Set once the visitor signs in; the wishlist then follows the account.</summary>
    public int? UserId { get; set; }
    public User? User { get; set; }
    public int ListingId { get; set; }
    public Listing? Listing { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Booking
{
    public int Id { get; set; }
    public string Reference { get; set; } = "";
    public string SessionId { get; set; } = "";
    public int? GuestUserId { get; set; }
    public User? GuestUser { get; set; }

    public int ListingId { get; set; }
    public Listing? Listing { get; set; }

    public DateOnly CheckIn { get; set; }
    public DateOnly CheckOut { get; set; }
    public int Guests { get; set; }
    public int Nights { get; set; }

    public decimal Subtotal { get; set; }
    public decimal CleaningFee { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal Total { get; set; }

    public string? GuestName { get; set; }
    public string? GuestEmail { get; set; }
    public string? GuestNote { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
    public string? CancellationReason { get; set; }

    public Payment? Payment { get; set; }
    /// <summary>Guarded so a stay can only be reviewed once.</summary>
    public bool HasReview { get; set; }
}
