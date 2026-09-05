using StayHost.Domain;
using StayHost.Infrastructure;
using StayHost.Web.Services.Gateways;

namespace StayHost.Web.Services;

/// <summary>
/// docs/01 TC-08 — selling a gift card, which means taking money for it.
///
/// Separate from <see cref="WalletService"/>, and not by preference: starting a
/// payment needs <see cref="PspCheckout"/>, which needs
/// <see cref="PaymentCompletion"/>, which needs <see cref="WalletService"/>.
/// Putting the sale on the wallet closed that ring and the container refused to
/// start — which is the good outcome, since the alternative is finding it at
/// runtime. So the pieces are split by which way they point: the wallet keeps
/// balances, <see cref="GiftCardService"/> switches a paid card on and is what
/// the checkout calls back into, and this starts the payment and is called by
/// nothing but the controller.
/// </summary>
public class GiftCardSales(
    StayHostDbContext db, PspRouter router, PspCheckout checkout,
    PaymentGateway gateway, GiftCardService giftCards, ILogger<GiftCardSales> log)
{
    /// <summary>
    /// Orders a card and takes the money for it.
    ///
    /// The taking is what was missing. This used to create the card Active, post
    /// <see cref="Ledger.SellGiftCard"/> and email the code without ever calling
    /// a gateway, so anyone signed in could mint the 20,000,000₫ ceiling and
    /// spend it on a real stay with a real host at the other end. Nothing
    /// complained: that ledger entry asserts the money reached escrow and its
    /// two legs balance, so the daily check of docs/07 §5 read zero, and a card
    /// raises no gateway charge for the reconciliation of §7 to find missing.
    ///
    /// Now the card is born <see cref="GiftCardStatus.AwaitingPayment"/> — no
    /// ledger entry, no code handed back, and <see cref="CreditRules.CanRedeem"/>
    /// refuses it — and one of two things pays for it, exactly as a stay is paid
    /// for. Which one is never guessed at here: <see cref="PspRouter"/> is asked,
    /// because a method wired to a real gateway being charged by the stand-in is
    /// the one thing that must never happen.
    /// </summary>
    public async Task<(GiftCard? Card, string? PayUrl, string? Error)> BuyAsync(
        User buyer, decimal amount, string recipientEmail, string? recipientName, string? message,
        string? method, string? cardLast4, string clientIp, CancellationToken ct)
    {
        if (amount < CreditRules.MinGiftCard || amount > CreditRules.MaxGiftCard)
            return (null, null, $"Thẻ quà tặng từ {CreditRules.MinGiftCard:#,##0}₫ đến {CreditRules.MaxGiftCard:#,##0}₫.");

        var email = (recipientEmail ?? "").Trim().ToLowerInvariant();
        if (email.Length == 0 || !email.Contains('@'))
            return (null, null, "Email người nhận không hợp lệ.");

        var pick = (method ?? "").Trim().ToLowerInvariant();
        if (pick.Length == 0) pick = "card";

        if (!GiftCardPurchase.CanPayWith(pick))
            return (null, null, GiftCardPurchase.Refusal(pick));

        var card = new GiftCard
        {
            Code = await giftCards.NewCodeAsync(ct),
            Amount = amount,
            // Worth nothing until the money lands; ActivateAsync is the only
            // thing that fills this in.
            Remaining = 0,
            Status = GiftCardStatus.AwaitingPayment,
            PurchasedByUserId = buyer.Id,
            RecipientEmail = email,
            RecipientName = recipientName?.Trim(),
            Message = message?.Trim()
        };

        db.GiftCards.Add(card);
        await db.SaveChangesAsync(ct);

        var attemptKey = $"gift-{card.Id}";

        // A licensed gateway: the buyer leaves, and the card turns on when the
        // gateway says the money arrived — through PspCheckout.SettleAsync, by
        // whichever of docs/07 §5's three routes gets back first.
        if (router.IsLive(pick))
        {
            var started = await checkout.StartForGiftCardAsync(card, pick, attemptKey, clientIp, ct);
            if (!started.Ok)
                return (null, null, started.Error ?? "Không mở được cổng thanh toán.");

            log.LogInformation("Thẻ quà tặng {Code} chờ thanh toán qua cổng {Provider}.", card.Code, pick);
            return (card, started.PayUrl, null);
        }

        // No provider behind this method, so the stand-in takes it in place — the
        // same gateway the demo checkout uses, and it refuses the test card
        // ending 0000 just the same.
        var charge = gateway.Charge(amount, pick, cardLast4, attemptKey);
        if (!charge.Ok)
        {
            await giftCards.CancelUnpaidAsync(card.Id, ct);
            return (null, null, charge.Reason);
        }

        await giftCards.ActivateAsync(card.Id, amount, ct);
        return (card, null, null);
    }
}
