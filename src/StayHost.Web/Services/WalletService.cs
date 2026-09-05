using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;

namespace StayHost.Web.Services;

/// <summary>
/// Gift cards, balance and referrals. A guest's balance is the sum of an
/// append-only run of entries, the same shape as the money ledger, so it can
/// always be explained line by line.
/// </summary>
public class WalletService(StayHostDbContext db, NotificationService notifications, ILogger<WalletService> log)
{
    /// <summary>
    /// What the guest can spend. docs/01 TC-07 — a grant that has lapsed is still
    /// a row in the run, so the sum of the rows is not the answer on its own; the
    /// lapsed part is left out here rather than waiting for the sweep to retire
    /// it, or the balance would be spendable for up to an hour after it expired.
    /// </summary>
    public async Task<decimal> BalanceAsync(int userId, CancellationToken ct)
    {
        var entries = await db.CreditEntries.Where(c => c.UserId == userId).ToListAsync(ct);
        return CreditLedger.Available(entries, DateTime.UtcNow);
    }

    /// <summary>
    /// Adds a movement without saving; the caller commits it with the rest. A
    /// positive amount is a grant, so it picks up whatever lifetime its kind
    /// carries under docs/07 §16 — twelve months for the promotional kinds, none
    /// for a gift card. The lifetime is stamped here, at the moment of the grant,
    /// which is why changing the setting later cannot reach back and expire
    /// balance a guest was already holding.
    /// </summary>
    public void Add(int userId, decimal amount, CreditReason reason, string memo, int? bookingId = null)
    {
        if (amount == 0) return;

        db.CreditEntries.Add(
            CreditLedger.Grant(userId, amount, reason, memo, DateTime.UtcNow, bookingId));
    }

    /// <summary>
    /// docs/01 TC-07 — retires grants that have lapsed, one row per grant so the
    /// guest's history says which promotion ended rather than showing a single
    /// unexplained subtraction. Runs from the same sweep as the rest of the
    /// lifecycle work, and does nothing at all while no kind of grant expires.
    /// </summary>
    public async Task<int> ExpireLapsedCreditAsync(CancellationToken ct)
    {
        if (CreditSettings.Current.NothingExpires) return 0;

        var now = DateTime.UtcNow;

        // Only users holding something that has already lapsed are worth loading.
        var candidates = await db.CreditEntries
            .Where(c => c.ExpiresAt != null && c.ExpiresAt <= now)
            .Select(c => c.UserId)
            .Distinct()
            .ToListAsync(ct);

        var written = 0;

        foreach (var userId in candidates)
        {
            var entries = await db.CreditEntries.Where(c => c.UserId == userId).ToListAsync(ct);

            foreach (var lot in CreditLedger.DueToExpire(entries, now))
            {
                db.CreditEntries.Add(new CreditEntry
                {
                    UserId = userId,
                    Amount = -lot.Remaining,
                    Reason = CreditReason.Expired,
                    Memo = $"Hết hạn sử dụng {lot.ExpiresAt:dd/MM/yyyy}",
                    CreatedAt = now
                });
                written++;
            }
        }

        if (written > 0)
        {
            await db.SaveChangesAsync(ct);
            log.LogInformation("Retired {Count} lapsed credit grant(s).", written);
        }

        return written;
    }

    /* -------------------------------------------------------- gift cards */


    public async Task<(decimal Added, string? Error)> RedeemAsync(User user, string? code, CancellationToken ct)
    {
        var trimmed = (code ?? "").Trim().ToUpperInvariant();
        if (trimmed.Length == 0) return (0m, "Nhập mã thẻ quà tặng.");

        var card = await db.GiftCards.FirstOrDefaultAsync(c => c.Code == trimmed, ct);
        if (card is null) return (0m, "Không tìm thấy mã này.");
        if (!CreditRules.CanRedeem(card)) return (0m, $"Thẻ này {CreditRules.StatusLabel(card.Status).ToLower()}.");

        var amount = card.Remaining;

        card.Remaining = 0;
        card.Status = GiftCardStatus.Redeemed;
        card.RedeemedByUserId = user.Id;
        card.RedeemedAt = DateTime.UtcNow;

        Add(user.Id, amount, CreditReason.GiftCard, $"Đổi thẻ {card.Code}");
        db.LedgerEntries.AddRange(Ledger.RedeemGiftCard(amount, card.Code, DateTime.UtcNow));

        await db.SaveChangesAsync(ct);
        return (amount, null);
    }

    /* --------------------------------------------------------- referrals */

    public async Task<Referral> InviteAsync(User referrer, string email, CancellationToken ct)
    {
        var trimmed = email.Trim().ToLowerInvariant();

        var existing = await db.Referrals
            .FirstOrDefaultAsync(r => r.ReferrerUserId == referrer.Id && r.InviteeEmail == trimmed, ct);
        if (existing is not null) return existing;

        var referral = new Referral
        {
            ReferrerUserId = referrer.Id,
            Code = await UniqueCodeAsync("RF", ct),
            InviteeEmail = trimmed,
            ReferrerReward = CreditRules.ReferrerReward,
            InviteeReward = CreditRules.InviteeReward
        };

        db.Referrals.Add(referral);

        db.EmailMessages.Add(new EmailMessage
        {
            ToEmail = trimmed,
            ToName = trimmed,
            Subject = $"{referrer.FullName} mời bạn dùng Staylio",
            Body = $"Đăng ký bằng mã {referral.Code} và bạn được {CreditRules.InviteeReward:#,##0}₫ " +
                   "vào số dư sau chuyến đi đầu tiên."
        });

        await db.SaveChangesAsync(ct);
        return referral;
    }

    /// <summary>Called when someone signs up: links the account to whoever invited them.</summary>
    public async Task ClaimAsync(User newUser, string? code, CancellationToken ct)
    {
        var trimmed = (code ?? "").Trim().ToUpperInvariant();

        var referral = trimmed.Length > 0
            ? await db.Referrals.FirstOrDefaultAsync(r => r.Code == trimmed && r.InviteeUserId == null, ct)
            : await db.Referrals.FirstOrDefaultAsync(
                r => r.InviteeEmail == newUser.Email.ToLower() && r.InviteeUserId == null, ct);

        if (referral is null || referral.ReferrerUserId == newUser.Id) return;

        referral.InviteeUserId = newUser.Id;
        referral.Status = ReferralStatus.Joined;
        referral.JoinedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// A newcomer finished their first stay, so both sides are paid. Called from
    /// the lifecycle sweep, once a booking reaches Completed.
    /// </summary>
    public async Task<int> RewardCompletedStaysAsync(CancellationToken ct)
    {
        var pending = await db.Referrals
            .Include(r => r.InviteeUser)
            .Include(r => r.ReferrerUser)
            .Where(r => r.Status == ReferralStatus.Joined && r.InviteeUserId != null)
            .ToListAsync(ct);
        if (pending.Count == 0) return 0;

        var ids = pending.Select(r => r.InviteeUserId!.Value).ToList();
        var travelled = await db.Bookings
            .Where(b => b.GuestUserId != null && ids.Contains(b.GuestUserId.Value)
                        && b.Status == BookingStatus.Completed)
            .Select(b => b.GuestUserId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var rewarded = 0;

        foreach (var referral in pending.Where(r => travelled.Contains(r.InviteeUserId!.Value)))
        {
            Add(referral.ReferrerUserId, referral.ReferrerReward, CreditReason.Referral,
                $"Bạn bè bạn giới thiệu đã đi chuyến đầu tiên");
            Add(referral.InviteeUserId!.Value, referral.InviteeReward, CreditReason.Referral,
                "Thưởng chuyến đi đầu tiên");

            db.LedgerEntries.AddRange(Ledger.GrantCredit(
                null, referral.ReferrerReward + referral.InviteeReward,
                "Thưởng giới thiệu bạn bè", DateTime.UtcNow));

            referral.Status = ReferralStatus.Rewarded;
            referral.RewardedAt = DateTime.UtcNow;
            rewarded++;

            await notifications.QueueWithEmailAsync(
                referral.ReferrerUser, NotificationKind.System,
                "Bạn được thưởng giới thiệu",
                $"{referral.ReferrerReward:#,##0}₫ đã vào số dư của bạn.", "/wallet", ct);

            await notifications.QueueWithEmailAsync(
                referral.InviteeUser, NotificationKind.System,
                "Thưởng chuyến đi đầu tiên",
                $"{referral.InviteeReward:#,##0}₫ đã vào số dư của bạn.", "/wallet", ct);
        }

        if (rewarded > 0) await db.SaveChangesAsync(ct);
        return rewarded;
    }

    private async Task<string> UniqueCodeAsync(string prefix, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = CreditRules.NewCode(prefix, RandomNumberGenerator.GetInt32);
            var taken = prefix == "GC"
                ? await db.GiftCards.AnyAsync(c => c.Code == code, ct)
                : await db.Referrals.AnyAsync(r => r.Code == code, ct);

            if (!taken) return code;
        }

        // Ten collisions on a 32^10 space means something is very wrong.
        throw new InvalidOperationException("Không tạo được mã duy nhất.");
    }
}
