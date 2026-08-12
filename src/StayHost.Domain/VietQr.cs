using System.Text;

namespace StayHost.Domain;

/// <summary>
/// docs/07 §2.3 — the VietQR payload a guest's banking app reads.
///
/// This is not a picture and not a request to anybody: it is a string in the
/// EMVCo merchant-QR format, with the Vietnamese profile NAPAS defines on top of
/// it. The guest's app decodes it, fills in the beneficiary, the amount and the
/// transfer memo, and the money moves bank to bank. Nothing here talks to
/// anything — which is why it can be built and tested with no contract, no
/// gateway and no network.
///
/// The whole scheme rests on one thing: <b>a memo unique to the booking</b>.
/// That is what turns an anonymous credit on a bank statement into "this is
/// order SH1A2B3C4D, paid". Encoding it in the QR is what stops the guest
/// mistyping it, which is the difference between matching every payment
/// automatically and running a queue of unidentified money.
/// </summary>
public static class VietQr
{
    /// <summary>NAPAS's identifier inside the EMVCo merchant-account field.</summary>
    private const string NapasGuid = "A000000727";

    /// <summary>Transfer to an account number, as opposed to a card number.</summary>
    private const string ToAccount = "QRIBFTTA";

    /// <summary>ISO 4217 numeric for the đồng.</summary>
    private const string Vnd = "704";

    /// <summary>
    /// A memo has to survive a bank's own transfer form, which is ASCII and
    /// short. The booking references this platform issues — SH1A2B3C4D,
    /// SV7EA95836, XP2E5A975A — already are, so nothing has to be mangled.
    /// </summary>
    public const int MaxMemoLength = 25;

    /// <summary>Where the money is going, and under whose name.</summary>
    public sealed record Beneficiary(string BankBin, string AccountNumber)
    {
        /// <summary>Both halves have to be there for a QR to mean anything.</summary>
        public bool IsComplete =>
            !string.IsNullOrWhiteSpace(BankBin) && !string.IsNullOrWhiteSpace(AccountNumber);
    }

    /// <summary>
    /// Strips a memo down to what a bank transfer form will carry: capitals and
    /// digits only. Vietnamese diacritics are dropped rather than transliterated
    /// — a memo is a key to match on, not a sentence to read, and the references
    /// this is used with never contain any.
    /// </summary>
    public static string Sanitise(string? memo)
    {
        if (string.IsNullOrWhiteSpace(memo)) return "";

        var plain = SearchText.Normalize(memo).ToUpperInvariant();
        var kept = new string(plain.Where(char.IsLetterOrDigit).ToArray());

        return kept.Length <= MaxMemoLength ? kept : kept[..MaxMemoLength];
    }

    /// <summary>
    /// The payload string, ready to be drawn as a QR.
    ///
    /// The amount is baked in, so the initiation method is "12" — used once —
    /// rather than "11". A static QR would let a guest pay any amount they liked
    /// and leave the platform to work out what they meant.
    /// </summary>
    public static string Payload(Beneficiary to, decimal amount, string? memo)
    {
        if (!to.IsComplete)
            throw new ArgumentException("VietQR cần mã ngân hàng và số tài khoản.", nameof(to));

        var account = Field("00", to.BankBin) + Field("01", to.AccountNumber);
        var merchant = Field("00", NapasGuid) + Field("01", account) + Field("02", ToAccount);

        var body = new StringBuilder()
            .Append(Field("00", "01"))                 // payload format indicator
            .Append(Field("01", "12"))                 // dynamic: this QR is for one payment
            .Append(Field("38", merchant))
            .Append(Field("53", Vnd))
            .Append(Field("54", Whole(amount)))
            .Append(Field("58", "VN"))
            .Append(Field("62", Field("08", Sanitise(memo))))
            .Append("6304")                            // the CRC's own tag and length…
            .ToString();

        // …which is included in what the CRC covers. That is the one part of the
        // format people get wrong, and a wrong CRC is a QR every banking app
        // silently refuses to read.
        return body + Crc16(body);
    }

    /// <summary>
    /// The đồng has no minor unit, so the amount travels as whole numbers with
    /// no separator. Rounding away from zero matches Pricing.Round, so the QR
    /// asks for exactly what the quote said.
    /// </summary>
    private static string Whole(decimal amount) =>
        Math.Round(amount, 0, MidpointRounding.AwayFromZero).ToString("0");

    /// <summary>One EMVCo field: two-digit tag, two-digit length, the value.</summary>
    private static string Field(string tag, string value) =>
        $"{tag}{value.Length:D2}{value}";

    /// <summary>
    /// CRC-16/CCITT-FALSE — polynomial 0x1021, seeded 0xFFFF, no reflection and
    /// no final xor. Four uppercase hex digits.
    /// </summary>
    public static string Crc16(string input)
    {
        ushort crc = 0xFFFF;

        foreach (var b in Encoding.ASCII.GetBytes(input))
        {
            crc ^= (ushort)(b << 8);
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }

        return crc.ToString("X4");
    }
}
