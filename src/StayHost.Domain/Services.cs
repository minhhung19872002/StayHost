namespace StayHost.Domain;

/// <summary>docs/01 MR-05 — how a service charges for itself.</summary>
public enum ServicePricing
{
    /// <summary>One price for the visit, however long it takes.</summary>
    PerSession = 0,
    PerHour = 1,
    PerPerson = 2,
    /// <summary>Per item, per bag, per trip — anything counted.</summary>
    PerOrder = 3
}

public enum ServiceBookingStatus
{
    Requested = 0,
    Confirmed = 1,
    Completed = 2,
    CancelledByGuest = 3,
    CancelledByProvider = 4
}

/// <summary>
/// docs/00 §2 and docs/01 MR-05 — a chef, a photographer, an airport transfer.
/// Sold by the session rather than by the night, and often at the guest's
/// address rather than the provider's.
/// </summary>
public class ServiceOffering
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";

    /// <summary>chef, photo, massage, fitness, transfer, luggage, groceries, car…</summary>
    public string Category { get; set; } = "";

    public string City { get; set; } = "";
    public string Country { get; set; } = "Việt Nam";

    public int HostId { get; set; }
    public HostProfile? Host { get; set; }

    public string Summary { get; set; } = "";
    public string Description { get; set; } = "";

    public ServicePricing Pricing { get; set; } = ServicePricing.PerSession;
    public decimal BasePrice { get; set; }

    /// <summary>The smallest booking the provider will take, in whatever the price is per.</summary>
    public int MinQuantity { get; set; } = 1;
    public int MaxQuantity { get; set; } = 10;

    /// <summary>Roughly how long one booking blocks, so two do not collide.</summary>
    public int DurationMinutes { get; set; } = 120;

    /// <summary>docs/01 MR-05 — how far the provider will travel, and whether they travel at all.</summary>
    public bool TravelsToGuest { get; set; } = true;
    public int ServiceRadiusKm { get; set; } = 10;

    /// <summary>Where the provider is based; the radius is measured from here.</summary>
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    /// <summary>Hours of the day the provider takes bookings, in local time.</summary>
    public int OpensAtHour { get; set; } = 8;
    public int ClosesAtHour { get; set; } = 20;

    // --- docs/01 MR-07: run by somebody else, with the platform taking a cut.
    public bool IsPartner { get; set; }
    public string? PartnerName { get; set; }

    /// <summary>What the platform keeps from a partner booking. Ignored for a host's own service.</summary>
    public decimal CommissionRate { get; set; } = 0.15m;

    public string TimeZoneId { get; set; } = "Asia/Ho_Chi_Minh";
    public bool IsPublished { get; set; }

    public double Rating { get; set; }
    public int ReviewCount { get; set; }

    public string SearchText { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ServiceImage> Images { get; set; } = [];

    public void RefreshSearchText() =>
        SearchText = StayHost.Domain.SearchText.Normalize(
            string.Join(' ', Title, Category, City, Country, Summary, Description, PartnerName ?? ""));
}

public class ServiceImage
{
    public int Id { get; set; }
    public int OfferingId { get; set; }
    public ServiceOffering? Offering { get; set; }
    public string Url { get; set; } = "";
    public int SortOrder { get; set; }
}

public class ServiceBooking
{
    public int Id { get; set; }
    public string Reference { get; set; } = "";

    public int OfferingId { get; set; }
    public ServiceOffering? Offering { get; set; }

    public int GuestUserId { get; set; }
    public User? GuestUser { get; set; }

    public DateTime StartsAt { get; set; }
    public int DurationMinutes { get; set; }

    /// <summary>Hours, people or items — whatever this service charges by.</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>docs/01 MR-06 — where the work happens.</summary>
    public string Address { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? Note { get; set; }

    public decimal Subtotal { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }

    /// <summary>What the platform keeps: the host fee, or the commission on a partner job.</summary>
    public decimal PlatformCut { get; set; }
    public decimal ProviderPayout { get; set; }

    public ServiceBookingStatus Status { get; set; } = ServiceBookingStatus.Confirmed;
    public decimal RefundedAmount { get; set; }
    public string? CancelReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledAt { get; set; }

    // docs/09 §4 (MR-C-03) — the provider is paid a day after the session ends.
    public PayoutStatus PayoutStatus { get; set; } = PayoutStatus.Scheduled;
    public DateTime? PaidOutAt { get; set; }
    public string? PayoutReference { get; set; }

    public DateTime EndsAt => StartsAt.AddMinutes(DurationMinutes);
}

/// <summary>The rules a service booking obeys, kept where they can be tested.</summary>
public static class ServiceRules
{
    /// <summary>Cancel this far ahead and it costs nothing.</summary>
    public static readonly TimeSpan FreeCancellation = TimeSpan.FromHours(24);

    /// <summary>Nothing may be booked closer than this to now.</summary>
    public static readonly TimeSpan MinimumNotice = TimeSpan.FromHours(4);

    public enum Refusal
    {
        None = 0,
        TooSoon,
        OutsideHours,
        OutOfRange,
        QuantityOutOfRange,
        AlreadyBooked,
        NoAddress,
        DoesNotTravel,
        TooTight
    }

    /// <summary>A job already on the provider's diary, with where it happens.</summary>
    public sealed record BusyJob(DateTime From, DateTime To, double Lat, double Lng);

    /// <summary>docs/09 §3.4 — the mandatory rest/clean-up gap between two jobs.</summary>
    public static readonly TimeSpan BufferBetweenJobs = TimeSpan.FromMinutes(30);

    /// <summary>A conservative city travel speed, used to turn distance into time.</summary>
    public const double TravelKmPerHour = 25;

    public static TimeSpan TravelTime(double km) => TimeSpan.FromHours(km / TravelKmPerHour);

    public readonly record struct Check(bool Ok, Refusal Reason, string Message)
    {
        public static Check Pass => new(true, Refusal.None, "");
        public static Check Fail(Refusal reason, string message) => new(false, reason, message);
    }

    public sealed record Request
    {
        public required ServiceOffering Offering { get; init; }
        public required DateTime StartsAt { get; init; }
        public required DateTime Now { get; init; }
        public required int Quantity { get; init; }
        public string? Address { get; init; }
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
        /// <summary>Jobs already on this provider's diary, with their locations.</summary>
        public IReadOnlyCollection<BusyJob> Busy { get; init; } = [];
    }

    public static Check CanBook(Request req)
    {
        var o = req.Offering;

        if (req.StartsAt - req.Now < MinimumNotice)
            return Check.Fail(Refusal.TooSoon,
                $"Cần đặt trước ít nhất {MinimumNotice.TotalHours:0} giờ.");

        var hour = req.StartsAt.Hour;
        var endHour = req.StartsAt.AddMinutes(o.DurationMinutes).Hour;
        if (hour < o.OpensAtHour || hour >= o.ClosesAtHour || (endHour > o.ClosesAtHour && endHour != 0))
            return Check.Fail(Refusal.OutsideHours,
                $"Dịch vụ này chỉ nhận từ {o.OpensAtHour}:00 đến {o.ClosesAtHour}:00.");

        if (req.Quantity < o.MinQuantity || req.Quantity > o.MaxQuantity)
            return Check.Fail(Refusal.QuantityOutOfRange,
                $"Nhận từ {o.MinQuantity} đến {o.MaxQuantity} {UnitLabel(o.Pricing)}.");

        if (o.TravelsToGuest)
        {
            if (string.IsNullOrWhiteSpace(req.Address))
                return Check.Fail(Refusal.NoAddress, "Cần địa chỉ nơi thực hiện dịch vụ.");

            if (req.Latitude is { } lat && req.Longitude is { } lng)
            {
                var km = DistanceKm(o.Latitude, o.Longitude, lat, lng);
                if (km > o.ServiceRadiusKm)
                    return Check.Fail(Refusal.OutOfRange,
                        $"Ngoài phạm vi phục vụ {o.ServiceRadiusKm} km (cách khoảng {km:0} km).");
            }
        }

        var from = req.StartsAt;
        var to = req.StartsAt.AddMinutes(o.DurationMinutes);

        // Where the provider is for this job: at the guest's address if they travel,
        // otherwise at their own premises.
        var hereLat = o.TravelsToGuest && req.Latitude is { } jlat ? jlat : o.Latitude;
        var hereLng = o.TravelsToGuest && req.Longitude is { } jlng ? jlng : o.Longitude;

        foreach (var b in req.Busy)
        {
            if (from < b.To && b.From < to)
                return Check.Fail(Refusal.AlreadyBooked, "Khung giờ này đã có người đặt.");

            // docs/09 §3.4 (scenario 8) — between two jobs the provider needs a
            // buffer plus the time to travel between the two places. A job 30
            // minutes but 20 km away is too tight to reach.
            var gap = from >= b.To ? from - b.To : b.From - to;
            var travel = (b.Lat != 0 || b.Lng != 0) && (hereLat != 0 || hereLng != 0)
                ? TravelTime(DistanceKm(hereLat, hereLng, b.Lat, b.Lng))
                : TimeSpan.Zero;

            if (gap < BufferBetweenJobs + travel)
                return Check.Fail(Refusal.TooTight,
                    "Quá sát một đơn khác — không đủ thời gian nghỉ và di chuyển giữa hai buổi.");
        }

        return Check.Pass;
    }

    /// <summary>Great-circle distance in kilometres.</summary>
    public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371;

        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        return earthRadiusKm * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;

    /// <summary>The mandatory note a category demands before a job can be booked.</summary>
    public enum NoteKind { None = 0, FoodAllergy, HealthConditions, FitnessInjuries }

    /// <summary>
    /// docs/09 §3.5 (scenario 10) — some categories cannot be booked without a
    /// note the provider needs for safety: a chef needs food allergies, massage
    /// needs health conditions and areas to avoid, a trainer needs goals and past
    /// injuries. Every other category leaves the note optional.
    /// </summary>
    public static NoteKind RequiredNote(string? category) => (category ?? "").Trim().ToLowerInvariant() switch
    {
        "chef" => NoteKind.FoodAllergy,
        "massage" => NoteKind.HealthConditions,
        "fitness" => NoteKind.FitnessInjuries,
        _ => NoteKind.None
    };

    public static string NoteLabel(NoteKind kind) => kind switch
    {
        NoteKind.FoodAllergy => "Dị ứng thực phẩm",
        NoteKind.HealthConditions => "Tình trạng sức khoẻ và vùng cần tránh",
        NoteKind.FitnessInjuries => "Mục tiêu tập luyện và chấn thương cũ",
        _ => ""
    };

    /// <summary>Whether this booking is missing a note its category makes mandatory.</summary>
    public static bool NoteMissing(string? category, string? note) =>
        RequiredNote(category) != NoteKind.None && string.IsNullOrWhiteSpace(note);

    public static decimal GuestRefund(ServiceBooking booking, DateTime now) =>
        booking.StartsAt - now >= FreeCancellation ? booking.Total : 0m;

    public static string UnitLabel(ServicePricing pricing) => pricing switch
    {
        ServicePricing.PerHour => "giờ",
        ServicePricing.PerPerson => "người",
        ServicePricing.PerOrder => "phần",
        _ => "buổi"
    };

    public static string PricingLabel(ServicePricing pricing) => pricing switch
    {
        ServicePricing.PerHour => "Tính theo giờ",
        ServicePricing.PerPerson => "Tính theo người",
        ServicePricing.PerOrder => "Tính theo phần",
        _ => "Trọn buổi"
    };

    public static string StatusLabel(ServiceBookingStatus status) => status switch
    {
        ServiceBookingStatus.Requested => "Chờ xác nhận",
        ServiceBookingStatus.Confirmed => "Đã xác nhận",
        ServiceBookingStatus.Completed => "Đã hoàn tất",
        ServiceBookingStatus.CancelledByGuest => "Khách đã huỷ",
        _ => "Bên cung cấp đã huỷ"
    };

    public static string StatusBadge(ServiceBookingStatus status) => status switch
    {
        ServiceBookingStatus.Confirmed or ServiceBookingStatus.Completed => "confirmed",
        ServiceBookingStatus.Requested => "pending",
        _ => "cancelled"
    };
}
