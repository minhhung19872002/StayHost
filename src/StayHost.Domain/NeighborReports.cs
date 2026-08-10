namespace StayHost.Domain;

/// <summary>
/// docs/01 AT-03 — a channel for a neighbour to report a problem with a nearby
/// short-let, without needing an account. A neighbour rarely knows which listing
/// it is, so this is keyed by a place they describe (an address or area) rather
/// than a listing id; contact details are optional, because the point is to lower
/// the bar to reporting a genuine safety or nuisance concern.
/// </summary>
public class NeighborReport
{
    public int Id { get; set; }

    /// <summary>Where it is happening, in the neighbour's own words.</summary>
    public string Location { get; set; } = "";
    public NeighborConcern Category { get; set; }
    public string Detail { get; set; } = "";

    /// <summary>Optional — a neighbour may want to stay anonymous.</summary>
    public string? Contact { get; set; }

    /// <summary>The reporter's session, only to rate-limit; never shown as identity.</summary>
    public string SessionId { get; set; } = "";

    public ReportStatus Status { get; set; } = ReportStatus.Open;
    public string? Resolution { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}

public enum NeighborConcern
{
    Noise = 0,
    Party = 1,
    Safety = 2,
    Parking = 3,
    Waste = 4,
    Other = 5
}

/// <summary>docs/01 AT-03 — the pure rules for a neighbour report.</summary>
public static class NeighborReports
{
    public static readonly IReadOnlyList<(NeighborConcern Concern, string Label)> Concerns =
    [
        (NeighborConcern.Noise, "Tiếng ồn"),
        (NeighborConcern.Party, "Tụ tập, tiệc tùng"),
        (NeighborConcern.Safety, "An toàn, an ninh"),
        (NeighborConcern.Parking, "Đỗ xe, lối đi"),
        (NeighborConcern.Waste, "Rác thải, vệ sinh"),
        (NeighborConcern.Other, "Vấn đề khác")
    ];

    public const int LocationMin = 5;
    public const int DetailMin = 15;
    public const int DetailMax = 2000;

    public static bool TryParseConcern(string? value, out NeighborConcern concern) =>
        Enum.TryParse(value, true, out concern) && Enum.IsDefined(concern);

    public static string ConcernLabel(NeighborConcern concern) =>
        Concerns.FirstOrDefault(c => c.Concern == concern).Label ?? "Vấn đề khác";

    /// <summary>Null when the report is good to file, otherwise the reason it is not.</summary>
    public static string? Validate(string? location, string? detail)
    {
        if ((location ?? "").Trim().Length < LocationMin)
            return "Vui lòng cho biết địa chỉ hoặc khu vực (tối thiểu 5 ký tự).";
        if ((detail ?? "").Trim().Length < DetailMin)
            return "Vui lòng mô tả sự việc rõ hơn (tối thiểu 15 ký tự).";
        return null;
    }
}
