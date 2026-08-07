using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Contracts;
using StayHost.Web.Services;

namespace StayHost.Web.Controllers;

/// <summary>
/// docs/07 §2 and §4 — what a guest may pay with, and the cards they have kept.
///
/// The full number never reaches this class: the client sends it once so the
/// brand and last four can be read off it, and what is stored is what §4 allows
/// on a screen. There is no endpoint that returns a card number because there is
/// no card number to return.
/// </summary>
[ApiController]
[Route("api/payment-methods")]
public class PaymentMethodsController(
    StayHostDbContext db, AuthService auth, NotificationService notifications) : ControllerBase
{
    /// <summary>docs/07 §2 — the catalogue, so the payment page and this screen agree.</summary>
    [HttpGet("catalogue")]
    public ActionResult<PaymentCatalogueDto> Catalogue()
    {
        var offered = PaymentMethods.Available()
            .Select(m => new PaymentMethodDto(m.Key, m.Group, m.Label, m.Hint, m.Savable))
            .ToList();

        return Ok(new PaymentCatalogueDto(offered, PaymentMethods.RefusedLabels, PaymentMethods.RefusalReason()));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SavedCardDto>>> Mine(CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        return Ok(await CardsOf(user.Id, ct));
    }

    [HttpPost]
    public async Task<ActionResult<IReadOnlyList<SavedCardDto>>> Add(
        [FromBody] SaveCardRequest req, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        if (!SavedCards.IsPlausibleNumber(req.Number))
            return BadRequest(new { message = "Số thẻ không đúng. Hãy kiểm tra lại." });

        var brand = SavedCards.BrandOf(req.Number);
        if (brand == CardBrand.Unknown)
            return BadRequest(new { message = "StayHost chưa nhận loại thẻ này." });

        if (req.ExpiryMonth is < 1 or > 12 || req.ExpiryYear < 2000)
            return BadRequest(new { message = "Tháng/năm hết hạn không hợp lệ." });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (SavedCards.ExpiresAfter(req.ExpiryMonth, req.ExpiryYear) < today)
            return BadRequest(new { message = "Thẻ này đã hết hạn." });

        var last4 = SavedCards.Last4Of(req.Number);

        var already = await db.SavedCards.AnyAsync(
            c => c.UserId == user.Id && c.Last4 == last4 && c.Brand == brand
                 && c.ExpiryMonth == req.ExpiryMonth && c.ExpiryYear == req.ExpiryYear, ct);

        if (already) return BadRequest(new { message = "Thẻ này đã được lưu rồi." });

        var card = new SavedCard
        {
            UserId = user.Id,
            Brand = brand,
            Last4 = last4,
            ExpiryMonth = req.ExpiryMonth,
            ExpiryYear = req.ExpiryYear,
            Nickname = string.IsNullOrWhiteSpace(req.Nickname) ? null : req.Nickname.Trim()
        };

        db.SavedCards.Add(card);
        await db.SaveChangesAsync(ct);

        // The first card saved is the default; a later one only takes over when
        // the guest asks for it.
        var cards = await db.SavedCards.Where(c => c.UserId == user.Id).ToListAsync(ct);
        SavedCards.Reseat(cards, req.MakeDefault ? card.Id : null);
        await db.SaveChangesAsync(ct);

        return Ok(await CardsOf(user.Id, ct));
    }

    [HttpPut("{id:int}/default")]
    public async Task<ActionResult<IReadOnlyList<SavedCardDto>>> MakeDefault(int id, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var cards = await db.SavedCards.Where(c => c.UserId == user.Id).ToListAsync(ct);
        if (cards.All(c => c.Id != id)) return NotFound(new { message = "Không tìm thấy thẻ." });

        SavedCards.Reseat(cards, id);
        await db.SaveChangesAsync(ct);

        return Ok(await CardsOf(user.Id, ct));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<IReadOnlyList<SavedCardDto>>> Rename(
        int id, [FromBody] RenameCardRequest req, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var card = await db.SavedCards.FirstOrDefaultAsync(c => c.Id == id && c.UserId == user.Id, ct);
        if (card is null) return NotFound(new { message = "Không tìm thấy thẻ." });

        card.Nickname = string.IsNullOrWhiteSpace(req.Nickname) ? null : req.Nickname.Trim();
        await db.SaveChangesAsync(ct);

        return Ok(await CardsOf(user.Id, ct));
    }

    /// <summary>
    /// docs/07 §4 — removing a card is blocked while it is doing something. The
    /// guest is told which, and what to do first.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<IReadOnlyList<SavedCardDto>>> Remove(int id, CancellationToken ct)
    {
        var user = await auth.CurrentUserAsync(ct);
        if (user is null) return Unauthorized(new { message = "Bạn cần đăng nhập." });

        var card = await db.SavedCards.FirstOrDefaultAsync(c => c.Id == id && c.UserId == user.Id, ct);
        if (card is null) return NotFound(new { message = "Không tìm thấy thẻ." });

        var block = SavedCards.CanRemove(
            await HasScheduledChargeAsync(user.Id, card.Last4, ct),
            await HasOpenBookingAsync(user.Id, card.Last4, ct));

        if (block != SavedCards.RemovalBlock.None)
            return BadRequest(new { message = SavedCards.RemovalMessage(block) });

        db.SavedCards.Remove(card);
        await db.SaveChangesAsync(ct);

        // Removing the default hands the title on rather than leaving none.
        var rest = await db.SavedCards.Where(c => c.UserId == user.Id).ToListAsync(ct);
        SavedCards.Reseat(rest);
        await db.SaveChangesAsync(ct);

        return Ok(await CardsOf(user.Id, ct));
    }

    /* ------------------------------------------------------------- helpers */

    private async Task<IReadOnlyList<SavedCardDto>> CardsOf(int userId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var cards = await db.SavedCards
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.IsDefault).ThenByDescending(c => c.AddedAt)
            .ToListAsync(ct);

        var result = new List<SavedCardDto>(cards.Count);

        foreach (var card in cards)
        {
            var scheduled = await HasScheduledChargeAsync(userId, card.Last4, ct);

            result.Add(new SavedCardDto(
                card.Id,
                card.Brand.ToString(),
                SavedCards.BrandLabel(card.Brand),
                card.Last4,
                SavedCards.ExpiryLabel(card),
                card.Nickname,
                card.IsDefault,
                SavedCards.IsExpired(card, today),
                SavedCards.ExpiringSoon(card, today),
                scheduled,
                await HasOpenBookingAsync(userId, card.Last4, ct)));
        }

        return result;
    }

    /// <summary>
    /// docs/07 §6 — money still to be taken: a deposit whose balance is due, or
    /// a share of a split bill nobody has paid yet.
    /// </summary>
    private Task<bool> HasScheduledChargeAsync(int userId, string last4, CancellationToken ct) =>
        db.Bookings.AnyAsync(
            b => b.GuestUserId == userId
                 && b.Payment != null && b.Payment.CardLast4 == last4
                 && b.BalanceDueOn != null && b.BalanceDue > 0
                 && BookingLifecycle.BlocksDates.Contains(b.Status), ct);

    private Task<bool> HasOpenBookingAsync(int userId, string last4, CancellationToken ct) =>
        db.Bookings.AnyAsync(
            b => b.GuestUserId == userId
                 && b.Payment != null && b.Payment.CardLast4 == last4
                 && BookingLifecycle.BlocksDates.Contains(b.Status), ct);

    /// <summary>
    /// docs/07 §4 — "Thẻ sắp hết hạn mà còn lịch thu tự động → nhắc khách cập
    /// nhật trước 14 ngày." Called from the nightly sweep, once per card.
    /// </summary>
    public static async Task<int> RemindExpiringAsync(
        StayHostDbContext db, NotificationService notifications, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var sent = 0;

        var cards = await db.SavedCards
            .Where(c => c.ExpiryReminderSentAt == null)
            .Include(c => c.User)
            .ToListAsync(ct);

        foreach (var card in cards.Where(c => SavedCards.ExpiringSoon(c, today)))
        {
            var owing = await db.Bookings.AnyAsync(
                b => b.GuestUserId == card.UserId
                     && b.Payment != null && b.Payment.CardLast4 == card.Last4
                     && b.BalanceDueOn != null && b.BalanceDue > 0
                     && BookingLifecycle.BlocksDates.Contains(b.Status), ct);

            // Only a card with money still to come off it is worth a reminder;
            // otherwise this is an email about nothing.
            if (!owing) continue;

            await notifications.QueueWithEmailAsync(card.User, NotificationKind.System,
                "Thẻ sắp hết hạn", SavedCards.ExpiryReminder(card), "/account/payments", ct);

            card.ExpiryReminderSentAt = DateTime.UtcNow;
            sent++;
        }

        if (sent > 0) await db.SaveChangesAsync(ct);
        return sent;
    }
}
