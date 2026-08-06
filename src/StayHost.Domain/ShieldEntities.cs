namespace StayHost.Domain;

/// <summary>
/// One StayShield case. Never deleted; the decision and the money that moved
/// are part of the record (docs/06 §6).
/// </summary>
public class ShieldClaim
{
    public int Id { get; set; }
    public string Reference { get; set; } = "";

    public int BookingId { get; set; }
    public Booking? Booking { get; set; }

    /// <summary>Who opened it.</summary>
    public int OpenedByUserId { get; set; }
    public User? OpenedByUser { get; set; }

    public ShieldSide Side { get; set; }
    public ShieldCase Kind { get; set; }
    public ShieldStatus Status { get; set; } = ShieldStatus.Open;

    public string Description { get; set; } = "";

    /// <summary>What the opener asked for. Zero on a guest case where the remedy decides it.</summary>
    public decimal Claimed { get; set; }

    /// <summary>Out-of-pocket travel and one emergency night (docs/06 §2.3, K1 and K2 only).</summary>
    public decimal ExpensesClaimed { get; set; }

    /// <summary>What a replacement stay cost above the original (docs/06 §2.3 levels 1–2).</summary>
    public decimal RehousingDifference { get; set; }

    public ShieldRemedy Remedy { get; set; } = ShieldRemedy.None;

    // --- docs/06 §3.1 C4: the person or body the damage was actually done to.
    /// <summary>Neighbour, building management, or somebody else entirely.</summary>
    public string? ThirdPartyName { get; set; }
    public string? ThirdPartyContact { get; set; }
    /// <summary>neighbour · building · other</summary>
    public string? ThirdPartyKind { get; set; }

    // --- what was actually decided
    public decimal Approved { get; set; }
    public decimal Deductible { get; set; }
    public decimal CreditGranted { get; set; }
    public decimal PaidFromFund { get; set; }
    public decimal RecoveredFromCounterparty { get; set; }
    public decimal RecoveredLater { get; set; }

    public string? Decision { get; set; }
    public int? DecidedByUserId { get; set; }
    public User? DecidedByUser { get; set; }
    public DateTime? DecidedAt { get; set; }

    /// <summary>docs/06 §6 — one appeal, decided by somebody else.</summary>
    public bool Appealed { get; set; }
    public int? AppealReviewerUserId { get; set; }

    /// <summary>docs/06 §7 — flagged accounts never settle themselves.</summary>
    public bool NeedsManualReview { get; set; }

    /// <summary>When the other side's 24 hours run out (docs/06 §6).</summary>
    public DateTime RespondBy { get; set; }

    /// <summary>The two promises of docs/06 §6, stored so a dashboard can show what is late.</summary>
    public DateTime FirstResponseDueAt { get; set; }
    public DateTime DecisionDueAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SettledAt { get; set; }

    public List<ShieldEvidence> Evidence { get; set; } = [];
    public List<ShieldEvent> Events { get; set; } = [];
    public List<ShieldItem> Items { get; set; } = [];
}

/// <summary>A photo, a video or a receipt. docs/06 §3.5 makes these compulsory.</summary>
public class ShieldEvidence
{
    public int Id { get; set; }
    public int ClaimId { get; set; }
    public ShieldClaim? Claim { get; set; }

    public string Url { get; set; } = "";
    public string? Caption { get; set; }

    /// <summary>photo · video · receipt · quote · listing-photo</summary>
    public string Kind { get; set; } = "photo";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>One damaged thing on a host's case, priced on its own (docs/06 §3.5).</summary>
public class ShieldItem
{
    public int Id { get; set; }
    public int ClaimId { get; set; }
    public ShieldClaim? Claim { get; set; }

    public string Name { get; set; } = "";
    public decimal Value { get; set; }

    /// <summary>docs/06 §3.2 C-E — declared on the listing before the guest arrived.</summary>
    public bool DeclaredOnListing { get; set; }

    /// <summary>What the ceiling let through.</summary>
    public decimal Allowed { get; set; }
}

/// <summary>Append-only history of a case, exactly like a booking's (docs/00 §6.2).</summary>
public class ShieldEvent
{
    public int Id { get; set; }
    public int ClaimId { get; set; }
    public ShieldClaim? Claim { get; set; }

    public ShieldStatus? FromStatus { get; set; }
    public ShieldStatus ToStatus { get; set; }

    /// <summary>guest:12 · host:5 · admin:1 · system</summary>
    public string Actor { get; set; } = "";
    public string Note { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// docs/06 §5 — money into and out of the fund, month by month. Append-only,
/// so the monthly report is a query rather than a stored figure that drifted.
/// </summary>
public enum FundMovementKind
{
    /// <summary>The monthly share of service-fee revenue.</summary>
    Contribution = 0,
    /// <summary>Paid out on a case.</summary>
    Payout = 1,
    /// <summary>Got back from whoever was responsible, after the fund had already paid.</summary>
    Recovery = 2
}

public class ShieldFundMovement
{
    public long Id { get; set; }

    public FundMovementKind Kind { get; set; }

    /// <summary>Positive in, negative out.</summary>
    public decimal Amount { get; set; }

    public int? ClaimId { get; set; }
    public ShieldClaim? Claim { get; set; }

    public string Memo { get; set; } = "";

    /// <summary>The month this belongs to, always the first of it.</summary>
    public DateOnly Period { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
