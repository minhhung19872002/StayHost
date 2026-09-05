using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;

namespace StayHost.Web.Services;

/// <summary>
/// docs/01 TC-08 — the one place a gift card stops being an order and becomes
/// money.
///
/// It is a service of its own so that the two ways a card can be paid for — a
/// licensed gateway settling a <see cref="PaymentSession"/>, and the stand-in
/// gateway charging in place on a deployment with no live provider — cannot end
/// up with two versions of what "paid" does. Putting it on
/// <see cref="WalletService"/> would also have made a dependency cycle, since
/// that is what starts the payment.
///
/// Before this existed, buying a card created it Active, posted
/// <see cref="Ledger.SellGiftCard"/> and emailed the code with no payment
/// anywhere in the path. Anyone signed in could mint the maximum and spend it on
/// a real stay. Nothing alarmed: the two ledger legs balance each other, so the
/// daily check of docs/07 §5 read zero, and a gift card produces no gateway
/// charge for the reconciliation of docs/07 §7 to find missing.
/// </summary>
public class GiftCardService(StayHostDbContext db, ILogger<GiftCardService> log)
{
    /// <summary>A code nobody holds yet. Ten collisions on 32^10 means something is badly wrong.</summary>
    public async Task<string> NewCodeAsync(CancellationToken ct)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = CreditRules.NewCode("GC", System.Security.Cryptography.RandomNumberGenerator.GetInt32);
            if (!await db.GiftCards.AnyAsync(c => c.Code == code, ct)) return code;
        }
        throw new InvalidOperationException("Không sinh được mã thẻ quà tặng duy nhất.");
    }

    /// <summary>
    /// Turns a paid-for card on: the ledger entry that says the money reached
    /// escrow, and the email carrying the code.
    ///
    /// Safe to call twice. Three callers can race here for the same reason
    /// docs/07 §5 gives three ways to learn a payment happened — the guest
    /// returning, the gateway's IPN, and the platform asking — and the second
    /// one through must not post the ledger again.
    /// </summary>
    public async Task<bool> ActivateAsync(int giftCardId, decimal paid, CancellationToken ct)
    {
        var card = await db.GiftCards.Include(c => c.PurchasedByUser)
            .FirstOrDefaultAsync(c => c.Id == giftCardId, ct);

        if (card is null)
        {
            log.LogError("Không tìm thấy thẻ quà tặng {Id} để kích hoạt.", giftCardId);
            return false;
        }

        // Already on, by whichever of the three arrived first.
        if (card.Status != GiftCardStatus.AwaitingPayment) return false;

        // The ledger entry must be for money that actually arrived. SettleAsync
        // has already refused a gateway reporting a different figure than the
        // session; this is the same question asked of the card itself, because
        // the entry written below is what the daily reconciliation will trust.
        if (!Psp.AmountMatches(card.Amount, paid))
        {
            log.LogError("Thẻ {Code} trị giá {Amount} nhưng cổng báo thu {Paid}. Không kích hoạt.",
                card.Code, card.Amount, paid);
            return false;
        }

        card.Status = GiftCardStatus.Active;
        card.Remaining = card.Amount;

        db.LedgerEntries.AddRange(Ledger.SellGiftCard(card.Amount, card.Code, DateTime.UtcNow));

        var buyer = card.PurchasedByUser?.FullName ?? "Một người bạn";

        // docs/01 TK-09 — the recipient may already have an account, and then
        // their language is on file even though the buyer only typed an email.
        // The card CODE is the secret this mail exists to carry, so RawTitle
        // stays null: the machine-translation pass must never touch it.
        var recipient = await db.Users
            .FirstOrDefaultAsync(u => u.Email == card.RecipientEmail.ToLower(), ct);
        var name = card.RecipientName ?? card.RecipientEmail;

        db.EmailMessages.Add(new EmailMessage
        {
            ToEmail = card.RecipientEmail,
            ToName = name,
            Subject = $"{buyer} tặng bạn {card.Amount:#,##0}₫ trên Staylio",
            Body = Emails.Compose(recipient?.Language, name,
                $"{buyer} tặng bạn một thẻ quà tặng {card.Amount:#,##0}₫.",
                $"Mã thẻ của bạn: {card.Code}\n" +
                (string.IsNullOrWhiteSpace(card.Message) ? "" : $"\"{card.Message}\"\n") +
                "Nhập mã trong mục Số dư để cộng vào tài khoản.", null),
            Language = recipient?.Language
        });

        await db.SaveChangesAsync(ct);
        log.LogInformation("Thẻ quà tặng {Code} đã thanh toán xong, trị giá {Amount}.",
            card.Code, card.Amount);
        return true;
    }

    /// <summary>
    /// The buyer never paid — the gateway refused, or they walked away from its
    /// page. The card is closed rather than left sitting in AwaitingPayment
    /// forever, so the buyer's list says what happened.
    /// </summary>
    public async Task CancelUnpaidAsync(int giftCardId, CancellationToken ct)
    {
        var card = await db.GiftCards.FirstOrDefaultAsync(c => c.Id == giftCardId, ct);
        if (card is null || card.Status != GiftCardStatus.AwaitingPayment) return;

        card.Status = GiftCardStatus.Cancelled;
        card.Remaining = 0;
        await db.SaveChangesAsync(ct);
    }
}
