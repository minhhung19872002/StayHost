using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>docs/07 §2.3 — the QR a guest's banking app has to be able to read.</summary>
public class VietQrTests
{
    /// <summary>MB Bank. The BIN is configuration in the app; fixed here so the
    /// expected payload below is a constant rather than a moving target.</summary>
    private static readonly VietQr.Beneficiary Mb = new("970422", "0123456789");

    [Fact]
    public void The_payload_carries_the_account_the_amount_and_the_memo()
    {
        var qr = VietQr.Payload(Mb, 2_672_000m, "SH1A2B3C4D");

        // EMVCo: two-digit tag, two-digit length, value. Read the pieces back out
        // rather than comparing one long string, so a failure says which field.
        Assert.StartsWith("000201", qr);          // format indicator
        Assert.Contains("010212", qr);            // used once, because the amount is fixed
        Assert.Contains("0010A000000727", qr);    // NAPAS
        Assert.Contains("0006970422", qr);        // bank
        Assert.Contains("01100123456789", qr);    // account
        Assert.Contains("0208QRIBFTTA", qr);      // to an account, not a card
        Assert.Contains("5303704", qr);           // đồng
        Assert.Contains("54072672000", qr);       // amount, whole đồng, no separators
        Assert.Contains("5802VN", qr);
        Assert.Contains("0810SH1A2B3C4D", qr);    // the memo, inside field 62
    }

    [Fact]
    public void The_checksum_covers_its_own_tag_and_length()
    {
        var qr = VietQr.Payload(Mb, 500_000m, "SH1A2B3C4D");

        // The last six characters are "6304" plus four hex digits, and the digits
        // are computed over everything before them *including* the "6304". Getting
        // that wrong is the classic VietQR bug: the string looks right and every
        // banking app refuses to read it, with no error anyone can see.
        var body = qr[..^4];
        Assert.EndsWith("6304", body);
        Assert.Equal(VietQr.Crc16(body), qr[^4..]);
    }

    [Fact]
    public void The_checksum_is_CRC16_CCITT_FALSE()
    {
        // The standard check value for this variant: "123456789" → 0x29B1.
        Assert.Equal("29B1", VietQr.Crc16("123456789"));
    }

    [Fact]
    public void Two_amounts_produce_two_different_codes()
    {
        // A guest must never be able to reuse yesterday's QR for today's price.
        Assert.NotEqual(
            VietQr.Payload(Mb, 500_000m, "SH1A2B3C4D"),
            VietQr.Payload(Mb, 500_001m, "SH1A2B3C4D"));
    }

    [Fact]
    public void The_amount_is_whole_dong_rounded_the_way_prices_are()
    {
        Assert.Contains("5406500001", VietQr.Payload(Mb, 500_000.5m, "SH1A2B3C4D"));
        Assert.Contains("5406500000", VietQr.Payload(Mb, 500_000.4m, "SH1A2B3C4D"));
    }

    [Fact]
    public void A_memo_is_reduced_to_what_a_transfer_form_can_carry()
    {
        // Diacritics and punctuation are dropped: this is a key to match on, not
        // a sentence to read.
        Assert.Equal("DATPHONGSH1A2B3C4D", VietQr.Sanitise("Đặt phòng SH1A2B3C4D"));
        Assert.Equal("", VietQr.Sanitise(null));
        Assert.Equal("", VietQr.Sanitise("   "));

        // Banks truncate long memos themselves; doing it here means the string in
        // the QR is the string that will come back on the statement.
        var long_ = VietQr.Sanitise(new string('A', 60));
        Assert.Equal(VietQr.MaxMemoLength, long_.Length);
    }

    [Fact]
    public void The_references_this_platform_issues_survive_untouched()
    {
        // Stays, services and tickets. All three are already capitals and digits,
        // which is why the memo needs no invented format of its own.
        Assert.Equal("SH1A2B3C4D", VietQr.Sanitise("SH1A2B3C4D"));
        Assert.Equal("SV7EA95836", VietQr.Sanitise("SV7EA95836"));
        Assert.Equal("XP2E5A975A", VietQr.Sanitise("XP2E5A975A"));
    }

    [Fact]
    public void A_beneficiary_missing_either_half_is_refused()
    {
        Assert.False(new VietQr.Beneficiary("", "0123456789").IsComplete);
        Assert.False(new VietQr.Beneficiary("970422", " ").IsComplete);
        Assert.True(Mb.IsComplete);

        Assert.Throws<ArgumentException>(() =>
            VietQr.Payload(new VietQr.Beneficiary("970422", ""), 100_000m, "SH1A2B3C4D"));
    }
}
