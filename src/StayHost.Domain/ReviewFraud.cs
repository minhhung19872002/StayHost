namespace StayHost.Domain;

/// <summary>
/// docs/01 ĐG-11 — spotting reviews that look planted through secondary accounts.
///
/// A review here always sits on a real completed booking, so the fraud is not a
/// made-up review — it is a host booking their own place through an account they
/// control and leaving themselves five stars. This scores the tell-tale signals of
/// that. Like the discrimination screen (AT-12) it is a tripwire, not a verdict:
/// it hands a reviewer a risk level and the reasons, for a human to judge.
/// </summary>
public static class ReviewFraud
{
    public enum Risk { None, Low, High }

    /// <summary>Everything the score is drawn from, pre-computed so this stays pure.</summary>
    public readonly record struct Signals(
        bool SameAccountAsHost,       // the reviewer *is* the host
        bool SharedSessionWithHost,   // both accounts were created from one browser
        int ReviewerAccountAgeDays,   // age of the reviewer's account when they reviewed
        bool ReviewerOnlyBookedThisHost,
        int ReviewerStayCount,        // how many stays the reviewer has ever had
        double Rating);

    private const int NewAccountDays = 3;

    public readonly record struct Assessment(Risk Level, IReadOnlyList<string> Reasons)
    {
        public bool Flagged => Level != Risk.None;
    }

    public static Assessment Assess(Signals s)
    {
        var reasons = new List<string>();
        var high = false;

        if (s.SameAccountAsHost)
        {
            reasons.Add("Người đánh giá chính là chủ nhà.");
            high = true;
        }
        if (s.SharedSessionWithHost)
        {
            reasons.Add("Tài khoản người đánh giá và chủ nhà tạo từ cùng một trình duyệt.");
            high = true;
        }

        var newAccount = s.ReviewerAccountAgeDays >= 0 && s.ReviewerAccountAgeDays <= NewAccountDays;
        if (newAccount) reasons.Add($"Tài khoản mới lập ({s.ReviewerAccountAgeDays} ngày) khi đánh giá.");
        if (s.ReviewerOnlyBookedThisHost && s.ReviewerStayCount <= 1)
            reasons.Add("Người đánh giá chỉ từng ở đúng chỗ của chủ nhà này.");

        // A brand-new account, only ever staying with this one host, giving top
        // marks — the shape of a planted review even without a hard identity link.
        if (!high && newAccount && s.ReviewerOnlyBookedThisHost && s.Rating >= 4.5)
            high = true;

        if (high) return new Assessment(Risk.High, reasons);
        if (reasons.Count > 0) return new Assessment(Risk.Low, reasons);
        return new Assessment(Risk.None, reasons);
    }

    public static string RiskLabel(Risk risk) => risk switch
    {
        Risk.High => "Nguy cơ cao",
        Risk.Low => "Cần xem lại",
        _ => "Bình thường"
    };
}
