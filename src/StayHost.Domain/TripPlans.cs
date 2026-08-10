namespace StayHost.Domain;

/// <summary>
/// docs/01 CĐ-10, CĐ-11 — several bookings pulled into one trip with a day-by-day
/// itinerary, which the owner and any invited companions build together.
/// </summary>
public class TripPlan
{
    public int Id { get; set; }
    public int OwnerId { get; set; }
    public User? Owner { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<TripPlanBooking> Bookings { get; set; } = [];
    public List<TripPlanMember> Members { get; set; } = [];
    public List<TripItineraryItem> Items { get; set; } = [];
}

/// <summary>docs/01 CĐ-10 — a booking merged into a trip.</summary>
public class TripPlanBooking
{
    public int Id { get; set; }
    public int TripPlanId { get; set; }
    public TripPlan? TripPlan { get; set; }
    public int BookingId { get; set; }
    public Booking? Booking { get; set; }
}

/// <summary>docs/01 CĐ-11 — a companion invited to co-edit the itinerary.</summary>
public class TripPlanMember
{
    public int Id { get; set; }
    public int TripPlanId { get; set; }
    public TripPlan? TripPlan { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>docs/01 CĐ-10/CĐ-11 — one line of the day-by-day plan.</summary>
public class TripItineraryItem
{
    public int Id { get; set; }
    public int TripPlanId { get; set; }
    public TripPlan? TripPlan { get; set; }
    public DateOnly Day { get; set; }
    public string Title { get; set; } = "";
    public string? Note { get; set; }
    public int AddedByUserId { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>docs/01 CĐ-10, CĐ-11 — the pure rules for who may see and edit a trip.</summary>
public static class TripPlans
{
    public const int NameMax = 120;
    public const int TitleMax = 200;
    public const int NoteMax = 1000;

    /// <summary>The owner and every invited companion may see and add to the trip.</summary>
    public static bool CanEdit(int ownerId, IEnumerable<int> memberIds, int userId) =>
        userId == ownerId || memberIds.Contains(userId);

    /// <summary>Only the owner manages membership and which bookings belong to the trip.</summary>
    public static bool IsOwner(int ownerId, int userId) => ownerId == userId;

    public static string? ValidateItem(string? title)
    {
        var t = (title ?? "").Trim();
        if (t.Length < 2) return "Tên địa điểm cần tối thiểu 2 ký tự.";
        if (t.Length > TitleMax) return "Tên địa điểm quá dài.";
        return null;
    }
}
