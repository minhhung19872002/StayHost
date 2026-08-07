namespace StayHost.Domain;

/// <summary>
/// docs/01 CĐ-03 and CĐ-04, docs/03 §10 — the arrival guide, and which part of
/// it a given guest may read at a given moment.
///
/// The gate matters more than the content. An exact address and a door code are
/// the two things that turn a listing into a way into somebody's home, so they
/// are not "details shown on the trip page": they are released, to one guest,
/// once the stay is real, and the code only as the stay comes up.
/// </summary>
public static class CheckInGuide
{
    /// <summary>docs/03 §10 — how long before check-in the door code appears.</summary>
    public static readonly TimeSpan DoorCodeWindow = TimeSpan.FromHours(48);

    /// <summary>Wifi names and appliance notes are prose, not essays.</summary>
    public const int NoteMax = 1000;
    public const int LineMax = 120;

    /// <summary>
    /// docs/03 §10 — a stay that is confirmed or under way. A request still
    /// waiting on the host is not one, and neither is a cancelled stay: the
    /// address does not stay readable after somebody cancels.
    /// </summary>
    public static bool IsLive(BookingStatus status) =>
        status is BookingStatus.Confirmed or BookingStatus.InProgress or BookingStatus.Completed;

    /// <summary>
    /// docs/01 CĐ-03 — the guide, the exact address and the host's phone. Held
    /// back until the booking is confirmed (docs/03 §10).
    /// </summary>
    public static bool CanSeeGuide(BookingStatus status) => IsLive(status);

    /// <summary>The instant the code becomes readable: 48 hours before check-in opens.</summary>
    public static DateTime DoorCodeVisibleFrom(DateOnly checkIn, TimeOnly checkInFrom) =>
        checkIn.ToDateTime(checkInFrom) - DoorCodeWindow;

    /// <summary>
    /// docs/01 CĐ-04 — the code needs both gates: a live booking, and the last
    /// 48 hours. <paramref name="localNow"/> is the listing's own wall clock
    /// (docs/03 §3), not the guest's and not the server's.
    /// </summary>
    public static bool CanSeeDoorCode(
        BookingStatus status, DateOnly checkIn, TimeOnly checkInFrom, DateTime localNow)
    {
        if (!IsLive(status)) return false;

        // Once somebody is inside, the code stays readable — a guest who locked
        // themselves out on night three should not be told to come back later.
        if (localNow >= checkIn.ToDateTime(checkInFrom)) return true;

        return localNow >= DoorCodeVisibleFrom(checkIn, checkInFrom);
    }

    /// <summary>What to tell somebody who is looking at the guide too early.</summary>
    public static string DoorCodeWaitNote(DateOnly checkIn, TimeOnly checkInFrom) =>
        $"Mã cửa hiện từ {DoorCodeVisibleFrom(checkIn, checkInFrom):HH:mm dd/MM} " +
        "— 48 giờ trước giờ nhận phòng.";

    /// <summary>
    /// docs/01 CĐ-03 — "Nhận phòng 14:00 – 22:00 · Trả phòng trước 12:00".
    /// One place, so the trip page, the listing page and the house rules agree.
    /// </summary>
    public static string WindowLabel(TimeOnly from, TimeOnly to, TimeOnly checkOutBefore) =>
        $"Nhận phòng {from:HH\\:mm} – {to:HH\\:mm} · Trả phòng trước {checkOutBefore:HH\\:mm}";

    /// <summary>
    /// "14:00" as an &lt;input type="time"&gt; hands it back, or "14:00:00" as
    /// some browsers do. Anything else keeps the time the listing already had:
    /// a garbled field should not silently move check-in to midnight.
    /// </summary>
    public static TimeOnly ParseTime(string? raw, TimeOnly fallback) =>
        TimeOnly.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    public static string MethodLabel(CheckInMethod method) => method switch
    {
        CheckInMethod.Keypad => "Tự nhận phòng bằng mã cửa",
        CheckInMethod.Lockbox => "Tự nhận phòng bằng hộp khoá",
        CheckInMethod.SmartLock => "Tự nhận phòng bằng khoá thông minh",
        CheckInMethod.Reception => "Nhận chìa khoá ở quầy lễ tân",
        _ => "Chủ nhà đón và giao chìa khoá"
    };

    /// <summary>The appliance notes as the host typed them, one instruction per line.</summary>
    public static IReadOnlyList<string> Lines(string? notes) =>
        string.IsNullOrWhiteSpace(notes)
            ? []
            : notes.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>True for the ways in that need a code to be of any use.</summary>
    public static bool NeedsDoorCode(CheckInMethod method) =>
        method is CheckInMethod.Keypad or CheckInMethod.Lockbox or CheckInMethod.SmartLock;
}

/// <summary>docs/01 CĐ-03 — how a guest gets in.</summary>
public enum CheckInMethod
{
    /// <summary>The host or somebody for them hands the keys over.</summary>
    Host = 0,
    /// <summary>docs/01 CĐ-04 — a keypad, which is what the door code is for.</summary>
    Keypad = 1,
    Lockbox = 2,
    /// <summary>A front desk or building reception.</summary>
    Reception = 3,
    SmartLock = 4
}
