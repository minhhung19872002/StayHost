using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Contracts;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

/// <summary>
/// Balance, gift cards and referrals. Balance comes off the room charge at
/// checkout, never off the fees or the tax — it is money towards a stay, not a
/// discount on what the platform and the tax office are owed.
/// </summary>
[ApiController]
[Route("api/wallet")]
public class WalletController(
    StayHostDbContext db, AuthService auth, WalletService wallet, GiftCardSales sales) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<WalletDto>> Mine(CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var balance = await wallet.BalanceAsync(user.Id, ct);

        // docs/01 TC-07 — the whole run, because what is about to lapse is worked
        // out by replaying it; the 50 newest are what gets shown.
        var all = await db.CreditEntries.Where(c => c.UserId == user.Id).ToListAsync(ct);

        var entries = all
            .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
            .Take(50)
            .Select(c => new CreditEntryDto(
                c.Id, c.Amount, c.Reason.ToString(), CreditRules.ReasonLabel(c.Reason),
                c.Memo, c.BookingId, c.CreatedAt, c.ExpiresAt))
            .ToList();

        var nextExpiry = CreditLedger.NextExpiry(all, DateTime.UtcNow);
        var expiring = nextExpiry is { } when ? CreditLedger.ExpiringOn(all, when) : 0m;

        // The code is withheld until the card is paid for, the same as it is in
        // the buy response. Redeeming an unpaid card is already refused by
        // CreditRules.CanRedeem, so this is not the lock — it is not leaving the
        // key on the table beside it.
        var bought = await db.GiftCards
            .Where(g => g.PurchasedByUserId == user.Id)
            .OrderByDescending(g => g.CreatedAt)
            .Select(g => new GiftCardDto(
                g.Id,
                g.Status == GiftCardStatus.AwaitingPayment ? "" : g.Code,
                g.Amount, g.Remaining, g.RecipientEmail, g.RecipientName, g.Message,
                g.Status.ToString(), CreditRules.StatusLabel(g.Status), g.CreatedAt, g.RedeemedAt))
            .ToListAsync(ct);

        var referrals = await db.Referrals
            .Where(r => r.ReferrerUserId == user.Id)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReferralDto(
                r.Id, r.Code, r.InviteeEmail, r.InviteeUser!.FullName,
                r.Status.ToString(), CreditRules.StatusLabel(r.Status),
                r.ReferrerReward, r.InviteeReward, r.CreatedAt))
            .ToListAsync(ct);

        return Ok(new WalletDto(
            balance, entries, bought, referrals,
            CreditRules.ReferrerReward, CreditRules.InviteeReward,
            CreditRules.MinGiftCard, CreditRules.MaxGiftCard,
            nextExpiry, expiring));
    }

    [HttpPost("gift-cards")]
    public async Task<ActionResult<GiftCardPurchaseDto>> Buy([FromBody] BuyGiftCardRequest req, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        // docs/08 §6 — "Số dư khuyến mãi: đóng băng, không xoá." The balance
        // survives the lock; it just does not move while the account is locked.
        if (user.IsSuspended)
            return StatusCode(403, new { message = "Tài khoản đang bị tạm khoá nên ví bị đóng băng — số dư vẫn được giữ nguyên." });

        var (card, payUrl, error) = await sales.BuyAsync(
            user, req.Amount, req.RecipientEmail ?? "", req.RecipientName, req.Message,
            req.Method, req.CardLast4, Psp.ClientIp(HttpContext.Connection.RemoteIpAddress?.ToString()), ct);
        if (card is null) return BadRequest(new { message = error });

        // The code is deliberately absent while the card is still awaiting
        // payment: it is the bearer instrument, and handing it back before the
        // money arrives would put the hole straight back.
        var paid = card.Status != GiftCardStatus.AwaitingPayment;

        return Ok(new GiftCardPurchaseDto(
            new GiftCardDto(
                card.Id, paid ? card.Code : "", card.Amount, card.Remaining,
                card.RecipientEmail, card.RecipientName, card.Message,
                card.Status.ToString(), CreditRules.StatusLabel(card.Status),
                card.CreatedAt, card.RedeemedAt),
            payUrl));
    }

    [HttpPost("redeem")]
    public async Task<ActionResult<WalletDto>> Redeem([FromBody] RedeemGiftCardRequest req, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        if (user.IsSuspended)
            return StatusCode(403, new { message = "Tài khoản đang bị tạm khoá nên ví bị đóng băng — số dư vẫn được giữ nguyên." });

        var (added, error) = await wallet.RedeemAsync(user, req.Code, ct);
        if (error is not null) return BadRequest(new { message = error });

        var mine = await Mine(ct);
        return mine.Result is null ? Ok(mine.Value) : mine;
    }

    [HttpPost("referrals")]
    public async Task<ActionResult<ReferralDto>> Invite([FromBody] InviteFriendRequest req, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        if (email.Length == 0 || !email.Contains('@'))
            return BadRequest(new { message = "Email không hợp lệ." });
        if (string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Không thể tự giới thiệu chính mình." });

        var referral = await wallet.InviteAsync(user, email, ct);

        return Ok(new ReferralDto(
            referral.Id, referral.Code, referral.InviteeEmail, null,
            referral.Status.ToString(), CreditRules.StatusLabel(referral.Status),
            referral.ReferrerReward, referral.InviteeReward, referral.CreatedAt));
    }
}
