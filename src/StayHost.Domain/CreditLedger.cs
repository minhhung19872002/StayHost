namespace StayHost.Domain;

/// <summary>
/// docs/01 TC-07 — how long a grant of balance lasts before it lapses.
///
/// Null means "never lapses". The numbers themselves are not decided here — they
/// come from `docs/07 §16`, which the customer settled on 11/08/2026 at twelve
/// months for the three promotional kinds, and appsettings.json carries them.
/// Expiry takes money off guests, so the number belongs where the decision is
/// recorded rather than compiled in.
///
/// A gift card is listed separately because it is the one grant somebody paid
/// real money for. Expiring it is closer to keeping their payment than to
/// retiring a promotion, so it stays on its own line rather than sharing a
/// number with the others — and §16 duly left it at "never".
/// </summary>
public sealed record CreditSettings
{
    public int? GiftCardMonths { get; init; }
    public int? GoodwillMonths { get; init; }
    public int? ReferralMonths { get; init; }

    /// <summary>
    /// Balance handed back when a booking it paid for was cancelled. It starts a
    /// fresh life rather than resuming the one it came from: the run of entries
    /// does not record which grant a spend drew on, so the alternative would be
    /// to guess, and guessing short takes money the guest never spent.
    /// </summary>
    public int? ReturnedMonths { get; init; }

    public static CreditSettings Current { get; set; } = new();

    public int? MonthsFor(CreditReason reason) => reason switch
    {
        CreditReason.GiftCard => GiftCardMonths,
        CreditReason.Goodwill => GoodwillMonths,
        CreditReason.Referral => ReferralMonths,
        CreditReason.Returned => ReturnedMonths,
        _ => null
    };

    /// <summary>Null when this kind of grant does not lapse at all.</summary>
    public DateTime? ExpiryFor(CreditReason reason, DateTime granted) =>
        MonthsFor(reason) is { } months && months > 0 ? granted.AddMonths(months) : null;

    /// <summary>True when nothing on the platform lapses, so no sweep is needed.</summary>
    public bool NothingExpires =>
        GiftCardMonths is null && GoodwillMonths is null
        && ReferralMonths is null && ReturnedMonths is null;
}

/// <summary>What is left of one grant, after everything spent against it.</summary>
public sealed record CreditLot(
    long EntryId, decimal Granted, decimal Remaining, DateTime? ExpiresAt, DateTime GrantedAt)
{
    public bool HasLapsedBy(DateTime moment) => ExpiresAt is { } x && x <= moment;
}

/// <summary>
/// docs/01 TC-07 and docs/07 §3 — reading an append-only run of balance entries
/// as the grants behind it.
///
/// The rows never say which grant a spend drew on, and they are not going to
/// start: the balance is the sum of its rows and that is what makes it
/// explainable. So the attribution is derived instead, by replaying the run in
/// the order it was written under one rule — a spend takes from whichever live
/// grant lapses soonest. That rule is `docs/07 §3` line 3, and applying it at
/// read time is what makes the same history always produce the same answer.
/// </summary>
public static class CreditLedger
{
    /// <summary>
    /// The one place a movement of balance is built.
    ///
    /// docs/01 TC-07 stamps a grant's lifetime at the moment it is made, so a
    /// caller that hand-rolls the row instead skips it — and the balance then
    /// never lapses, whatever `docs/07 §16` says. That is not hypothetical: the
    /// refund path in BookingsController built its own row and returned balance
    /// outlived every rule for it. Going through here is what makes the setting
    /// mean the same thing everywhere.
    ///
    /// Only a positive amount carries an expiry; a spend or a sweep row is not a
    /// grant and has nothing to lapse.
    /// </summary>
    public static CreditEntry Grant(
        int userId, decimal amount, CreditReason reason, string memo, DateTime now, int? bookingId = null) =>
        new()
        {
            UserId = userId,
            Amount = amount,
            Reason = reason,
            Memo = memo,
            BookingId = bookingId,
            CreatedAt = now,
            ExpiresAt = amount > 0 ? CreditSettings.Current.ExpiryFor(reason, now) : null
        };

    /// <summary>
    /// What remains of each grant. Order is the order the grants were made.
    /// </summary>
    public static IReadOnlyList<CreditLot> Lots(IEnumerable<CreditEntry> entries)
    {
        var open = new List<Open>();

        foreach (var entry in entries.OrderBy(e => e.CreatedAt).ThenBy(e => e.Id))
        {
            if (entry.Amount > 0)
            {
                open.Add(new Open(entry.Id, entry.Amount, entry.ExpiresAt, entry.CreatedAt));
                continue;
            }

            var wanted = -entry.Amount;
            if (wanted <= 0) continue;

            // A sweep row retires what has already lapsed; every other kind of
            // withdrawal may only touch what was still live when it was written.
            var lapsedOnly = entry.Reason == CreditReason.Expired;
            wanted = Draw(open, wanted, l => l.HasLapsed(entry.CreatedAt) == lapsedOnly);

            // Nothing in the matching half covered it. Rather than let the lots
            // stop adding up to the balance, take from whatever is left: rows
            // written before this column existed have no expiry to reason about,
            // and a total that disagrees with the sum of the rows would be a
            // worse answer than a slightly arbitrary attribution.
            if (wanted > 0) Draw(open, wanted, _ => true);
        }

        return open
            .Select(l => new CreditLot(l.EntryId, l.Granted, l.Remaining, l.ExpiresAt, l.GrantedAt))
            .ToList();
    }

    /// <summary>
    /// What the guest can actually spend right now: everything still live,
    /// whether or not the sweep has caught up with what is not.
    /// </summary>
    public static decimal Available(IEnumerable<CreditEntry> entries, DateTime now) =>
        Lots(entries).Where(l => !l.HasLapsedBy(now)).Sum(l => l.Remaining);

    /// <summary>
    /// Lapsed but not yet retired by the sweep. Never spendable — Available
    /// already leaves it out — so this exists to be swept and to be explained.
    /// </summary>
    public static decimal Lapsed(IEnumerable<CreditEntry> entries, DateTime now) =>
        Lots(entries).Where(l => l.HasLapsedBy(now)).Sum(l => l.Remaining);

    /// <summary>The next date the guest loses something, so they can be told.</summary>
    public static DateTime? NextExpiry(IEnumerable<CreditEntry> entries, DateTime now) =>
        Lots(entries)
            .Where(l => l.Remaining > 0 && l.ExpiresAt is { } x && x > now)
            .Min(l => l.ExpiresAt);

    /// <summary>How much lapses on that next date.</summary>
    public static decimal ExpiringOn(IEnumerable<CreditEntry> entries, DateTime when) =>
        Lots(entries).Where(l => l.ExpiresAt == when).Sum(l => l.Remaining);

    /// <summary>
    /// The rows the sweep should write: one per lapsed grant with something left
    /// on it. Returning the grants rather than a single total keeps each row
    /// pointing at the promotion it retired.
    /// </summary>
    public static IReadOnlyList<CreditLot> DueToExpire(IEnumerable<CreditEntry> entries, DateTime now) =>
        Lots(entries).Where(l => l.Remaining > 0 && l.HasLapsedBy(now)).ToList();

    private static decimal Draw(List<Open> open, decimal wanted, Func<Open, bool> eligible)
    {
        var order = open
            .Where(l => l.Remaining > 0 && eligible(l))
            // docs/07 §3 — soonest to lapse goes first; grants that never lapse
            // wait until last, because they are the ones that can afford to.
            .OrderBy(l => l.ExpiresAt ?? DateTime.MaxValue)
            .ThenBy(l => l.GrantedAt)
            .ThenBy(l => l.EntryId)
            .ToList();

        foreach (var lot in order)
        {
            if (wanted <= 0) break;
            var take = Math.Min(wanted, lot.Remaining);
            lot.Remaining -= take;
            wanted -= take;
        }

        return wanted;
    }

    private sealed class Open(long entryId, decimal granted, DateTime? expiresAt, DateTime grantedAt)
    {
        public long EntryId { get; } = entryId;
        public decimal Granted { get; } = granted;
        public decimal Remaining { get; set; } = granted;
        public DateTime? ExpiresAt { get; } = expiresAt;
        public DateTime GrantedAt { get; } = grantedAt;

        public bool HasLapsed(DateTime moment) => ExpiresAt is { } x && x <= moment;
    }
}
