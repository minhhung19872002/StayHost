namespace StayHost.Web.Contracts;

/* ------------------------------------------------------------------ account */

public record RegisterRequest(
    string Email, string Password, string FullName, string? Phone,
    /// <summary>Optional: the code a friend sent. Falls back to matching on email.</summary>
    string? ReferralCode = null);

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
    PricingRulesDto Pricing,
    IReadOnlyList<BedroomDto> BedLayout,
    IReadOnlyList<string> ImageCaptions,
    LegalDeclarationDto Legal,
    int WizardStep,
    bool IsComplete);

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
    PricingRulesDto? Pricing = null,
    /// <summary>docs/01 CN-05 — beds per room, as the host laid them out.</summary>
    IReadOnlyList<BedroomDto>? BedLayout = null,
    /// <summary>docs/01 CN-07 — one label per photo, in display order.</summary>
    IReadOnlyList<string>? ImageCaptions = null,
    /// <summary>docs/01 CN-12 — licence, cameras, weapons, animals.</summary>
    LegalDeclarationDto? Legal = null,
    /// <summary>docs/01 CN-01 — where the host got to; 0 means finished.</summary>
    int WizardStep = 0,
    bool IsComplete = true);

/// <summary>docs/01 CN-12 — the declarations a host must make before publishing.</summary>
public record LegalDeclarationDto(
    string? LicenseNumber,
    bool HasSecurityCameras,
    string? SecurityCameraNote,
    bool HasWeaponsOnProperty,
    bool HasDangerousAnimals);

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
    string StatusLabel,
    string StatusBadge,
    string PaymentStatus,
    /// <summary>Set while a request is still inside its 24-hour window.</summary>
    DateTime? RequestExpiresAt,
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

/* --------------------------------------------------------- host operations */

/// <summary>docs/01 QL-01 — the day's work, grouped the way a host thinks about it.</summary>
public record TodayBoardDto(
    IReadOnlyList<TodayItemDto> Arriving,
    IReadOnlyList<TodayItemDto> InHouse,
    IReadOnlyList<TodayItemDto> Leaving,
    IReadOnlyList<TodayItemDto> AwaitingAnswer,
    int NeedsAnswer);

public record TodayItemDto(
    int BookingId, string Reference, string ListingTitle, string GuestName,
    DateOnly CheckIn, DateOnly CheckOut, int Nights, int Guests,
    string What, string StatusLabel, string StatusBadge);

/// <summary>docs/01 QL-04 — every listing's month, side by side.</summary>
public record MultiCalendarDto(DateOnly From, int Days, IReadOnlyList<MultiCalendarRowDto> Rows);

public record MultiCalendarRowDto(
    int ListingId, string Title, bool IsPublished, IReadOnlyList<MultiCalendarCellDto> Days);

public record MultiCalendarCellDto(
    DateOnly Date, decimal Rate, string RateSource,
    /// <summary>open / booked / blocked.</summary>
    string State,
    string? BookingReference);

/// <summary>docs/01 QL-06 and QL-07 — the rules that decide who can book which nights.</summary>
public record CalendarRulesDto(
    int MinNights,
    int MaxNights,
    int AdvanceNoticeHours,
    int? SameDayCutoffHour,
    int CalendarVisibilityMonths,
    int TurnoverDays,
    /// <summary>Bitmask over DayOfWeek; Sunday is bit 0.</summary>
    int BlockedCheckInDays,
    int BlockedCheckOutDays,
    string TimeZoneId);

/// <summary>docs/01 QL-05 — one edit applied to a run of days.</summary>
public record BulkDayEditRequest(
    DateOnly From,
    DateOnly To,
    decimal? NightlyRate,
    int? MinNights,
    /// <summary>True blocks the range, false clears blocks, null leaves them alone.</summary>
    bool? Blocked,
    string? Label);

/// <summary>docs/01 QL-20 — where the money goes, and when.</summary>
public record PayoutSettingsDto(
    string? BankName,
    string? AccountName,
    string? AccountLast4,
    string Schedule,
    IReadOnlyList<PayoutRowDto> Upcoming);

public record PayoutRowDto(string Reference, string ListingTitle, DateOnly DueOn, decimal Amount, string Status);

public record SavePayoutRequest(string? BankName, string? AccountName, string? AccountNumber, string? Schedule);

/// <summary>docs/03 §8 — the four criteria and where the host stands on each.</summary>
public record SuperhostProgressDto(
    bool IsSuperhost,
    bool WouldQualify,
    DateOnly NextReview,
    IReadOnlyList<SuperhostCriterionDto> Criteria);

public record SuperhostCriterionDto(string Key, string Label, string Current, string Target, bool Met);

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

/* -------------------------------------------------------- resolution centre */

public record OpenResolutionRequest(
    int BookingId, string Kind, decimal AmountClaimed, string Description, IReadOnlyList<string>? EvidenceUrls);

public record RespondResolutionRequest(bool Accept, string? Note);

public record DecideResolutionRequest(decimal AmountAwarded, string Decision);

/// <summary>docs/01 AT-04 — one claim, everything both sides and an admin need to see.</summary>
public record ResolutionCaseDto(
    int Id,
    string Reference,
    int BookingId,
    string BookingReference,
    string ListingTitle,
    string Kind,
    string KindLabel,
    string Status,
    string StatusLabel,
    string StatusBadge,
    decimal AmountClaimed,
    decimal AmountAwarded,
    string Description,
    IReadOnlyList<string> EvidenceUrls,
    string OpenedByName,
    bool OpenedByHost,
    DateTime ResponseDueAt,
    string? Response,
    DateTime? RespondedAt,
    string? Decision,
    string? DecidedByName,
    DateTime? DecidedAt,
    /// <summary>The viewer is the one who owes an answer right now.</summary>
    bool NeedsMyResponse,
    bool CanWithdraw,
    IReadOnlyList<ResolutionEventDto> History,
    DateTime CreatedAt);

public record ResolutionEventDto(string FromLabel, string ToLabel, string Actor, string Note, DateTime At);

/* -------------------------------------------------------------------- admin */

public record AdminOverviewDto(
    int Users, int Hosts, int Listings, int PublishedListings, int Drafts,
    int Bookings, int ActiveBookings, decimal GrossVolume, decimal PlatformRevenue,
    int OpenReports, int QueuedEmails,
    IReadOnlyList<AdminListingDto> RecentListings,
    IReadOnlyList<ReportDto> Reports,
    LedgerReportDto Ledger,
    /// <summary>docs/01 QT-09 — who did what, newest first.</summary>
    IReadOnlyList<StayHost.Web.Services.AdminAudit.AdminAuditRow> AuditLog,
    /// <summary>docs/01 QT-06 — fee rates and the regional tax rules.</summary>
    PlatformSettingsDto Settings);

public record PlatformSettingsDto(
    decimal GuestServiceFeeRate,
    decimal HostServiceFeeRate,
    int MaxDiscountPercent,
    decimal DefaultCleaningFee,
    IReadOnlyList<TaxRuleDto> TaxRules);

public record TaxRuleDto(
    int Id, string Country, string? City, string Name,
    string Method, string Base, decimal Value, int SortOrder, bool IsActive);

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

public record MessageDto(
    int Id, int SenderUserId, string SenderName, string Body, DateTime SentAt, bool Mine,
    /// <summary>Written by the platform, not a person (docs/01 TN-04).</summary>
    bool IsSystem,
    /// <summary>True when contact details in this message are currently masked (TN-07).</summary>
    bool ContactsMasked,
    /// <summary>docs/01 TN-02 — photos sent with the message.</summary>
    IReadOnlyList<string> Attachments);

public record ThreadDetailDto(
    ThreadSummaryDto Summary,
    IReadOnlyList<MessageDto> Messages,
    /// <summary>False until the guest has a confirmed booking at this listing.</summary>
    bool ContactsUnlocked,
    /// <summary>docs/01 TN-03 — the order this conversation is about, if there is one.</summary>
    ThreadBookingDto? Booking,
    /// <summary>docs/01 TN-08 — the host's saved phrases; empty for a guest.</summary>
    IReadOnlyList<QuickReplyDto> QuickReplies);

/// <summary>docs/01 TN-03 — a compact order card, with the actions worth taking from here.</summary>
public record ThreadBookingDto(
    int Id, string Reference, DateOnly CheckIn, DateOnly CheckOut, int Nights, int Guests,
    decimal Total, string StatusLabel, string StatusBadge, bool NeedsHostAnswer);

public record QuickReplyDto(int Id, string Title, string Body, int SortOrder);

public record SaveQuickReplyRequest(string Title, string Body, int SortOrder);

public record SendMessageRequest(int? ThreadId, int? ListingId, string Body, IReadOnlyList<string>? Attachments = null);

public record ReplyToReviewRequest(string Text);

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
    double Value,
    /// <summary>docs/01 ĐG-05 — feedback for the host alone, never published.</summary>
    string? PrivateNote = null);

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
    decimal? StayTotal,
    /// <summary>
    /// The stay this card was matched on. Only set when a flexible search
    /// (docs/01 TM-06, TM-07) landed it on dates other than the ones shown at
    /// the top of the page.
    /// </summary>
    DateOnly? StayCheckIn = null,
    DateOnly? StayCheckOut = null);

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
    IReadOnlyList<ListingCardDto> Items,
    /// <summary>Only present when nothing matched (docs/01 TM-22).</summary>
    NoResultsDto? NoResults = null,
    /// <summary>Only present when the guest searched flexible dates.</summary>
    FlexibleDatesDto? Dates = null);

/// <summary>
/// docs/01 TM-06 and TM-07 — what a loose "a week sometime in October" was
/// turned into, so the page can say which stay it is pricing.
/// </summary>
public record FlexibleDatesDto(
    string Length,
    string Label,
    int Nights,
    DateOnly CheckIn,
    DateOnly CheckOut,
    /// <summary>How many alternative stays were considered.</summary>
    int Options);

/// <summary>
/// docs/01 TM-22 — when a search comes back empty, say which filter is doing
/// the blocking and where there is something nearby, rather than a dead end.
/// </summary>
public record NoResultsDto(
    IReadOnlyList<BlockingFilterDto> BlockingFilters,
    IReadOnlyList<NearbyAreaDto> NearbyAreas);

/// <summary>A filter that, dropped on its own, would bring back <c>Count</c> stays.</summary>
public record BlockingFilterDto(string Key, string Label, int Count);

public record NearbyAreaDto(string City, int Count, decimal FromPrice);

/// <summary>One night on the room calendar: its rate and whether it is for sale.</summary>
public record CalendarNightDto(
    DateOnly Date,
    decimal Rate,
    /// <summary>day / season / weekend / base — where the rate came from.</summary>
    string RateSource,
    bool Available,
    int MinNights);

/// <summary>docs/01 TĐ-09 — a run of free nights offered when the chosen dates are taken.</summary>
public record OpeningDto(DateOnly From, DateOnly To, int Nights);

public record ListingCalendarDto(
    int ListingId,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<CalendarNightDto> Nights,
    IReadOnlyList<OpeningDto> NextOpenings);

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
    double Rating,
    /// <summary>docs/01 TĐ-12 — the host's single public answer.</summary>
    string? HostReply,
    DateTime? HostRepliedAt);

public record RatingBreakdownDto(
    double Cleanliness,
    double Accuracy,
    double CheckIn,
    double Communication,
    double Location,
    double Value,
    /// <summary>docs/01 TĐ-10 — how many reviews gave 5, 4, 3, 2 and 1 star.</summary>
    IReadOnlyList<int> StarCounts);

public record AmenityGroupDto(string Group, IReadOnlyList<AmenityDto> Items);

/// <summary>
/// docs/01 TĐ-04 — the amenity list shows everything, with the ones this place
/// does not have struck through, so a guest can see what is missing.
/// </summary>
public record AmenityAvailabilityDto(string Key, string Label, string Icon, string Group, bool Available);

/// <summary>docs/01 TĐ-05 — which beds are in which room.</summary>
public record BedroomDto(string Name, IReadOnlyList<string> Beds);

public record ListingDetailDto(
    ListingCardDto Card,
    string Description,
    string CancellationPolicy,
    IReadOnlyList<string> HouseRules,
    IReadOnlyList<string> SafetyInfo,
    IReadOnlyList<AmenityGroupDto> AmenityGroups,
    /// <summary>Every filterable amenity, marked present or missing (TĐ-04).</summary>
    IReadOnlyList<AmenityAvailabilityDto> AllAmenities,
    IReadOnlyList<BedroomDto> Bedrooms,
    IReadOnlyList<ReviewDto> Reviews,
    RatingBreakdownDto RatingBreakdown,
    HostDto Host,
    IReadOnlyList<ListingCardDto> Similar,
    /// <summary>Nights already taken by a live booking; the picker greys these out.</summary>
    IReadOnlyList<DateOnly> UnavailableDates,
    /// <summary>
    /// docs/01 MR-08 — empty for an ordinary place. When it has rows the guest
    /// must pick one before checkout (MR-09).
    /// </summary>
    IReadOnlyList<HotelRoomDto>? RoomTypes = null);

public record HotelRoomDto(
    int Id,
    string Name,
    string Summary,
    int Inventory,
    /// <summary>How many of this kind are still free on the busiest night of the searched stay.</summary>
    int Available,
    int MaxGuests,
    int Beds,
    double SizeSqm,
    decimal PricePerNight,
    string? ImageUrl,
    IReadOnlyList<string> Features);

public record QuoteRequest(int ListingId, DateOnly CheckIn, DateOnly CheckOut, int Guests);

/* ---- docs/01 MR-10: best-price guarantee -------------------------------- */

public record PriceMatchDto(
    int Id,
    int BookingId,
    string Reference,
    string CompetitorUrl,
    decimal CompetitorNightlyRate,
    decimal OurNightlyRate,
    decimal Difference,
    string Status,
    string StatusLabel,
    string? Decision,
    DateTime CreatedAt);

public record SubmitPriceMatchRequest(string? CompetitorUrl, decimal CompetitorNightlyRate);

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

/// <summary>Card details captured when the guest actually pays, not when the hold started.</summary>
public record PayBookingRequest(
    string? PaymentMethod,
    string? CardLast4,
    /// <summary>docs/01 ĐP-06 — take a deposit now instead of the whole amount.</summary>
    bool PayDeposit = false,
    /// <summary>How much of a deposit. Held to at least half the total.</summary>
    decimal? DepositAmount = null);

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
    int Pets = 0,
    /// <summary>docs/01 MR-09 — which kind of room, when the listing is a hotel.</summary>
    int? RoomTypeId = null,
    /// <summary>Spend the guest's balance on this booking, up to the room charge.</summary>
    bool UseCredit = false);

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
    /// <summary>The Vietnamese wording; the server owns it so every screen agrees.</summary>
    string StatusLabel,
    /// <summary>pending / confirmed / cancelled — the badge classes the UI already has.</summary>
    string StatusBadge,
    string PaymentStatus,
    string? PaymentReference,
    string? PaymentMethod,
    string? CardLast4,
    bool HasReview,
    bool CanReview,
    bool CanCancel,
    /// <summary>When the 15-minute payment hold lapses, if one is running.</summary>
    DateTime? HoldExpiresAt,
    /// <summary>When an unanswered request to book expires.</summary>
    DateTime? RequestExpiresAt,
    /// <summary>Append-only history, oldest first (docs/00 §6.2).</summary>
    IReadOnlyList<BookingEventDto> History,
    string? GuestNote,
    string HostName,
    DateTime CreatedAt,
    // --- docs/01 ĐP-06: paying a deposit now and the rest later.
    decimal DepositPaid = 0,
    decimal BalanceDue = 0,
    DateOnly? BalanceDueOn = null,
    string BalanceStatus = "None",
    string BalanceLabel = "");

public record BookingEventDto(
    string? FromStatus, string FromLabel, string ToStatus, string ToLabel,
    string Actor, string Reason, DateTime At);

/* ---- docs/01 QL-19: co-hosts --------------------------------------------- */

public record ScopeOptionDto(string Key, string Label);

public record CoHostDto(
    int Id,
    string Email,
    string? Name,
    /// <summary>Null means every listing this host has, now and later.</summary>
    int? ListingId,
    string? ListingTitle,
    IReadOnlyList<string> Scopes,
    string ScopeLabel,
    string Status,
    string StatusLabel,
    DateTime InvitedAt);

/// <summary>The same grant seen from the other side, by the person invited.</summary>
public record CoHostInviteDto(
    int Id,
    string Token,
    string OwnerName,
    int? ListingId,
    string? ListingTitle,
    string ScopeLabel,
    string Status,
    string StatusLabel);

public record CoHostBoardDto(
    IReadOnlyList<CoHostDto> Invited,
    IReadOnlyList<CoHostInviteDto> Helping,
    IReadOnlyList<ScopeOptionDto> Scopes);

public record InviteCoHostRequest(string? Email, int? ListingId, IReadOnlyList<string>? Scopes);

/* ---- docs/01 QL-10: calendar sync ---------------------------------------- */

public record CalendarFeedDto(
    int Id,
    string Label,
    string Url,
    DateTime? LastSyncedAt,
    /// <summary>Set when the last attempt failed; the blocks from before are kept.</summary>
    string? LastError,
    int EventCount);

public record CalendarSyncDto(
    int ListingId,
    string Title,
    /// <summary>The address other platforms subscribe to. The token in it is the credential.</summary>
    string ExportUrl,
    IReadOnlyList<CalendarFeedDto> Feeds);

public record AddCalendarFeedRequest(string? Url, string? Label);

/* ---- docs/01 AT-07: the help centre ------------------------------------- */

public record HelpArticleDto(
    string Slug,
    string Title,
    string Category,
    string Audience,
    string AudienceLabel,
    string Summary,
    /// <summary>Only filled in when a single article was asked for.</summary>
    string? Body,
    DateTime UpdatedAt);

public record HelpCategoryDto(string Name, int Count);

public record HelpIndexDto(
    IReadOnlyList<HelpArticleDto> Articles,
    IReadOnlyList<HelpCategoryDto> Categories,
    int Total);

/* ---- docs/01 AT-11: anomaly detection ----------------------------------- */

public record RiskFlagDto(
    int Id,
    int UserId,
    string UserName,
    string UserEmail,
    int? BookingId,
    string? BookingReference,
    string Kind,
    string Severity,
    string SeverityLabel,
    string SeverityBadge,
    string Summary,
    string Detail,
    string Status,
    string? Resolution,
    DateTime CreatedAt);

public record ResolveRiskFlagRequest(string? Resolution, bool Acted = false);

/* ---- docs/01 ĐP-07: one booking, up to sixteen payers -------------------- */

public record BillShareDto(
    int Id,
    string Email,
    string? Name,
    decimal Amount,
    string Status,
    string StatusLabel,
    /// <summary>The link this person follows. Whoever holds it can pay this share and nothing else.</summary>
    string Link,
    DateTime? PaidAt);

public record BillSplitDto(
    int Id,
    int BookingId,
    string Reference,
    decimal Total,
    string Status,
    string StatusLabel,
    DateTime ExpiresAt,
    IReadOnlyList<BillShareDto> Shares);

public record OpenSplitRequest(IReadOnlyList<string>? Emails);

/// <summary>What someone sees when they open their link, with no account.</summary>
public record ShareInviteDto(
    string Token,
    string Reference,
    string ListingTitle,
    string City,
    DateOnly CheckIn,
    DateOnly CheckOut,
    int Nights,
    int Guests,
    decimal Total,
    decimal Amount,
    string ShareStatus,
    string ShareStatusLabel,
    string SplitStatus,
    string SplitStatusLabel,
    int PaidCount,
    int PeopleCount,
    DateTime ExpiresAt);

public record PayShareRequest(string? Name, string? CardLast4);

/* ---- docs/01 MR-01 → MR-04: experiences ---------------------------------- */

public record ExperienceCardDto(
    int Id,
    string Slug,
    string Title,
    string City,
    string Summary,
    int DurationMinutes,
    int MaxGroup,
    decimal PricePerPerson,
    double Rating,
    int ReviewCount,
    string HostName,
    IReadOnlyList<string> Images,
    /// <summary>Sessions still on sale, so a card can say "còn 3 suất".</summary>
    int OpenSlots);

public record ExperienceSlotDto(
    int Id,
    DateTime StartsAt,
    int Capacity,
    int SeatsTaken,
    int SeatsLeft,
    bool IsPrivate,
    string Status,
    string? CancelReason);

public record ExperienceDetailDto(
    int Id,
    string Slug,
    string Title,
    string City,
    string Country,
    string Summary,
    string Description,
    int DurationMinutes,
    int MaxGroup,
    int MinGuests,
    IReadOnlyList<string> Languages,
    int MinAge,
    string MeetingPoint,
    double Latitude,
    double Longitude,
    IReadOnlyList<string> Included,
    decimal PricePerPerson,
    decimal? PrivateGroupPrice,
    bool IsPublished,
    double Rating,
    int ReviewCount,
    string HostName,
    string HostInitials,
    IReadOnlyList<string> Images,
    IReadOnlyList<ExperienceSlotDto> Slots);

public record ExperienceQuoteDto(
    int SlotId,
    DateTime StartsAt,
    int Seats,
    bool Private,
    decimal PerSeat,
    decimal Subtotal,
    decimal ServiceFee,
    decimal Tax,
    decimal Total,
    IReadOnlyList<PriceLineDto> Lines,
    bool CanBook,
    string? Reason);

public record ExperienceBookingDto(
    int Id,
    string Reference,
    int ExperienceId,
    string Title,
    string City,
    string Slug,
    DateTime StartsAt,
    int DurationMinutes,
    int Seats,
    bool Private,
    decimal Subtotal,
    decimal ServiceFee,
    decimal Tax,
    decimal Total,
    decimal RefundedAmount,
    string Status,
    string StatusLabel,
    string StatusBadge,
    string? CancelReason,
    DateTime CreatedAt);

public record BookExperienceRequest(
    int Seats,
    bool Private = false,
    string? PaymentMethod = null,
    string? CardLast4 = null);

public record SaveExperienceRequest(
    int? Id,
    string? Title,
    string? City,
    string? Summary,
    string? Description,
    int DurationMinutes,
    int MaxGroup,
    int MinGuests,
    IReadOnlyList<string>? Languages,
    int MinAge,
    string? MeetingPoint,
    double Latitude,
    double Longitude,
    IReadOnlyList<string>? Included,
    decimal PricePerPerson,
    decimal? PrivateGroupPrice,
    IReadOnlyList<string>? Images,
    bool Publish = false);

public record AddSlotsRequest(IReadOnlyList<DateTime>? StartsAt, int? Capacity);

/* ---- docs/01 MR-05 → MR-07: services ------------------------------------ */

public record ServiceCardDto(
    int Id,
    string Slug,
    string Title,
    string Category,
    string City,
    string Summary,
    decimal BasePrice,
    string Pricing,
    string PricingLabel,
    string Unit,
    bool TravelsToGuest,
    int ServiceRadiusKm,
    /// <summary>docs/01 MR-07 — run by somebody else, with the platform on commission.</summary>
    bool IsPartner,
    string? PartnerName,
    double Rating,
    int ReviewCount,
    string HostName,
    IReadOnlyList<string> Images);

/// <summary>A stretch of the provider's diary that is already spoken for.</summary>
public record BusySlotDto(DateTime From, DateTime To);

public record ServiceDetailDto(
    int Id,
    string Slug,
    string Title,
    string Category,
    string City,
    string Country,
    string Summary,
    string Description,
    decimal BasePrice,
    string Pricing,
    string PricingLabel,
    string Unit,
    int MinQuantity,
    int MaxQuantity,
    int DurationMinutes,
    bool TravelsToGuest,
    int ServiceRadiusKm,
    double Latitude,
    double Longitude,
    int OpensAtHour,
    int ClosesAtHour,
    bool IsPartner,
    string? PartnerName,
    bool IsPublished,
    double Rating,
    int ReviewCount,
    string HostName,
    string HostInitials,
    IReadOnlyList<string> Images,
    IReadOnlyList<BusySlotDto> Busy);

public record ServiceQuoteDto(
    int OfferingId,
    DateTime StartsAt,
    int DurationMinutes,
    int Quantity,
    decimal Subtotal,
    decimal ServiceFee,
    decimal Tax,
    decimal Total,
    IReadOnlyList<PriceLineDto> Lines,
    bool CanBook,
    string? Reason);

public record ServiceBookingDto(
    int Id,
    string Reference,
    int OfferingId,
    string Title,
    string Slug,
    string Category,
    string City,
    DateTime StartsAt,
    int DurationMinutes,
    int Quantity,
    string Unit,
    string Address,
    string? Note,
    decimal Subtotal,
    decimal ServiceFee,
    decimal Tax,
    decimal Total,
    decimal RefundedAmount,
    string Status,
    string StatusLabel,
    string StatusBadge,
    string? CancelReason,
    DateTime CreatedAt);

public record QuoteServiceRequest(
    DateTime StartsAt,
    int Quantity,
    string? Address,
    double? Latitude,
    double? Longitude);

public record BookServiceRequest(
    DateTime StartsAt,
    int Quantity,
    string? Address,
    double? Latitude,
    double? Longitude,
    string? Note,
    string? PaymentMethod,
    string? CardLast4);

/* ---- gift cards, balance and referrals ---------------------------------- */

public record CreditEntryDto(
    long Id,
    /// <summary>Positive when granted, negative when spent.</summary>
    decimal Amount,
    string Reason,
    string ReasonLabel,
    string Memo,
    int? BookingId,
    DateTime CreatedAt);

public record GiftCardDto(
    int Id,
    string Code,
    decimal Amount,
    decimal Remaining,
    string RecipientEmail,
    string? RecipientName,
    string? Message,
    string Status,
    string StatusLabel,
    DateTime CreatedAt,
    DateTime? RedeemedAt);

public record ReferralDto(
    int Id,
    string Code,
    string InviteeEmail,
    string? InviteeName,
    string Status,
    string StatusLabel,
    decimal ReferrerReward,
    decimal InviteeReward,
    DateTime CreatedAt);

public record WalletDto(
    decimal Balance,
    IReadOnlyList<CreditEntryDto> Entries,
    IReadOnlyList<GiftCardDto> GiftCards,
    IReadOnlyList<ReferralDto> Referrals,
    decimal ReferrerReward,
    decimal InviteeReward,
    decimal MinGiftCard,
    decimal MaxGiftCard);

public record BuyGiftCardRequest(decimal Amount, string? RecipientEmail, string? RecipientName, string? Message);
public record RedeemGiftCardRequest(string? Code);
public record InviteFriendRequest(string? Email);

/* ---- docs/06 — StayShield ------------------------------------------------ */

public record ShieldEvidenceDto(int Id, string Url, string? Caption, string Kind);

public record ShieldItemDto(
    int Id,
    string Name,
    decimal Value,
    /// <summary>docs/06 §3.2 C-E — declared on the listing before the guest arrived.</summary>
    bool DeclaredOnListing,
    /// <summary>What the per-item ceiling let through.</summary>
    decimal Allowed);

public record ShieldEventDto(
    int Id,
    string? FromStatus,
    string ToStatus,
    string ToStatusLabel,
    string Actor,
    string Note,
    DateTime CreatedAt);

public record ShieldClaimDto(
    int Id,
    string Reference,
    int BookingId,
    string BookingReference,
    string ListingTitle,
    string ListingSlug,
    string Side,
    string Kind,
    string KindLabel,
    string Status,
    string StatusLabel,
    string StatusBadge,
    string Description,
    decimal Claimed,
    decimal ExpensesClaimed,
    decimal RehousingDifference,
    string Remedy,
    decimal Approved,
    decimal Deductible,
    decimal CreditGranted,
    decimal PaidFromFund,
    decimal RecoveredFromCounterparty,
    decimal RecoveredLater,
    string? Decision,
    DateTime? DecidedAt,
    bool Appealed,
    /// <summary>docs/06 §7 — a flagged account never settles itself.</summary>
    bool NeedsManualReview,
    DateTime RespondBy,
    DateTime FirstResponseDueAt,
    DateTime DecisionDueAt,
    DateTime CreatedAt,
    string OpenedByName,
    bool OpenedByMe,
    IReadOnlyList<ShieldEvidenceDto> Evidence,
    IReadOnlyList<ShieldItemDto> Items,
    IReadOnlyList<ShieldEventDto> Events);

public record ShieldCaseTotalDto(string Kind, string Label, int Count, decimal PaidFromFund);

public record ShieldFundDto(
    decimal Balance,
    decimal ContributedThisMonth,
    decimal SpentThisMonth,
    decimal RecoveredThisMonth,
    /// <summary>docs/06 §5 — spending has passed the warning threshold for the month.</summary>
    bool Alarm,
    decimal ContributionRate,
    decimal AlarmRate,
    IReadOnlyList<ShieldCaseTotalDto> ByCase);

public record ShieldEvidenceInput(string? Url, string? Caption, string? Kind);

public record ShieldItemInput(string? Name, decimal Value, bool DeclaredOnListing);

public record OpenShieldClaimRequest(
    string? Kind,
    string? Description,
    /// <summary>docs/06 §2.2 — a safety matter or strangers inside skips the waiting period.</summary>
    bool Urgent = false,
    decimal ExpensesClaimed = 0,
    decimal RehousingDifference = 0,
    IReadOnlyList<ShieldEvidenceInput>? Evidence = null,
    IReadOnlyList<ShieldItemInput>? Items = null);

public record RespondShieldRequest(string? Answer, decimal? AgreedAmount, string? Note);

public record DecideShieldRequest(
    bool Approve,
    string? Reason,
    /// <summary>Guest cases: Rehoused · SelfRehoused · Refunded (docs/06 §2.3).</summary>
    string? Remedy = null,
    int? NightsUnused = null,
    /// <summary>Host cases: what the arbitration allows before ceilings and the excess.</summary>
    decimal? ApprovedAmount = null,
    decimal? DepositAvailable = null,
    decimal? RecoverFromGuest = null);

public record RecoverShieldRequest(decimal Amount);

/// <summary>docs/06 §8 AT-06-01 — what the programme covers, in the platform's own words.</summary>
public record ShieldTermsDto(
    string Side,
    string Title,
    string Intro,
    IReadOnlyList<ShieldTermsSectionDto> Sections,
    IReadOnlyList<string> Exclusions,
    string Disclaimer);

public record ShieldTermsSectionDto(string Heading, IReadOnlyList<string> Points);

public record HostCancelRequest(string? Reason);

/* ---- docs/06 AT-06-08: finding somewhere else for a guest ---------------- */

public record RehousingOptionDto(
    int ListingId,
    string Slug,
    string Title,
    string City,
    string TypeLabel,
    int MaxGuests,
    int Bedrooms,
    double Rating,
    int ReviewCount,
    string? Image,
    /// <summary>All-in price for the nights the guest still has, priced by the usual engine.</summary>
    decimal Total,
    /// <summary>How much more than the original booking's share of those nights.</summary>
    decimal Difference,
    /// <summary>True when the difference sits inside what the platform will cover.</summary>
    bool WithinTopUp,
    /// <summary>Roughly how far from the place they booked.</summary>
    double DistanceKm);

public record RehousingDto(
    int ClaimId,
    string Reference,
    string OriginalTitle,
    string City,
    DateOnly From,
    DateOnly To,
    int Nights,
    int Guests,
    /// <summary>What the guest paid for the nights being replaced.</summary>
    decimal AlreadyPaid,
    /// <summary>The ceiling on the difference the platform will cover (docs/06 K-A).</summary>
    decimal TopUpCeiling,
    IReadOnlyList<RehousingOptionDto> Options);
