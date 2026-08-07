namespace StayHost.Domain;

/// <summary>How soon docs/07 §2 expects a way of paying to exist.</summary>
public enum MethodTier
{
    /// <summary>§2.1 — cannot launch without it.</summary>
    Required = 0,
    /// <summary>§2.2 — wanted, not blocking.</summary>
    Wanted = 1,
    /// <summary>§2.3 — later.</summary>
    Later = 2
}

/// <summary>
/// docs/07 §2 — everything a guest may pay with, and the four things they may
/// not.
///
/// This is a catalogue, not a switch: the payment page, the saved-methods screen
/// and the refund path all read the same list, so a method cannot be offered in
/// one place and unknown in another.
/// </summary>
public static class PaymentMethods
{
    public readonly record struct Method(
        string Key,
        string Group,
        string Label,
        string Hint,
        MethodTier Tier,
        /// <summary>True for the card family, which is what §4 lets a guest save.</summary>
        bool Savable = false);

    /* ------------------------------------------------------------ §2.1–§2.3 */

    private static readonly Method[] All =
    [
        new("card", "Thẻ quốc tế", "Thẻ tín dụng / ghi nợ", "Visa, Mastercard, JCB, American Express",
            MethodTier.Required, Savable: true),
        new("napas", "Thẻ nội địa", "Thẻ ATM nội địa", "Qua NAPAS, cần đăng ký thanh toán trực tuyến",
            MethodTier.Required, Savable: true),
        new("momo", "Ví điện tử", "Ví MoMo", "Mở ứng dụng MoMo để xác nhận", MethodTier.Required),
        new("zalopay", "Ví điện tử", "ZaloPay", "Mở ứng dụng ZaloPay để xác nhận", MethodTier.Required),
        new("balance", "Số dư", "Số dư & thẻ quà tặng", "Dùng trước, phần còn thiếu trả bằng cách khác",
            MethodTier.Required),

        new("applepay", "Ví thiết bị", "Apple Pay", "Xác nhận bằng Face ID hoặc Touch ID", MethodTier.Wanted),
        new("googlepay", "Ví thiết bị", "Google Pay", "Xác nhận trong ứng dụng Google Wallet", MethodTier.Wanted),

        new("paypal", "Quốc tế", "PayPal", "Dành cho khách thanh toán bằng ngoại tệ", MethodTier.Later),
        new("vietqr", "Chuyển nhanh", "VietQR", "Quét mã bằng ứng dụng ngân hàng", MethodTier.Later),
        new("installment", "Trả góp", "Trả góp qua thẻ", "0% qua ngân hàng phát hành thẻ", MethodTier.Later)
    ];

    public static IReadOnlyList<Method> Available(MethodTier upTo = MethodTier.Required) =>
        All.Where(m => m.Tier <= upTo).ToList();

    public static Method? Find(string? key) =>
        All.FirstOrDefault(m => m.Key == key) is { Key.Length: > 0 } found ? found : null;

    public static bool IsAccepted(string? key) => Find(key) is not null;

    public static bool IsSavable(string? key) => Find(key) is { Savable: true };

    /// <summary>The card family, which is the only thing §4's rules are about.</summary>
    public static bool IsCard(string? key) => key is "card" or "napas";

    /* ------------------------------------------------------- §3, the fallback */

    /// <summary>
    /// docs/07 §3 — "Nếu số dư đủ trả toàn bộ thì vẫn bắt buộc gắn một phương
    /// thức dự phòng, dùng khi có phát sinh (đổi lịch, bồi thường)."
    ///
    /// A booking that costs the guest nothing today can still cost them
    /// something later, and docs/06 §3.3 collects damages through exactly this
    /// stored method. Without one the platform has a claim and nowhere to send
    /// it.
    /// </summary>
    public static bool NeedsFallbackMethod(decimal charged, string? method, string? cardLast4) =>
        charged <= 0 && !HasFallback(method, cardLast4);

    /// <summary>
    /// A card counts only once it has a number behind it; a wallet counts on its
    /// own, because it is authorised in its own app rather than stored here.
    /// The balance never counts — it is the thing that ran out.
    /// </summary>
    private static bool HasFallback(string? method, string? cardLast4) =>
        IsCard(method)
            ? !string.IsNullOrWhiteSpace(cardLast4)
            : IsAccepted(method) && method != "balance";

    public static string FallbackNotice() =>
        "Số dư của bạn đủ trả toàn bộ đơn này, nhưng vẫn cần một phương thức dự phòng " +
        "để dùng khi có phát sinh như đổi lịch hoặc bồi thường. Bạn sẽ không bị trừ tiền bây giờ.";

    /* -------------------------------------------------------------- §2.4 */

    /// <summary>
    /// docs/07 §2.4 — "Khi khách hỏi những cách này, hệ thống phải giải thích lý
    /// do ngắn gọn". Refusing without a reason reads as a broken feature; the
    /// reason is the same one every time, so it lives here rather than in copy.
    /// </summary>
    private static readonly Dictionary<string, string> Refused = new()
    {
        ["cash"] = "Tiền mặt",
        ["transfer"] = "Chuyển khoản thủ công",
        ["crypto"] = "Tiền mã hoá",
        ["cheque"] = "Séc",
        ["on-arrival"] = "Trả khi nhận phòng"
    };

    public static bool IsRefused(string? key) => key is not null && Refused.ContainsKey(key);

    public static IReadOnlyList<string> RefusedLabels => Refused.Values.ToList();

    public static string RefusalReason() =>
        "StayHost giữ tiền cho tới khi bạn nhận phòng để bảo vệ cả bạn và chủ nhà, " +
        "nên chỉ nhận các cách trả tiền qua hệ thống.";
}
