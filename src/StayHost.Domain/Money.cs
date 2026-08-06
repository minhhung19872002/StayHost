namespace StayHost.Domain;

/// <summary>
/// Platform-wide money knobs. docs/00 §6.8 says every money rule is defined once;
/// these are the numbers that rule depends on, kept in one settable place so the
/// admin console (QT-06) can change them without a deploy.
/// </summary>
public sealed record PricingSettings
{
    /// <summary>docs/03 §1 step 7 — charged to the guest on the subtotal, before tax.</summary>
    public decimal GuestServiceFeeRate { get; init; } = 0.14m;

    /// <summary>docs/03 §1 step 11 — withheld from the host's share of the subtotal.</summary>
    public decimal HostServiceFeeRate { get; init; } = 0.03m;

    /// <summary>docs/03 §1 — every discount added together may not exceed this.</summary>
    public int MaxDiscountPercent { get; init; } = 60;

    /// <summary>docs/03 §1 step 4 — a listing's first N stays carry the new-listing discount.</summary>
    public int NewListingBookingCount { get; init; } = 3;
    public int NewListingDiscountPercent { get; init; } = 20;

    /// <summary>Fallback cleaning fee used when a host leaves the field empty.</summary>
    public decimal DefaultCleaningFee { get; init; } = 350_000m;

    /// <summary>Replaced at startup from configuration; the defaults match the spec.</summary>
    public static PricingSettings Current { get; set; } = new();
}

/// <summary>
/// Who is staying. docs/03 §1 step 5 and §2 rule 2 both hinge on the fact that
/// infants count for neither capacity nor the extra-guest surcharge.
/// </summary>
public readonly record struct PartySize(int Adults, int Children = 0, int Infants = 0, int Pets = 0)
{
    /// <summary>Adults plus children — the number that capacity and surcharges use.</summary>
    public int Counted => Adults + Children;

    public static PartySize Of(int guests) => new(Math.Max(1, guests));
}

/// <summary>One displayed money row. Amounts are already rounded; negatives are reductions.</summary>
public sealed record PriceLine(string Key, string Label, decimal Amount);

public enum TaxMethod
{
    /// <summary>A percentage of the configured base.</summary>
    Percentage = 0,
    /// <summary>A flat amount for each night of the stay.</summary>
    PerNight = 1,
    /// <summary>A flat amount for each counted guest, each night.</summary>
    PerGuestPerNight = 2,
    /// <summary>A flat amount charged once for the whole stay.</summary>
    PerStay = 3
}

/// <summary>What a percentage tax is calculated on.</summary>
public enum TaxBase
{
    /// <summary>Room after discounts plus surcharges and the cleaning fee.</summary>
    Subtotal = 0,
    /// <summary>The subtotal plus the guest service fee, which step 7 computes first.</summary>
    SubtotalPlusGuestFee = 1,
    /// <summary>Room charges only; lodging levies often exclude cleaning.</summary>
    RoomOnly = 2
}

/// <summary>
/// A lodging tax for one region. docs/03 §1 step 8: a region can stack several,
/// and they are not all percentages.
/// </summary>
public class TaxRule
{
    public int Id { get; set; }

    public string Country { get; set; } = "Việt Nam";
    /// <summary>Null means the rule covers the whole country.</summary>
    public string? City { get; set; }

    public string Name { get; set; } = "";
    public TaxMethod Method { get; set; } = TaxMethod.Percentage;
    public TaxBase Base { get; set; } = TaxBase.SubtotalPlusGuestFee;
    /// <summary>A fraction for <see cref="TaxMethod.Percentage"/>, otherwise an amount.</summary>
    public decimal Value { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateOnly? EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }

    public bool AppliesOn(DateOnly night) =>
        IsActive
        && (EffectiveFrom is null || EffectiveFrom <= night)
        && (EffectiveTo is null || night <= EffectiveTo);
}
