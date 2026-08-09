namespace StayHost.Domain;

/// <summary>
/// docs/01 ĐG-12 — a public note left on a listing when its host cancels a
/// confirmed stay. It is not a review: it carries no rating and never touches the
/// score (a host cancelling is already penalised in ranking, docs/03 §6). It is
/// transparency for the next guest, the way an airline shows a cancelled flight.
/// </summary>
public class ListingCancellationNote
{
    public int Id { get; set; }

    public int ListingId { get; set; }
    public Listing? Listing { get; set; }

    /// <summary>The public sentence shown on the listing.</summary>
    public string Note { get; set; } = "";

    /// <summary>How many days before check-in the host pulled out; for sorting/context.</summary>
    public int DaysBeforeCheckIn { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class CancellationNotes
{
    /// <summary>How long a note stays visible. A cancellation two years ago is not news.</summary>
    public static readonly TimeSpan Visibility = TimeSpan.FromDays(365);

    public static int DaysBefore(DateOnly checkIn, DateTime cancelledAtUtc) =>
        Math.Max(0, checkIn.DayNumber - DateOnly.FromDateTime(cancelledAtUtc).DayNumber);

    /// <summary>The public sentence, phrased by how close to arrival it was.</summary>
    public static string Compose(int daysBefore) => daysBefore switch
    {
        0 => "Chủ nhà đã huỷ một đơn đặt vào đúng ngày nhận phòng.",
        1 => "Chủ nhà đã huỷ một đơn đặt một ngày trước khi nhận phòng.",
        _ => $"Chủ nhà đã huỷ một đơn đặt {daysBefore} ngày trước khi nhận phòng."
    };

    public static bool IsVisible(ListingCancellationNote note, DateTime now) =>
        now - note.CreatedAt <= Visibility;
}
