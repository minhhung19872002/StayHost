namespace StayHost.Web.Contracts;

/* ------------------------------------------------------------------ account */

public record RegisterRequest(string Email, string Password, string FullName, string? Phone);

public record LoginRequest(string Email, string Password);

public record UpdateProfileRequest(string? FullName, string? Phone, string? Bio);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Token, string NewPassword);

public record VerifyEmailRequest(string Token);

public record SessionDto(int Id, string Device, DateTime CreatedAt, DateTime ExpiresAt, bool IsCurrent);

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
    bool EmailConfirmed,
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
    string CancellationTier,
    double Rating,
    int ReviewCount,
    string Description,
    string? Highlight,
    double Latitude,
    double Longitude,
    IReadOnlyList<string> Images,
    IReadOnlyList<string> AmenityKeys,
    int UpcomingBookings,
    decimal EarningsToDate,
    PricingRulesDto Pricing);

/// <summary>The host-settable half of docs/03 §1 — discounts and surcharges.</summary>
public record PricingRulesDto(
    int WeeklyDiscountPercent,
    int MonthlyDiscountPercent,
    int EarlyBirdDays,
    int EarlyBirdPercent,
    int LastMinuteDays,
    int LastMinutePercent,
    decimal WeekendSurchargeRate,
    int FreeGuestThreshold,
    decimal ExtraGuestFee,
    bool PetsAllowed,
    int MaxPets,
    decimal PetFee,
    bool PetFeePerNight);

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
    string CancellationTier,
    string Description,
    string? Highlight,
    double? Latitude,
    double? Longitude,
    IReadOnlyList<string> Images,
    IReadOnlyList<string> AmenityKeys,
    /// <summary>Omitted by older clients; the listing keeps its current rules then.</summary>
    PricingRulesDto? Pricing = null);

public record CalendarBlockDto(int Id, DateOnly From, DateOnly To, string? Note);

public record PriceRuleDto(int Id, string Name, DateOnly From, DateOnly To, decimal NightlyRate);

public record CreatePriceRuleRequest(int ListingId, string? Name, DateOnly From, DateOnly To, decimal NightlyRate);

public record ReviewGuestRequest(double Rating, string Text, bool WouldHostAgain);

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

/* ------------------------------------------------------------ notifications */

public record NotificationDto(
    int Id, string Kind, string Title, string Body, string? Link, bool Unread, DateTime CreatedAt);

public record NotificationFeedDto(int Unread, IReadOnlyList<NotificationDto> Items);

/* ------------------------------------------------------------------ reports */

public record CreateReportRequest(int ListingId, string Reason, string? Detail);

public record ReportDto(
    int Id, int ListingId, string ListingTitle, string Reason, string? Detail,
    string Status, string? Resolution, string ReporterName, DateTime CreatedAt);

public record ResolveReportRequest(string Status, string? Resolution);

/* -------------------------------------------------------------------- admin */

public record AdminOverviewDto(
    int Users, int Hosts, int Listings, int PublishedListings, int Drafts,
    int Bookings, int ActiveBookings, decimal GrossVolume, decimal PlatformRevenue,
    int OpenReports, int QueuedEmails,
    IReadOnlyList<AdminListingDto> RecentListings,
    IReadOnlyList<ReportDto> Reports,
    LedgerReportDto Ledger);

/// <summary>
/// The daily reconciliation docs/03 §5 asks for. <c>Imbalance</c> must be zero;
/// anything else means a transaction was written without its other half.
/// </summary>
public record LedgerReportDto(
    decimal Imbalance,
    int Entries,
    int Transactions,
    IReadOnlyList<LedgerAccountDto> Accounts);

public record LedgerAccountDto(string Account, string Label, decimal Debits, decimal Credits, decimal Balance);

public record AdminListingDto(
    int Id, string Slug, string Title, string City, string HostName,
    bool IsPublished, double Rating, int ReviewCount, decimal PricePerNight, DateTime CreatedAt);

/* ---------------------------------------------------------------- wishlists */

public record WishlistDto(
    int Id,
    string Name,
    bool IsDefault,
    int Count,
    IReadOnlyList<string> CoverImages);

public record WishlistDetailDto(WishlistDto List, IReadOnlyList<ListingCardDto> Items);

public record SaveWishlistRequest(string Name);

/* ----------------------------------------------------------------- messages */

public record ThreadSummaryDto(
    int Id,
    int ListingId,
    string ListingSlug,
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
    IReadOnlyList<LanguageDto> Languages,
    FeesDto Fees);

/// <summary>
/// The fee constants the client needs to explain a price. The authoritative
/// numbers still live in <c>Pricing</c>; these are published so the UI never
/// hard-codes a rate of its own (docs/00 §6.8).
/// </summary>
public record FeesDto(
    decimal GuestServiceFeeRate,
    decimal HostServiceFeeRate,
    int MaxDiscountPercent,
    decimal DefaultCleaningFee);

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
    /// <summary>Pre-discount nightly rate, or null when the listing is not on offer.</summary>
    decimal? OriginalPricePerNight,
    int DiscountPercent,
    bool InstantBook,
    double Rating,
    int ReviewCount,
    bool IsSuperhost,
    bool IsGuestFavorite,
    double Latitude,
    double Longitude,
    string? Highlight,
    IReadOnlyList<string> Images,
    IReadOnlyList<string> AmenityKeys,
    bool IsFavorite,
    /// <summary>Cleaning fee, so a card can show the same all-in figure as checkout.</summary>
    decimal CleaningFee,
    /// <summary>
    /// All-in total for the searched dates, priced by the same engine as the
    /// room page and checkout. Null when the search carried no dates.
    /// </summary>
    decimal? StayTotal);

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

/// <summary>One row of the price breakdown, already rounded. Negative means a reduction.</summary>
public record PriceLineDto(string Key, string Label, decimal Amount);

public record QuoteDto(
    int ListingId,
    int Nights,
    int Guests,
    decimal PricePerNight,
    decimal RoomBeforeDiscount,
    decimal RoomDiscount,
    int DiscountPercent,
    decimal ExtraGuestFee,
    decimal PetFee,
    decimal CleaningFee,
    decimal Subtotal,
    decimal ServiceFee,
    decimal Tax,
    decimal Total,
    decimal HostServiceFee,
    decimal HostPayout,
    /// <summary>Exactly what to render; the total is the sum of these.</summary>
    IReadOnlyList<PriceLineDto> Lines,
    bool GuestsExceeded,
    int MaxGuests,
    int MinNights,
    bool BelowMinNights,
    string CancellationTier,
    string CancellationSummary);

public record RefundPreviewDto(
    decimal Refund,
    decimal Penalty,
    decimal Total,
    string Explanation,
    decimal RoomRefund,
    decimal CleaningRefund,
    decimal ServiceFeeRefund,
    decimal TaxRefund,
    decimal GoodwillCredit);

public record CreateBookingRequest(
    int ListingId,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Guests,
    string? GuestName,
    string? GuestEmail,
    string? GuestNote,
    string? PaymentMethod,
    string? CardLast4,
    int? Adults = null,
    int Children = 0,
    int Infants = 0,
    int Pets = 0);

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
    decimal Tax,
    decimal Total,
    decimal RefundedAmount,
    decimal GoodwillCredit,
    /// <summary>The rows exactly as they were quoted, so an old receipt still adds up.</summary>
    IReadOnlyList<PriceLineDto> Lines,
    string CancellationTier,
    string CancellationSummary,
    string Status,
    string PaymentStatus,
    string? PaymentReference,
    string? PaymentMethod,
    string? CardLast4,
    bool HasReview,
    bool CanReview,
    bool CanCancel,
    string? GuestNote,
    string HostName,
    DateTime CreatedAt);
