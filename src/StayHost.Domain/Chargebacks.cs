namespace StayHost.Domain;

public enum ChargebackStatus
{
    /// <summary>The bank has told us and taken the money back. The clock starts here.</summary>
    Received = 0,
    /// <summary>Evidence sent, waiting on the bank.</summary>
    Contested = 1,
    /// <summary>The bank found for us; the money came back.</summary>
    Won = 2,
    /// <summary>The bank found for the guest. The platform wears it unless the host was at fault.</summary>
    Lost = 3,
    /// <summary>Nobody answered in time. Counts as lost.</summary>
    Expired = 4
}

/// <summary>
/// docs/07 §11 — a guest has told their bank the charge was wrong, and the bank
/// has taken the money back off the platform while it decides.
/// </summary>
public class Chargeback
{
    public int Id { get; set; }

    public int BookingId { get; set; }
    public Booking? Booking { get; set; }

    public decimal Amount { get; set; }
    public string Reason { get; set; } = "";

    public ChargebackStatus Status { get; set; } = ChargebackStatus.Received;

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
    public DateTime? DecidedAt { get; set; }

    /// <summary>What was sent to the bank, as a note for whoever picks this up next.</summary>
    public string? Evidence { get; set; }

    /// <summary>
    /// docs/07 §11 — set when arbitration found the host at fault. Only then does
    /// the loss come out of the host's money rather than the platform's.
    /// </summary>
    public bool HostAtFault { get; set; }
}

/// <summary>docs/07 §11 — the clock, and who ends up paying.</summary>
public static class Chargebacks
{
    /// <summary>"Tập hợp bằng chứng trong 7 ngày."</summary>
    public const int EvidenceDays = 7;

    public static DateTime EvidenceDueBy(DateTime receivedAt) => receivedAt.AddDays(EvidenceDays);

    public static bool EvidenceOverdue(Chargeback c, DateTime now) =>
        c.Status == ChargebackStatus.Received && now > EvidenceDueBy(c.ReceivedAt);

    /// <summary>What the platform should be gathering, from docs/07 §11 step 3.</summary>
    public static IReadOnlyList<string> EvidenceChecklist =>
    [
        "Lịch sử đặt đơn và các mốc thời gian",
        "Tin nhắn giữa khách và chủ nhà",
        "Xác nhận khách đã nhận phòng",
        "Chính sách huỷ đã hiển thị lúc đặt",
        "Hoá đơn và các khoản đã thu"
    ];

    /// <summary>
    /// docs/07 §11 — a case still running keeps the host's money where it is.
    /// A decided one, either way, stops holding it.
    /// </summary>
    public static bool HoldsPayout(ChargebackStatus status) =>
        status is ChargebackStatus.Received or ChargebackStatus.Contested;

    /// <summary>
    /// docs/07 §11 — "Chủ nhà không bị mất tiền vì khiếu nại của khách, trừ khi
    /// phân xử cho thấy lỗi thuộc về chủ nhà."
    /// </summary>
    public static bool HostBearsLoss(Chargeback c) =>
        c.Status is ChargebackStatus.Lost or ChargebackStatus.Expired && c.HostAtFault;

    public static bool PlatformBearsLoss(Chargeback c) =>
        c.Status is ChargebackStatus.Lost or ChargebackStatus.Expired && !c.HostAtFault;

    /// <summary>
    /// docs/07 §11 step 6 — a guest who keeps doing this is flagged. Two is a
    /// coincidence worth watching; the rule is about a pattern, not an accident.
    /// </summary>
    public const int SuspiciousCount = 2;

    public static bool GuestNeedsWatching(int chargebacksLost) => chargebacksLost >= SuspiciousCount;

    public static string StatusLabel(ChargebackStatus status) => status switch
    {
        ChargebackStatus.Received => "Mới nhận, đang thu thập bằng chứng",
        ChargebackStatus.Contested => "Đã gửi bằng chứng, chờ ngân hàng",
        ChargebackStatus.Won => "Ngân hàng xử thắng, tiền đã quay lại",
        ChargebackStatus.Lost => "Ngân hàng xử thua",
        _ => "Quá hạn phản hồi, tính như xử thua"
    };
}
