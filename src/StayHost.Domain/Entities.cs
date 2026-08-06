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
    Cancelled = 2,
    Completed = 3
}

/// <summary>
/// The six policies of docs/03 §4. <see cref="Cancellation"/> owns the refund
/// maths; this enum only names the choice a host makes per listing.
/// </summary>
public enum CancellationTier
{
    /// <summary>Full refund up to 24h before check-in, then the first night is lost.</summary>
    Flexible = 0,
    /// <summary>Full refund up to 5 days before check-in, then 50% of the room rate.</summary>
    Moderate = 1,
    /// <summary>100% before 30 days, 50% before 7 days, nothing after.</summary>
    Strict = 2,
    /// <summary>50% up to 7 days before; nothing after.</summary>
    SuperStrict = 3,
    /// <summary>No room refund at any point, in exchange for a lower price.</summary>
    NonRefundable = 4,
    /// <summary>For stays of 28 nights or more: after 30 days out, the guest pays the first 30 nights.</summary>
    LongTermStrict = 5
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
    /// <summary>Uplift applied to Friday and Saturday nights (docs/03 §1 step 1, weekend tier).</summary>
    public decimal WeekendSurchargeRate { get; set; } = 0.15m;

    // --- docs/03 §1 step 2: length-of-stay discounts. One tier applies, longer wins.
    public int WeeklyDiscountPercent { get; set; }
    public int MonthlyDiscountPercent { get; set; }

    // --- step 3: booking-time discounts. One applies, whichever is larger.
    /// <summary>Book at least this many days ahead to earn <see cref="EarlyBirdPercent"/>.</summary>
    public int EarlyBirdDays { get; set; }
    public int EarlyBirdPercent { get; set; }
    /// <summary>Book within this many days of check-in to earn <see cref="LastMinutePercent"/>.</summary>
    public int LastMinuteDays { get; set; }
    public int LastMinutePercent { get; set; }

    // --- step 5: surcharges.
    /// <summary>Guests included in the nightly rate; infants never count (docs/03 §1 step 5).</summary>
    public int FreeGuestThreshold { get; set; } = 2;
    /// <summary>Charged per extra guest, per night.</summary>
    public decimal ExtraGuestFee { get; set; }

    public bool PetsAllowed { get; set; }
    public int MaxPets { get; set; } = 2;
    public decimal PetFee { get; set; }
    /// <summary>False charges the pet fee once for the stay, true charges it nightly.</summary>
    public bool PetFeePerNight { get; set; }

    public double Rating { get; set; }
    public int ReviewCount { get; set; }
    public bool IsSuperhost { get; set; }
    public bool IsGuestFavorite { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public string Description { get; set; } = "";
    public string? SpaceHighlight { get; set; }
    public CancellationTier CancellationTier { get; set; } = CancellationTier.Moderate;
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
    public List<PriceRule> PriceRules { get; set; } = [];
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

/// <summary>A named collection of saved listings, e.g. "Chuyến đi Đà Nẵng".</summary>
public class Wishlist
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string SessionId { get; set; } = "";
    public int? UserId { get; set; }
    public User? User { get; set; }
    /// <summary>The list new saves land in when the guest does not pick one.</summary>
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Favorite> Items { get; set; } = [];
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
    public int? WishlistId { get; set; }
    public Wishlist? Wishlist { get; set; }
    public string? Note { get; set; }
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
    /// <summary>Adults plus children — what capacity and surcharges were priced on.</summary>
    public int Guests { get; set; }
    public int Adults { get; set; } = 1;
    public int Children { get; set; }
    public int Infants { get; set; }
    public int Pets { get; set; }
    public int Nights { get; set; }

    // The priced stay, frozen at booking time. docs/00 §6.2: a receipt must still
    // add up years later, even after the host has changed their rates.
    public decimal RoomBeforeDiscount { get; set; }
    public decimal RoomDiscount { get; set; }
    public int DiscountPercent { get; set; }
    public decimal ExtraGuestFee { get; set; }
    public decimal PetFee { get; set; }
    public decimal CleaningFee { get; set; }
    /// <summary>Room after discount plus every surcharge — the base of both service fees.</summary>
    public decimal Subtotal { get; set; }
    /// <summary>The guest service fee (docs/03 §1 step 7).</summary>
    public decimal ServiceFee { get; set; }
    public decimal Tax { get; set; }
    public decimal Promotion { get; set; }
    public decimal Total { get; set; }

    public decimal HostServiceFee { get; set; }
    public decimal HostPayout { get; set; }

    /// <summary>The displayed rows, as JSON, so a receipt renders exactly as quoted.</summary>
    public string PriceLinesJson { get; set; } = "[]";

    public decimal RefundedAmount { get; set; }
    /// <summary>Promotional balance granted on cancellation, e.g. when the host walked away.</summary>
    public decimal GoodwillCredit { get; set; }
    public CancellationTier CancellationTier { get; set; } = CancellationTier.Moderate;

    public string? GuestName { get; set; }
    public string? GuestEmail { get; set; }
    public string? GuestNote { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
    public string? CancellationReason { get; set; }
    public CancelledBy? CancelledBy { get; set; }

    public Payment? Payment { get; set; }
    public List<LedgerEntry> LedgerEntries { get; set; } = [];
    /// <summary>Guarded so a stay can only be reviewed once.</summary>
    public bool HasReview { get; set; }
}
