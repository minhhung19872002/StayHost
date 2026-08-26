using System.Security.Cryptography;
using System.Text;
using StayHost.Domain;

namespace StayHost.Domain.Tests;

/// <summary>
/// docs/07 §13 — the signatures a licensed gateway checks.
///
/// These are the tests that matter most in the payment module and the ones
/// hardest to notice missing: a wrong signature does not throw, it just makes
/// every payment fail, or — the direction that costs money — makes a forged
/// callback look genuine. So each gateway is checked twice: that a correct
/// payload passes, and that a payload somebody edited does not.
/// </summary>
public class PspTests
{
    private const string VnSecret = "SECRETKEY123456";
    private const string MomoAccess = "F8BBA842ECF85";
    private const string MomoSecret = "K951B6PE1waDMi640xX08PD3vg6EkVlz";
    private const string ZaloKey1 = "PcY4iZIKFCIdgZvA6ueMcMHHUbRLYjPL";
    private const string ZaloKey2 = "kLtgPl8HHhfvMTrAuDzcvAcO61sw2Vqc";

    /* -------------------------------------------------- the gateway's user id */

    /// <summary>
    /// docs/07 §15.5 — a token is created under this name and removed under it.
    /// VNPay answers a mismatch with the same code it uses for a card that has
    /// already gone, so a drift between the two would make every removal look
    /// like a success while the card stayed on their side.
    /// </summary>
    [Fact]
    public void The_gateway_knows_a_guest_by_one_name_only()
    {
        Assert.Equal("stayhost-4812", Psp.AppUserRef(4_812));
    }

    [Fact]
    public void Removing_a_token_accepts_both_done_and_already_gone()
    {
        Assert.True(Psp.VnPayTokenForgotten("00"));   // removed now
        Assert.True(Psp.VnPayTokenForgotten("11"));   // nothing there to remove
        Assert.False(Psp.VnPayTokenForgotten("97"));  // bad signature
        Assert.False(Psp.VnPayTokenForgotten(null));
    }

    /* ---------------------------------------------------------- the order ref */

    [Fact]
    public void An_order_reference_carries_the_booking_back_out_of_it()
    {
        var at = new DateTime(2026, 8, 17, 3, 30, 0, DateTimeKind.Utc);
        var reference = Psp.OrderRef(4_812, at, 0);

        Assert.Equal(20, reference.Length);
        Assert.Equal(4_812, Psp.BookingOf(reference));
    }

    /// <summary>
    /// ZaloPay hands its <c>app_trans_id</c> back exactly as it was given, prefix
    /// included, so the lookup has to survive that.
    /// </summary>
    [Fact]
    public void A_zalopay_transaction_id_still_names_its_booking()
    {
        var at = new DateTime(2026, 8, 17, 9, 0, 0, DateTimeKind.Utc);
        var transId = Psp.ZaloTransId(Psp.OrderRef(77, at, 3), at);

        Assert.StartsWith("260817_", transId);
        Assert.Equal(77, Psp.BookingOf(transId));
    }

    /// <summary>
    /// The seven-hour trap. A payment made at 01:00 UTC is already tomorrow in
    /// Ho Chi Minh City, and ZaloPay refuses an id prefixed with any date but its
    /// own today — so the prefix has to be Vietnam's, not the server's.
    /// </summary>
    [Fact]
    public void The_zalopay_date_prefix_is_vietnams_day_not_utcs()
    {
        var lateUtc = new DateTime(2026, 8, 16, 18, 30, 0, DateTimeKind.Utc);  // 01:30 on the 17th in VN

        Assert.StartsWith("260817_", Psp.ZaloTransId(Psp.OrderRef(1, lateUtc, 0), lateUtc));
    }

    [Fact]
    public void Nonsense_names_no_booking()
    {
        Assert.Null(Psp.BookingOf(null));
        Assert.Null(Psp.BookingOf(""));
        Assert.Null(Psp.BookingOf("not-a-reference"));
        Assert.Null(Psp.BookingOf("2608170330000048"));   // too short
    }

    /* ------------------------------------------------------------ the caller */

    /// <summary>
    /// VNPay wants <c>vnp_IpAddr</c> to be 7 to 45 characters. Kestrel says
    /// <c>::1</c> for anything local, VNPay answers a blank error page, and the
    /// page says nothing about which field it disliked — so it reads exactly like
    /// a bad signature. Cost a debugging session; hence the test.
    /// </summary>
    [Theory]
    [InlineData("::1", "127.0.0.1")]
    [InlineData("", "127.0.0.1")]
    [InlineData(null, "127.0.0.1")]
    [InlineData("::", "127.0.0.1")]
    [InlineData("::ffff:203.0.113.5", "203.0.113.5")]
    [InlineData("203.0.113.5", "203.0.113.5")]
    [InlineData("2001:0db8:85a3:0000:0000:8a2e:0370:7334", "2001:0db8:85a3:0000:0000:8a2e:0370:7334")]
    public void The_callers_address_is_given_in_a_shape_vnpay_accepts(string? raw, string expected)
    {
        var ip = Psp.ClientIp(raw);

        Assert.Equal(expected, ip);
        Assert.InRange(ip.Length, 7, 45);
    }

    /* ---------------------------------------------------------------- VNPay */

    private static Dictionary<string, string> VnFields() => new()
    {
        ["vnp_Version"] = "2.1.0",
        ["vnp_Command"] = "pay",
        ["vnp_TmnCode"] = "DEMOTMN1",
        ["vnp_Amount"] = "452786100",
        ["vnp_CurrCode"] = "VND",
        ["vnp_TxnRef"] = "26081710300000481200",
        ["vnp_OrderInfo"] = "StayHost SH1A2B3C4D",
        ["vnp_OrderType"] = "other",
        ["vnp_Locale"] = "vn",
        ["vnp_ReturnUrl"] = "http://localhost:5199/api/payments/vnpay/return",
        ["vnp_IpAddr"] = "127.0.0.1",
        ["vnp_CreateDate"] = "20260817103000"
    };

    /// <summary>
    /// The signed string is the sorted query, and it must be the *same* encoding
    /// the browser is sent — one space encoded differently on the two sides is
    /// the whole failure mode.
    /// </summary>
    [Fact]
    public void The_vnpay_signature_is_over_the_sorted_encoded_query()
    {
        var fields = VnFields();
        var query = Psp.VnPayQuery(fields);

        Assert.StartsWith("vnp_Amount=452786100&vnp_Command=pay&", query);
        Assert.Contains("vnp_OrderInfo=StayHost+SH1A2B3C4D", query);   // space, not %20
        Assert.DoesNotContain("vnp_SecureHash", query);

        var expected = HmacHex(HMACSHA512.HashData(
            Encoding.UTF8.GetBytes(VnSecret), Encoding.UTF8.GetBytes(query)));

        Assert.Equal(expected, Psp.VnPaySign(fields, VnSecret));
    }

    [Fact]
    public void A_vnpay_return_signed_with_the_right_key_is_accepted()
    {
        var payload = VnFields();
        payload["vnp_ResponseCode"] = "00";
        payload["vnp_TransactionStatus"] = "00";
        payload["vnp_TransactionNo"] = "14260817";
        payload["vnp_SecureHash"] = Psp.VnPaySign(payload, VnSecret);

        Assert.True(Psp.VnPayVerify(payload, VnSecret));
    }

    /// <summary>
    /// The one that matters: somebody who edits the amount in the address bar
    /// must not be able to confirm a stay for a different number.
    /// </summary>
    [Fact]
    public void A_vnpay_return_somebody_edited_is_refused()
    {
        var payload = VnFields();
        payload["vnp_ResponseCode"] = "00";
        payload["vnp_SecureHash"] = Psp.VnPaySign(payload, VnSecret);

        payload["vnp_Amount"] = "100";

        Assert.False(Psp.VnPayVerify(payload, VnSecret));
    }

    [Fact]
    public void A_vnpay_return_with_no_signature_at_all_is_refused()
    {
        Assert.False(Psp.VnPayVerify(VnFields(), VnSecret));
    }

    /// <summary>
    /// Extra parameters appended to the return URL are outside the vnp_ family
    /// and must not disturb the rebuild — otherwise anyone could break a genuine
    /// callback by adding ?x=1 to it.
    /// </summary>
    [Fact]
    public void Parameters_that_are_not_vnpays_do_not_break_the_check()
    {
        var payload = VnFields();
        payload["vnp_ResponseCode"] = "00";
        payload["vnp_SecureHash"] = Psp.VnPaySign(payload, VnSecret);
        payload["utm_source"] = "email";

        Assert.True(Psp.VnPayVerify(payload, VnSecret));
    }

    /// <summary>VNPay counts in đồng × 100 and will not take a decimal point.</summary>
    [Fact]
    public void Vnpay_amounts_are_dong_times_a_hundred()
    {
        Assert.Equal(452_786_100L, Psp.VnPayAmount(4_527_861m));
        Assert.Equal(100L, Psp.VnPayAmount(0.6m));      // rounds up, then scales
    }

    /// <summary>docs/07 §8 — the bank's code never reaches the guest, a reason does.</summary>
    [Theory]
    [InlineData("51", DeclineReason.InsufficientFunds)]
    [InlineData("65", DeclineReason.LimitExceeded)]
    [InlineData("09", DeclineReason.OnlinePaymentsOff)]
    [InlineData("12", DeclineReason.BankRefused)]
    public void Vnpay_codes_become_reasons_a_guest_can_act_on(string code, DeclineReason expected)
    {
        Assert.Equal(expected, Psp.VnPayDecline(code));
        Assert.NotEqual(Payments.Message(expected), code);
    }

    [Fact]
    public void Pressing_cancel_on_vnpays_page_is_not_a_refusal()
    {
        Assert.True(Psp.VnPayCancelled("24"));
        Assert.False(Psp.VnPayCancelled("51"));
    }

    /* ------------------------------------------------- §10, sending it back */

    /// <summary>
    /// The refund checksum is a pipe-joined list, not the sorted query the
    /// payment uses — and its order is not the order the fields are documented
    /// in either: the transaction number sits in the middle and the description
    /// comes last. Reproduced from VNPay's own written formula.
    /// </summary>
    [Fact]
    public void The_vnpay_refund_checksum_follows_their_written_field_order()
    {
        var signature = Psp.VnPayRefundSign(VnSecret,
            "r2608170048120000", "2.1.0", "refund", "DEMOTMN1", "02",
            "26081710300000481200", "452786100", "14260817", "20260817103500",
            "system", "20260817110000", "127.0.0.1", "Hoan tien don SH1A2B3C4D");

        var raw = "r2608170048120000|2.1.0|refund|DEMOTMN1|02|26081710300000481200|452786100" +
                  "|14260817|20260817103500|system|20260817110000|127.0.0.1|Hoan tien don SH1A2B3C4D";

        Assert.Equal(HmacHex(HMACSHA512.HashData(
            Encoding.UTF8.GetBytes(VnSecret), Encoding.UTF8.GetBytes(raw))), signature);
    }

    /// <summary>
    /// The distinction the whole refund path turns on. A response code of 00
    /// says "request accepted", not "money moved": a transaction status of 09 is
    /// the bank refusing a second later, and reading 00 alone as done would tell
    /// a guest their money is coming back when it is not.
    /// </summary>
    [Theory]
    [InlineData("00", "00", Psp.RefundOutcome.Accepted)]
    [InlineData("00", "05", Psp.RefundOutcome.Accepted)]
    [InlineData("00", "06", Psp.RefundOutcome.Accepted)]
    [InlineData("00", "09", Psp.RefundOutcome.Refused)]
    [InlineData("94", null, Psp.RefundOutcome.Accepted)]
    [InlineData("91", null, Psp.RefundOutcome.Refused)]
    [InlineData("95", null, Psp.RefundOutcome.Refused)]
    [InlineData("97", null, Psp.RefundOutcome.Unknown)]
    [InlineData("99", null, Psp.RefundOutcome.Unknown)]
    public void A_refund_reply_is_read_as_accepted_refused_or_unknown(
        string code, string? status, Psp.RefundOutcome expected)
    {
        Assert.Equal(expected, Psp.VnPayRefundOutcome(code, status));
    }

    /// <summary>
    /// A refused refund is docs/07 §10's own case and becomes balance. Not
    /// knowing must never be read as refused, or a guest gets balance for a
    /// refund that then lands on their card as well.
    /// </summary>
    [Fact]
    public void Not_knowing_is_not_the_same_as_being_refused()
    {
        Assert.NotEqual(Psp.RefundOutcome.Refused, Psp.VnPayRefundOutcome("99", null));
        Assert.NotEqual(Psp.RefundOutcome.Accepted, Psp.VnPayRefundOutcome("99", null));
    }

    /// <summary>
    /// Derived, not random: a retry after a lost reply has to be recognised as
    /// the same request, or the guest is refunded twice.
    /// </summary>
    [Fact]
    public void A_refund_request_id_is_the_same_on_a_retry()
    {
        var at = new DateTime(2026, 8, 17, 3, 30, 0, DateTimeKind.Utc);
        var first = Psp.RefundRequestId("26081710300000481200", 4_527_861m, at);
        var again = Psp.RefundRequestId("26081710300000481200", 4_527_861m, at.AddMinutes(20));

        Assert.Equal(first, again);
        Assert.InRange(first.Length, 1, 32);
        Assert.NotEqual(first, Psp.RefundRequestId("26081710300000481200", 1_000_000m, at));
    }

    [Fact]
    public void A_full_refund_and_a_partial_one_are_told_apart()
    {
        Assert.Equal("02", Psp.VnPayRefundType(4_527_861m, 4_527_861m));
        Assert.Equal("03", Psp.VnPayRefundType(1_000_000m, 4_527_861m));
    }

    /* ----------------------------------------------------------------- MoMo */

    /// <summary>
    /// MoMo's own documentation publishes this raw string. Reproducing it here is
    /// the point: the field order is alphabetical by name and nothing may be
    /// omitted, not even an empty extraData.
    /// </summary>
    [Fact]
    public void The_momo_create_signature_follows_their_published_field_order()
    {
        var signature = Psp.MoMoCreateSign(
            MomoAccess, MomoSecret, 50_000, "", "https://staylio.vn/ipn",
            "MM1540456472575", "SDK team.", "MOMO", "https://staylio.vn/return",
            "MM1540456472575", "captureWallet");

        var raw = $"accessKey={MomoAccess}&amount=50000&extraData=&ipnUrl=https://staylio.vn/ipn" +
                  "&orderId=MM1540456472575&orderInfo=SDK team.&partnerCode=MOMO" +
                  "&redirectUrl=https://staylio.vn/return&requestId=MM1540456472575" +
                  "&requestType=captureWallet";

        Assert.Equal(HmacHex(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(MomoSecret), Encoding.UTF8.GetBytes(raw))), signature);
    }

    /// <summary>A result signature is a different field list from a create one.</summary>
    [Fact]
    public void A_momo_result_signature_changes_when_the_amount_does()
    {
        var honest = Psp.MoMoResultSign(MomoAccess, MomoSecret, 4_527_861, "", "Successful.",
            "26081710300000481200", "StayHost", "momo_wallet", "MOMO", "webApp",
            "26081710300000481200", "1755400000000", "0", "2547283641");

        var edited = Psp.MoMoResultSign(MomoAccess, MomoSecret, 1, "", "Successful.",
            "26081710300000481200", "StayHost", "momo_wallet", "MOMO", "webApp",
            "26081710300000481200", "1755400000000", "0", "2547283641");

        Assert.NotEqual(honest, edited);
    }

    [Fact]
    public void Momo_says_zero_for_taken_and_a_thousand_for_undecided()
    {
        Assert.True(Psp.MoMoPaid(0));
        Assert.False(Psp.MoMoPaid(1006));
        Assert.True(Psp.MoMoPending(1000));
        Assert.True(Psp.MoMoCancelled(1006));
        Assert.Equal(DeclineReason.InsufficientFunds, Psp.MoMoDecline(1001));
    }

    /* -------------------------------------------------------------- ZaloPay */

    [Fact]
    public void The_zalopay_create_mac_is_seven_fields_pipe_joined()
    {
        var mac = Psp.ZaloCreateMac(ZaloKey1, "2553", "260817_26081710300000481200",
            "stayhost", 4_527_861, 1_755_400_000_000, "{}", "[]");

        var raw = "2553|260817_26081710300000481200|stayhost|4527861|1755400000000|{}|[]";

        Assert.Equal(HmacHex(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(ZaloKey1), Encoding.UTF8.GetBytes(raw))), mac);
    }

    /// <summary>
    /// The callback is signed with key2, not key1. Getting this wrong would mean
    /// rejecting every genuine callback — or, if the two were swapped the other
    /// way, accepting one signed with a key the whole world can see in a sample.
    /// </summary>
    [Fact]
    public void A_zalopay_callback_is_checked_against_the_second_key()
    {
        const string data = "{\"app_trans_id\":\"260817_26081710300000481200\",\"amount\":4527861}";
        var mac = HmacHex(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(ZaloKey2), Encoding.UTF8.GetBytes(data)));

        Assert.True(Psp.ZaloCallbackValid(ZaloKey2, data, mac));
        Assert.False(Psp.ZaloCallbackValid(ZaloKey1, data, mac));
        Assert.False(Psp.ZaloCallbackValid(ZaloKey2, data.Replace("4527861", "1"), mac));
    }

    /* ------------------------------------------------------------ the amount */

    /// <summary>
    /// docs/07 §7 — a gateway reporting a different amount than the booking is
    /// for is not a payment to act on. Rounding to the đồng is allowed; a
    /// thousand đồng is not.
    /// </summary>
    [Fact]
    public void A_gateway_reporting_a_different_amount_does_not_match()
    {
        Assert.True(Psp.AmountMatches(4_527_861m, 4_527_861m));
        Assert.True(Psp.AmountMatches(4_527_861m, 4_527_861.4m));
        Assert.False(Psp.AmountMatches(4_527_861m, 1_000m));
        Assert.False(Psp.AmountMatches(4_527_861m, 4_526_861m));
    }

    private static string HmacHex(byte[] hash)
    {
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
