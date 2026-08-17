namespace StayHost.Domain;

/// <summary>What a card is, as far as anyone here is allowed to know.</summary>
public enum CardBrand
{
    Unknown = 0,
    Visa = 1,
    Mastercard = 2,
    Jcb = 3,
    Amex = 4,
    /// <summary>Domestic ATM card routed through NAPAS.</summary>
    Napas = 5
}

/// <summary>
/// docs/07 §4 — a card a guest has chosen to keep.
///
/// The number is not here, and never was: only what §4 allows on the screen —
/// brand, last four, expiry. docs/07 §14 is blunt about why.
/// </summary>
public class SavedCard
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public CardBrand Brand { get; set; }
    public string Last4 { get; set; } = "";

    public int ExpiryMonth { get; set; }
    public int ExpiryYear { get; set; }

    /// <summary>What the cardholder called it, if anything. Never the full name on the card.</summary>
    public string? Nickname { get; set; }

    public bool IsDefault { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Set when the expiry reminder of §4 has gone out, so it goes out once.</summary>
    public DateTime? ExpiryReminderSentAt { get; set; }

    /// <summary>
    /// docs/07 §4 with §14.2 — the gateway's own handle on this card, sealed.
    ///
    /// Once the card form belongs to VNPay rather than to this platform, a saved
    /// card cannot be a number kept here; it is a token kept by them and a
    /// reference kept by us. Null on a card typed into the built-in stand-in.
    /// </summary>
    public string? GatewayTokenSealed { get; set; }

    /// <summary>Which gateway holds it — vnpay today, and null for a stand-in card.</summary>
    public string? Provider { get; set; }

    /// <summary>
    /// True when the card lives at a gateway. Its expiry is theirs to know: the
    /// token API returns a masked number and a family, and no expiry date at all.
    /// </summary>
    public bool IsGatewayHeld => !string.IsNullOrEmpty(GatewayTokenSealed);
}

/// <summary>docs/07 §4 — what may be kept about a card, and what may be done with it.</summary>
public static class SavedCards
{
    /* ------------------------------------------------- reading the number */

    /// <summary>
    /// The brand, from the prefix ranges the schemes publish. Read once, at the
    /// moment the guest types the number, and then the number is gone.
    /// </summary>
    public static CardBrand BrandOf(string? number)
    {
        var digits = Digits(number);
        if (digits.Length < 6) return CardBrand.Unknown;

        var two = int.Parse(digits[..2]);
        var four = int.Parse(digits[..4]);

        if (digits[0] == '4') return CardBrand.Visa;
        if (two is >= 51 and <= 55 || four is >= 2221 and <= 2720) return CardBrand.Mastercard;
        if (two is 34 or 37) return CardBrand.Amex;
        if (four is >= 3528 and <= 3589) return CardBrand.Jcb;

        // NAPAS domestic cards carry the 9704 issuer prefix.
        if (four == 9704) return CardBrand.Napas;

        return CardBrand.Unknown;
    }

    public static string BrandLabel(CardBrand brand) => brand switch
    {
        CardBrand.Visa => "Visa",
        CardBrand.Mastercard => "Mastercard",
        CardBrand.Jcb => "JCB",
        CardBrand.Amex => "American Express",
        CardBrand.Napas => "Thẻ nội địa NAPAS",
        _ => "Thẻ"
    };

    /// <summary>
    /// The Luhn check every scheme's numbers satisfy. Catching a typo here saves
    /// the guest a refusal they would read as their bank saying no.
    /// </summary>
    public static bool IsPlausibleNumber(string? number)
    {
        var digits = Digits(number);
        if (digits.Length is < 12 or > 19) return false;

        var sum = 0;
        var doubling = false;

        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var d = digits[i] - '0';
            if (doubling)
            {
                d *= 2;
                if (d > 9) d -= 9;
            }
            sum += d;
            doubling = !doubling;
        }

        return sum % 10 == 0;
    }

    public static string Last4Of(string? number)
    {
        var digits = Digits(number);
        return digits.Length >= 4 ? digits[^4..] : "";
    }

    private static string Digits(string? raw) => new((raw ?? "").Where(char.IsDigit).ToArray());

    /* ------------------------------------------------------------- expiry */

    /// <summary>A card is good until the end of its expiry month.</summary>
    public static DateOnly ExpiresAfter(int month, int year) =>
        month is < 1 or > 12
            ? DateOnly.MinValue
            : new DateOnly(year, month, 1).AddMonths(1).AddDays(-1);

    /// <summary>
    /// A card held at a gateway has no expiry here to compare against — VNPay's
    /// token API returns a masked number and a family and no date. Calling that
    /// "expired" would hide a perfectly good card; the gateway refuses the token
    /// when the card really has expired, and that refusal is what the guest sees.
    /// </summary>
    public static bool ExpiryKnown(SavedCard card) =>
        !card.IsGatewayHeld && card.ExpiryMonth is >= 1 and <= 12 && card.ExpiryYear >= 2000;

    public static bool IsExpired(SavedCard card, DateOnly today) =>
        ExpiryKnown(card) && today > ExpiresAfter(card.ExpiryMonth, card.ExpiryYear);

    /// <summary>docs/07 §4 — "nhắc khách cập nhật trước 14 ngày".</summary>
    public const int ExpiryNoticeDays = 14;

    public static bool ExpiringSoon(SavedCard card, DateOnly today) =>
        ExpiryKnown(card)
        && !IsExpired(card, today)
        && ExpiresAfter(card.ExpiryMonth, card.ExpiryYear) <= today.AddDays(ExpiryNoticeDays);

    public static string ExpiryLabel(SavedCard card) =>
        ExpiryKnown(card) ? $"{card.ExpiryMonth:00}/{card.ExpiryYear % 100:00}" : "Do cổng thanh toán giữ";

    public static string Display(SavedCard card) =>
        $"{BrandLabel(card.Brand)} •••• {card.Last4} · {ExpiryLabel(card)}";

    public static string ExpiryReminder(SavedCard card) =>
        $"{BrandLabel(card.Brand)} •••• {card.Last4} hết hạn {ExpiryLabel(card)} và đang có lịch thu tự động. " +
        "Hãy cập nhật thẻ trước ngày thu để đơn không bị huỷ.";

    /* ------------------------------------------------------------- delete */

    /// <summary>Why a card cannot be removed yet.</summary>
    public enum RemovalBlock
    {
        None = 0,
        /// <summary>The card is on a booking that has not finished.</summary>
        OpenBooking = 1,
        /// <summary>The card has money still to be taken from it.</summary>
        ScheduledCharge = 2
    }

    /// <summary>
    /// docs/07 §4 — "Xoá thẻ: chặn nếu thẻ đó đang gắn với đơn chưa hoàn tất
    /// hoặc còn lịch thu tự động." The scheduled charge is named first: it is the
    /// one that would take money the guest thought they had cancelled.
    /// </summary>
    public static RemovalBlock CanRemove(bool hasScheduledCharge, bool hasOpenBooking) =>
        hasScheduledCharge ? RemovalBlock.ScheduledCharge
        : hasOpenBooking ? RemovalBlock.OpenBooking
        : RemovalBlock.None;

    public static string RemovalMessage(RemovalBlock block) => block switch
    {
        RemovalBlock.ScheduledCharge =>
            "Thẻ này còn lịch thu tự động. Hãy thêm thẻ khác và đặt làm mặc định trước, rồi xoá thẻ này.",
        RemovalBlock.OpenBooking =>
            "Thẻ này đang gắn với một đơn chưa hoàn tất. Hãy thêm thẻ khác và đặt làm mặc định trước, rồi xoá thẻ này.",
        _ => ""
    };

    /* ------------------------------------------------------------ default */

    /// <summary>
    /// The first card a guest saves is their default, and removing the default
    /// hands the title to the next one — never leaves them with none while cards
    /// remain, because the automatic charge of §6 has to have something to use.
    /// </summary>
    public static void Reseat(IReadOnlyList<SavedCard> cards, int? preferredId = null)
    {
        if (cards.Count == 0) return;

        var chosen = cards.FirstOrDefault(c => c.Id == preferredId)
                     ?? cards.FirstOrDefault(c => c.IsDefault)
                     ?? cards[0];

        foreach (var card in cards) card.IsDefault = card == chosen;
    }
}
