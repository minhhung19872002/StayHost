namespace StayHost.Web.Contracts;

/* ------------------------------------------------------------------ account */

public record RegisterRequest(string Email, string Password, string FullName, string? Phone);

public record LoginRequest(string Email, string Password);

public record UpdateProfileRequest(string? FullName, string? Phone, string? Bio);

public record CurrentUserDto(
    int Id,
    string Email,
    string FullName,
    string Initials,
    string? Phone,
    string? Bio,
    string Role,
    bool IsHost,
    int? HostId,
    int ListingCount,
    int UnreadMessages,
    string JoinedLabel);

/* ------------------------------------------------------------------ hosting */

public record HostListingDto(
    int Id,
    string Slug,
    string Title,
    string City,
    string TypeKey,
    string RoomTypeKey,
    int Bedrooms,
    int Beds,
    int Bathrooms,
    int MaxGuests,
    decimal PricePerNight,
    decimal CleaningFee,
    int MinNights,
    bool InstantBook,
    bool IsPublished,
    double Rating,
    int ReviewCount,
    string Description,
    string? Highlight,
    double Latitude,
    double Longitude,
    IReadOnlyList<string> Images,
    IReadOnlyList<string> AmenityKeys,
    int UpcomingBookings,
    decimal EarningsToDate);

public record SaveListingRequest(
    string Title,
    string City,
    string TypeKey,
    string RoomTypeKey,
    int Bedrooms,
    int Beds,
    int Bathrooms,
    int MaxGuests,
    decimal PricePerNight,
    decimal CleaningFee,
    int MinNights,
    bool InstantBook,
    bool IsPublished,
    string Description,
    string? Highlight,
    double? Latitude,
    double? Longitude,
    IReadOnlyList<string> Images,
    IReadOnlyList<string> AmenityKeys);

public record CalendarBlockDto(int Id, DateOnly From, DateOnly To, string? Note);

public record CreateBlockRequest(int ListingId, DateOnly From, DateOnly To, string? Note);

public record HostBookingDto(
    int Id,
    string Reference,
    int ListingId,
    string ListingTitle,
    string GuestName,
    string? GuestEmail,
    string? GuestNote,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Nights,
    int Guests,
    decimal Total,
    decimal HostPayout,
    string Status,
    string PaymentStatus,
    DateTime CreatedAt);

public record HostDashboardDto(
    int ListingCount,
    int PublishedCount,
    int UpcomingBookings,
    decimal EarningsToDate,
    decimal EarningsUpcoming,
    double AverageRating,
    int TotalReviews,
    int UnreadMessages,
    IReadOnlyList<HostListingDto> Listings,
    IReadOnlyList<HostBookingDto> Bookings,
    IReadOnlyList<MonthlyEarningDto> EarningsByMonth);

public record MonthlyEarningDto(string Month, decimal Amount, int Nights);

/* ----------------------------------------------------------------- messages */

public record ThreadSummaryDto(
    int Id,
    int ListingId,
    string ListingTitle,
    string ListingImage,
    string CounterpartName,
    string CounterpartInitials,
    bool ViewerIsHost,
    string? LastMessage,
    DateTime LastMessageAt,
    int UnreadCount);

public record MessageDto(int Id, int SenderUserId, string SenderName, string Body, DateTime SentAt, bool Mine);

public record ThreadDetailDto(ThreadSummaryDto Summary, IReadOnlyList<MessageDto> Messages);

public record SendMessageRequest(int? ThreadId, int? ListingId, string Body);

/* ------------------------------------------------------------- guest review */

public record SubmitReviewRequest(
    int BookingId,
    double Rating,
    string Text,
    double Cleanliness,
    double Accuracy,
    double CheckIn,
    double Communication,
    double Location,
    double Value);

public record CategoryDto(string Key, string Label, string Icon, int Count);

public record AmenityDto(string Key, string Label, string Icon, string Group);

public record MetaDto(
    IReadOnlyList<CategoryDto> Categories,
    IReadOnlyList<AmenityDto> Amenities,
    IReadOnlyList<AmenityDto> QuickAmenities,
    IReadOnlyList<string> Cities,
    IReadOnlyList<RoomTypeDto> RoomTypes,
    decimal MinPrice,
    decimal MaxPrice,
    IReadOnlyList<int> PriceHistogram,
    IReadOnlyList<CurrencyDto> Currencies,
    IReadOnlyList<LanguageDto> Languages);

public record RoomTypeDto(string Key, string Label, string Hint);

public record CurrencyDto(string Code, string Label, string Symbol, decimal RateFromVnd);

public record LanguageDto(string Code, string Label, string Region);

public record ListingCardDto(
    int Id,
    string Slug,
    string Title,
    string City,
    string Country,
    string TypeKey,
    string TypeLabel,
    string RoomTypeLabel,
    int Bedrooms,
    int Beds,
    int Bathrooms,
    int MaxGuests,
    decimal PricePerNight,
    double Rating,
    int ReviewCount,
    bool IsSuperhost,
    bool IsGuestFavorite,
    double Latitude,
    double Longitude,
    string? Highlight,
    IReadOnlyList<string> Images,
    IReadOnlyList<string> AmenityKeys,
    bool IsFavorite);

public record HomeSectionDto(
    string Key,
    string Title,
    string? Subtitle,
    string Href,
    IReadOnlyList<ListingCardDto> Items);

public record InspirationGroupDto(string Tab, IReadOnlyList<InspirationLinkDto> Links);

public record InspirationLinkDto(string Title, string Subtitle, string Href);

public record HomeDto(
    IReadOnlyList<HomeSectionDto> Sections,
    IReadOnlyList<InspirationGroupDto> Inspiration);

public record SearchResultDto(
    int Total,
    int Page,
    int PageSize,
    IReadOnlyList<ListingCardDto> Items);

public record HostDto(
    int Id,
    string Name,
    string Initials,
    bool IsSuperhost,
    int YearsHosting,
    string? Bio,
    string ResponseRate,
    string ResponseTime,
    string JoinedLabel,
    int ListingCount,
    double AverageRating,
    int TotalReviews,
    /// <summary>Null for seeded demo hosts; set when the host has a real account to message.</summary>
    int? UserId);

public record ReviewDto(
    int Id,
    string AuthorName,
    string AuthorInitials,
    string? AuthorLocation,
    string When,
    string Text,
    double Rating);

public record RatingBreakdownDto(
    double Cleanliness,
    double Accuracy,
    double CheckIn,
    double Communication,
    double Location,
    double Value);

public record AmenityGroupDto(string Group, IReadOnlyList<AmenityDto> Items);

public record ListingDetailDto(
    ListingCardDto Card,
    string Description,
    string CancellationPolicy,
    IReadOnlyList<string> HouseRules,
    IReadOnlyList<string> SafetyInfo,
    IReadOnlyList<AmenityGroupDto> AmenityGroups,
    IReadOnlyList<ReviewDto> Reviews,
    RatingBreakdownDto RatingBreakdown,
    HostDto Host,
    IReadOnlyList<ListingCardDto> Similar,
    /// <summary>Nights already taken by a live booking; the picker greys these out.</summary>
    IReadOnlyList<DateOnly> UnavailableDates);

public record QuoteRequest(int ListingId, DateOnly CheckIn, DateOnly CheckOut, int Guests);

public record QuoteDto(
    int ListingId,
    int Nights,
    int Guests,
    decimal PricePerNight,
    decimal Subtotal,
    decimal CleaningFee,
    decimal ServiceFee,
    decimal Total,
    bool GuestsExceeded,
    int MaxGuests);

public record CreateBookingRequest(
    int ListingId,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Guests,
    string? GuestName,
    string? GuestEmail);

public record BookingDto(
    int Id,
    string Reference,
    int ListingId,
    string ListingTitle,
    string ListingCity,
    string ListingImage,
    string ListingSlug,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Nights,
    int Guests,
    decimal Subtotal,
    decimal CleaningFee,
    decimal ServiceFee,
    decimal Total,
    string Status,
    string PaymentStatus,
    bool HasReview,
    bool CanReview,
    DateTime CreatedAt);
