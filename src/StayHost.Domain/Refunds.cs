namespace StayHost.Domain;

/// <summary>
/// docs/07 §10 — where a refund goes back to.
///
/// "Hoàn về đúng nguồn đã trả. Trả thẻ thì hoàn về thẻ, trả bằng số dư thì hoàn
/// về số dư, trả nhiều nguồn thì hoàn ngược thứ tự đã trừ."
///
/// <see cref="Cancellation"/> decides how much comes back; this decides which
/// pocket it lands in. Keeping them apart is what lets the refund policy change
/// without anybody re-deriving where the money goes.
/// </summary>
public static class Refunds
{
    /// <summary>
    /// What a booking was actually paid with. The deduction order of docs/07 §3
    /// is gift card, then balance, then the payment method — so a refund unwinds
    /// it the other way round.
    /// </summary>
    public readonly record struct Sources(decimal Card, decimal Credit)
    {
        public decimal Total => Card + Credit;
    }

    /// <summary>Where a refund ended up, and what could not be sent anywhere.</summary>
    public readonly record struct Split(decimal ToCard, decimal ToCredit, decimal Unrefundable)
    {
        public decimal Total => ToCard + ToCredit;

        /// <summary>An amount already known to be going back to the card.</summary>
        public static Split Of(decimal toCard) => new(Math.Max(0m, toCard), 0m, 0m);
    }

    /// <summary>
    /// docs/07 §10 — the payment method is repaid first, then the balance.
    ///
    /// <paramref name="alreadyRefunded"/> is what earlier refunds on this same
    /// booking have already sent back: "tổng hoàn không bao giờ vượt số đã thu",
    /// and a booking can be refunded more than once.
    /// </summary>
    public static Split Allocate(Sources paid, decimal amount, decimal alreadyRefunded = 0)
    {
        var headroom = Math.Max(0m, paid.Total - Math.Max(0m, alreadyRefunded));
        var wanted = Math.Max(0m, amount);
        var payable = Math.Min(wanted, headroom);

        // Earlier refunds came out of the card first, so what is left of each
        // source has to account for them in the same order.
        var spentFromCard = Math.Min(alreadyRefunded, paid.Card);
        var cardLeft = Math.Max(0m, paid.Card - spentFromCard);
        var creditLeft = Math.Max(0m, paid.Credit - (alreadyRefunded - spentFromCard));

        var toCard = Math.Min(payable, cardLeft);
        var toCredit = Math.Min(payable - toCard, creditLeft);

        return new Split(toCard, toCredit, wanted - payable);
    }

    /// <summary>
    /// docs/07 §10 — a card that has expired or been closed.
    ///
    /// The refund is still sent to it first: banks usually move it to the
    /// cardholder's account themselves. Only when the bank hands it back does it
    /// become balance — and the guest is told, because money arriving somewhere
    /// they did not expect is worse than money arriving late.
    /// </summary>
    public static Split Redirect(Split split) =>
        split.ToCard <= 0 ? split : new Split(0, split.ToCredit + split.ToCard, split.Unrefundable);

    public static string RedirectNotice(decimal amount) =>
        $"Thẻ dùng cho đơn này không nhận được hoàn tiền, nên {amount:#,##0}₫ đã được chuyển vào " +
        "số dư Staylio của bạn. Bạn rút về ngân hàng bất cứ lúc nào.";

    /// <summary>How long a card refund takes to appear, in the bank's hands rather than ours.</summary>
    public const int CardRefundDaysMin = 5;
    public const int CardRefundDaysMax = 10;

    /// <summary>docs/07 §10 — said before the guest confirms, not after.</summary>
    public static string TimingNotice(Split split)
    {
        if (split.ToCard > 0 && split.ToCredit > 0)
            return $"{split.ToCard:#,##0}₫ về thẻ trong {CardRefundDaysMin}–{CardRefundDaysMax} ngày làm việc " +
                   $"và {split.ToCredit:#,##0}₫ về số dư ngay.";

        if (split.ToCard > 0)
            return $"{split.ToCard:#,##0}₫ về thẻ trong {CardRefundDaysMin}–{CardRefundDaysMax} ngày làm việc. " +
                   "Đây là thời gian xử lý của ngân hàng, không phải của StayHost.";

        return split.ToCredit > 0
            ? $"{split.ToCredit:#,##0}₫ về số dư Staylio của bạn ngay lập tức."
            : "Không có khoản nào được hoàn cho đơn này.";
    }
}
