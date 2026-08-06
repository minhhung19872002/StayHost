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
    public Task<decimal> BalanceAsync(int userId, CancellationToken ct) =>
        db.CreditEntries.Where(c => c.UserId == userId).SumAsync(c => (decimal?)c.Amount, ct)
            .ContinueWith(t => t.Result ?? 0m, ct);

    /// <summary>Adds a movement without saving; the caller commits it with the rest.</summary>
    public void Add(int userId, decimal amount, CreditReason reason, string memo, int? bookingId = null)
    {
        if (amount == 0) return;

        db.CreditEntries.Add(new CreditEntry
        {
            UserId = userId,
            Amount = amount,
            Reason = reason,
            Memo = memo,
            BookingId = bookingId
        });
    }

    /* -------------------------------------------------------- gift cards */

    public async Task<(GiftCard? Card, string? Error)> BuyAsync(
        User buyer, decimal amount, string recipientEmail, string? recipientName, string? message,
        CancellationToken ct)
    {
        if (amount < CreditRules.MinGiftCard || amount > CreditRules.MaxGiftCard)
            return (null, $"Thẻ quà tặng từ {CreditRules.MinGiftCard:#,##0}₫ đến {CreditRules.MaxGiftCard:#,##0}₫.");

        var email = (recipientEmail ?? "").Trim().ToLowerInvariant();
        if (email.Length == 0 || !email.Contains('@'))
            return (null, "Email người nhận không hợp lệ.");

        var card = new GiftCard
        {
            Code = await UniqueCodeAsync("GC", ct),
            Amount = amount,
            Remaining = amount,
            PurchasedByUserId = buyer.Id,
            RecipientEmail = email,
            RecipientName = recipientName?.Trim(),
            Message = message?.Trim()
        };

        db.GiftCards.Add(card);
        db.LedgerEntries.AddRange(Ledger.SellGiftCard(amount, card.Code, DateTime.UtcNow));

        db.EmailMessages.Add(new EmailMessage
        {
            ToEmail = email,
            ToName = card.RecipientName ?? email,
            Subject = $"{buyer.FullName} tặng bạn {amount:#,##0}₫ trên StayHost",
            Body = $"Mã thẻ của bạn: {card.Code}\n" +
                   (string.IsNullOrWhiteSpace(card.Message) ? "" : $"\"{card.Message}\"\n") +
                   "Nhập mã trong mục Số dư để cộng vào tài khoản."
        });

        await db.SaveChangesAsync(ct);
        log.LogInformation("Gift card {Code} sold for {Amount}.", card.Code, amount);

        return (card, null);
    }

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
            Subject = $"{referrer.FullName} mời bạn dùng StayHost",
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
