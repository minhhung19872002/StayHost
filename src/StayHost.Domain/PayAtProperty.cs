namespace StayHost.Domain;

/// <summary>
/// docs/07 §2.5 — the guest settles with the host on arrival instead of with the
/// platform in advance.
///
/// This reverses what §2.4 used to say. The old rule refused it in one line,
/// with a reason worth keeping in mind: the platform holding the money is what
/// protects both sides, and none of that protection exists here. So the shape of
/// this feature is not "another way to pay" — it is **the platform stepping out
/// of the payment entirely**, and every consequence follows from that:
///
/// <list type="bullet">
/// <item>No ledger entry is written when the booking is made or confirmed. No
/// money moved through Staylio, and writing one would state that it had.</item>
/// <item>There is nothing to refund on a cancellation, so the refund preview has
/// to say so rather than quote a number nobody will pay.</item>
/// <item>The platform's fees are still earned, and are recorded as
/// <see cref="HostProfile.OwedToPlatform"/> — the same mechanism a lost
/// chargeback already uses — netted off the host's next transfer.</item>
/// <item>The host collects the tax with the cash and remits it themselves. The
/// platform never held it, so it cannot be the one to pass it on.</item>
/// </list>
///
/// It is off unless a host turns it on for a listing, and it never becomes the
/// default: a guest who wants the platform to hold their money must always be
/// able to choose that.
/// </summary>
public static class PayAtProperty
{
    /// <summary>The method key, in the same namespace as card, momo and vietqr.</summary>
    public const string Key = "property";

    public enum Refusal
    {
        None = 0,
        /// <summary>The host did not offer it on this listing.</summary>
        NotOfferedHere,
        /// <summary>docs/01 ĐP-06 — a deposit is money through the platform, which this is not.</summary>
        NotWithDeposit,
        /// <summary>docs/01 ĐP-07 — sixteen people cannot each hand over cash at one front desk.</summary>
        NotWithSplit,
        /// <summary>Balance and promo codes are settled by the platform, so they need the platform in the loop.</summary>
        NotWithPlatformMoney
    }

    public readonly record struct Check(bool Ok, Refusal Reason, string Message)
    {
        public static Check Pass => new(true, Refusal.None, "");
        public static Check Fail(Refusal reason, string message) => new(false, reason, message);
    }

    public static Check CanUse(
        bool listingAccepts, bool payingDeposit, bool splittingBill, bool usesCreditOrCoupon)
    {
        if (!listingAccepts)
            return Check.Fail(Refusal.NotOfferedHere,
                "Chỗ nghỉ này không nhận trả tiền tại nơi ở. Hãy chọn cách trả khác.");

        if (payingDeposit)
            return Check.Fail(Refusal.NotWithDeposit,
                "Trả tại nơi ở thì trả một lần khi nhận phòng, không đặt cọc trước.");

        if (splittingBill)
            return Check.Fail(Refusal.NotWithSplit,
                "Chia hoá đơn cần thu tiền qua sàn, nên không đi cùng trả tại nơi ở.");

        if (usesCreditOrCoupon)
            return Check.Fail(Refusal.NotWithPlatformMoney,
                "Số dư và mã giảm giá do sàn trừ, nên không dùng chung với trả tại nơi ở.");

        return Check.Pass;
    }

    /// <summary>
    /// What the host owes Staylio once they have the cash: both service fees.
    ///
    /// The guest hands over the whole quoted total — the same number they would
    /// have paid by card, so the choice of method never changes the price — and
    /// that total already contains the guest's 14% (docs/03 §1). The host is
    /// therefore holding Staylio's fee as well as their own.
    /// </summary>
    public static decimal FeesOwed(decimal guestServiceFee, decimal hostServiceFee) =>
        Math.Max(0m, guestServiceFee) + Math.Max(0m, hostServiceFee);

    /// <summary>
    /// Nothing was taken, so nothing goes back. Said out loud because the refund
    /// preview of docs/01 CĐ-07 otherwise quotes a figure off the booking total
    /// and promises money that was never collected.
    /// </summary>
    public const string NothingToRefund =
        "Bạn chưa trả đồng nào cho đơn này nên không có khoản nào để hoàn. "
        + "Huỷ sớm giúp chủ nhà bán lại được ngày đó.";

    /// <summary>What the guest is told at checkout, and again on the trip page.</summary>
    public static string Notice(decimal total, string currencySuffix = "₫") =>
        $"Bạn trả {total:#,##0}{currencySuffix} trực tiếp cho chủ nhà khi nhận phòng. "
        + "Staylio không thu trước và không giữ tiền của đơn này.";

    /// <summary>
    /// What the host is told, because the protection they are giving up is
    /// theirs. A no-show costs them the night with nothing held against it.
    /// </summary>
    public const string HostWarning =
        "Khách trả tiền mặt cho bạn khi nhận phòng. Staylio không giữ tiền, nên nếu khách "
        + "không tới thì không có khoản nào để bù. Phí dịch vụ của đơn được trừ vào lần "
        + "chuyển tiền kế tiếp của bạn.";
}
