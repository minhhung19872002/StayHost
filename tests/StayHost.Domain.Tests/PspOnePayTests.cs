using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>
/// docs/07 §13 — OnePay's signature, pinned to a transaction that really happened.
///
/// The fixture below is not invented and not copied from a manual. It is the
/// reply OnePay's public test merchant sent back after a Visa card was
/// authorised on their sandbox on 18/08/2026, signature and all. That matters
/// because their documentation does not spell out three things that each fail
/// silently when guessed wrong: SHA-256 rather than SHA-512, a key read as
/// hexadecimal bytes rather than as text, and <c>AgainLink</c> excluded from the
/// signed set even though OnePay sends it back. A wrong guess does not throw —
/// it makes every payment look forged, or worse, makes a forgery look genuine.
/// </summary>
public class PspOnePayTests
{
    /// <summary>OnePay's published test merchant. Not a secret; it moves no real money.</summary>
    private const string Secret = "6D0870CDE5F24F34F3915FB0045120DB";

    private const string RealSignature =
        "717238E5247A72CF2A3BE6EB8A9082C4F47377A10D9D14957A29416ED797F1D5";

    /// <summary>The approved Visa payment, exactly as it came back.</summary>
    private static Dictionary<string, string> Approved() => new()
    {
        ["AgainLink"] = "https://staylio.bluestar.com.vn/",
        ["vpc_AVSResultCode"] = "1",
        ["vpc_AcqResponseCode"] = "00",
        ["vpc_Amount"] = "100000",
        ["vpc_AuthorizeId"] = "853818",
        ["vpc_BatchNo"] = "20260818",
        ["vpc_CSCResultCode"] = "M",
        ["vpc_Card"] = "VC",
        ["vpc_CardNum"] = "400555xxxxxx0001",
        ["vpc_Command"] = "pay",
        ["vpc_Locale"] = "vn",
        ["vpc_MerchTxnRef"] = "SHTEST1787025864",
        ["vpc_Merchant"] = "TESTONEPAY",
        ["vpc_Message"] = "Approved",
        ["vpc_OrderInfo"] = "STAYHOSTVISAPROBE",
        ["vpc_ReceiptNo"] = "877772",
        ["vpc_TransactionNo"] = "128450979",
        ["vpc_TxnResponseCode"] = "0",
        ["vpc_VerSecurityLevel"] = "07",
        ["vpc_VerType"] = "2D",
        ["vpc_Version"] = "2",
        ["vpc_SecureHash"] = RealSignature
    };

    [Fact]
    public void A_real_reply_from_OnePay_verifies()
    {
        Assert.True(Psp.OnePayVerify(Approved(), Secret));
    }

    /// <summary>
    /// The whole point of checking one. An attacker who cannot reach the card
    /// form can still open the return URL and put whatever they like in it.
    /// </summary>
    [Fact]
    public void An_edited_reply_does_not()
    {
        var tampered = Approved();
        tampered["vpc_Amount"] = "1";

        Assert.False(Psp.OnePayVerify(tampered, Secret));
    }

    [Fact]
    public void A_reply_with_no_signature_at_all_does_not()
    {
        var bare = Approved();
        bare.Remove("vpc_SecureHash");

        Assert.False(Psp.OnePayVerify(bare, Secret));
    }

    /// <summary>
    /// <c>AgainLink</c> is the trap. OnePay sends it, it is not signed, and
    /// including it in the rebuild produces a mismatch on every genuine reply —
    /// which reads exactly like a wrong secret. Changing it must not matter.
    /// </summary>
    [Fact]
    public void The_unsigned_fields_OnePay_sends_do_not_affect_the_check()
    {
        var moved = Approved();
        moved["AgainLink"] = "https://somewhere.else/";

        Assert.True(Psp.OnePayVerify(moved, Secret));
    }

    /// <summary>
    /// A caller cannot break the check by hanging extra parameters off the
    /// return URL either — but anything named <c>vpc_</c> is signed, so adding
    /// one of those must fail rather than be ignored.
    /// </summary>
    [Fact]
    public void An_added_vpc_field_invalidates_the_signature()
    {
        var extra = Approved();
        extra["vpc_Extra"] = "1";

        Assert.False(Psp.OnePayVerify(extra, Secret));
    }

    [Fact]
    public void The_signature_this_platform_produces_is_the_one_OnePay_produced()
    {
        var fields = Approved();
        fields.Remove("vpc_SecureHash");

        Assert.Equal(RealSignature, Psp.OnePaySign(fields, Secret));
    }

    /// <summary>
    /// Their success code is one character. Reading it as VNPay's two-character
    /// "00" would treat every approved payment as a decline.
    /// </summary>
    [Fact]
    public void Only_a_single_zero_means_the_money_moved()
    {
        Assert.True(Psp.OnePayPaid("0"));
        Assert.False(Psp.OnePayPaid("00"));
        Assert.False(Psp.OnePayPaid("1"));
        Assert.False(Psp.OnePayPaid(null));
    }

    /// <summary>
    /// docs/07 §4 — the difference that made OnePay worth wiring: four digits of
    /// the card, on an ordinary payment, with no token API in sight.
    /// </summary>
    [Fact]
    public void The_last_four_digits_arrive_on_an_ordinary_payment()
    {
        Assert.Equal("0001", Psp.OnePayLast4("400555xxxxxx0001"));
    }

    /// <summary>
    /// docs/07 §10 — a lost call is "unknown", never "refused". Refused sends
    /// the guest's money to their StayHost balance, so mistaking one for the
    /// other after a refund that did land pays them twice.
    /// </summary>
    [Fact]
    public void A_refund_with_no_answer_is_unknown_rather_than_refused()
    {
        Assert.Equal(Psp.RefundOutcome.Unknown, Psp.OnePayRefundOutcome(null));
        Assert.Equal(Psp.RefundOutcome.Unknown, Psp.OnePayRefundOutcome(""));
        Assert.Equal(Psp.RefundOutcome.Accepted, Psp.OnePayRefundOutcome("0"));
        Assert.Equal(Psp.RefundOutcome.Refused, Psp.OnePayRefundOutcome("2"));
    }

    /// <summary>
    /// The two gateways share every credential, so nothing about a misrouted
    /// order looks wrong: the signature checks out, OnePay accepts it, and the
    /// guest simply arrives at a form their card cannot fill.
    /// </summary>
    [Fact]
    public void A_domestic_card_goes_to_the_domestic_gateway()
    {
        Assert.True(Psp.OnePayIsDomestic("napas"));
        Assert.False(Psp.OnePayIsDomestic("card"));
        Assert.False(Psp.OnePayIsDomestic(null));
    }

    [Fact]
    public void Amounts_go_out_in_dong_times_one_hundred()
    {
        Assert.Equal(100_000, Psp.OnePayAmount(1_000m));
        Assert.Equal(577_913_300, Psp.OnePayAmount(5_779_133m));
    }
}
