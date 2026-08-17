using System.Text.Json;
using Microsoft.Extensions.Options;
using StayHost.Domain;

namespace StayHost.Web.Services.Gateways;

/// <summary>
/// docs/07 §13 phương án A — VNPay, the one gateway that covers both rows at the
/// top of the checkout: an international card (Visa / Mastercard / JCB / Amex)
/// and a domestic ATM card over NAPAS.
///
/// Nothing here ever sees a card number. The guest leaves for VNPay's own page,
/// types it there, and comes back with a signed answer — which is what docs/07
/// §14.1–2 requires and what the hand-written card form in this build was not.
/// </summary>
public class VnPayProvider(
    IOptions<PspSettings> options, IHttpClientFactory http, ILogger<VnPayProvider> log)
    : IPspProvider
{
    private readonly PspSettings _psp = options.Value;
    private PspSettings.VnPayOptions Cfg => _psp.Vnpay;

    public string Key => Psp.VnPay;
    public bool IsConfigured => Cfg.IsConfigured;

    /// <summary>
    /// VNPay's own name for "let the guest pick" versus the two shortcuts the
    /// checkout already asked for. Sending the guest straight to the right list
    /// is the difference between the two rows meaning something and both of them
    /// opening the same menu.
    /// </summary>
    private static string BankCode(string method) => method switch
    {
        "napas" => "VNBANK",   // domestic ATM / internet banking
        "card" => "INTCARD",   // Visa, Mastercard, JCB, Amex
        _ => ""
    };

    /// <summary>docs/07 §4 — whether this build may offer to keep a card at VNPay.</summary>
    public bool TokensEnabled => IsConfigured && Cfg.Tokens;

    public Task<PspStart> StartAsync(PspOrder order, CancellationToken ct)
    {
        if (!IsConfigured)
            return Task.FromResult(new PspStart(false, Error: "VNPay chưa được cấu hình."));

        // docs/07 §4 — keeping a card, or paying with one already kept, is a
        // different API on a different path with every parameter spelled
        // differently. Same secret, same sorted-query checksum.
        if (TokensEnabled && (order.SaveCard || order.Token is { Length: > 0 }))
            return Task.FromResult(StartToken(order));

        var now = Psp.Vn(DateTime.UtcNow);

        var fields = new Dictionary<string, string>
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = Cfg.TmnCode,
            ["vnp_Amount"] = Psp.VnPayAmount(order.Amount).ToString(),
            ["vnp_CurrCode"] = "VND",
            ["vnp_TxnRef"] = order.OrderRef,
            ["vnp_OrderInfo"] = order.Description,
            ["vnp_OrderType"] = "other",
            ["vnp_Locale"] = "vn",
            ["vnp_ReturnUrl"] = $"{_psp.PublicUrl}/api/payments/vnpay/return",
            ["vnp_IpAddr"] = order.ClientIp,
            ["vnp_CreateDate"] = now.ToString("yyyyMMddHHmmss"),
            // The gateway closes its own window at the same moment the booking
            // stops holding the dates, so a guest cannot pay for a stay that has
            // already gone back on sale.
            ["vnp_ExpireDate"] = now.Add(PaymentSession.Window).ToString("yyyyMMddHHmmss")
        };

        var bank = BankCode(order.Method);
        if (bank.Length > 0) fields["vnp_BankCode"] = bank;

        var query = Psp.VnPayQuery(fields);
        var hash = Psp.VnPaySign(fields, Cfg.HashSecret);

        return Task.FromResult(new PspStart(true, $"{Cfg.PayUrl}?{query}&vnp_SecureHash={hash}"));
    }

    /// <summary>
    /// docs/07 §4 — pay, and keep the card at VNPay for next time. Or pay with
    /// one already kept.
    ///
    /// Every parameter name here is lower-case with underscores. That is not a
    /// typo carried over from somewhere: VNPay's token API genuinely spells them
    /// differently from its payment API, and mixing the two gets a blank error
    /// page with no reason on it. The checksum is the same sorted query — which
    /// their documentation does not say, and which was settled by sending their
    /// sandbox one request per candidate rule and seeing which reached a payment
    /// page rather than <c>error.html</c>.
    /// </summary>
    private PspStart StartToken(PspOrder order)
    {
        var now = Psp.Vn(DateTime.UtcNow);
        var back = $"{_psp.PublicUrl}/api/payments/vnpay/token-return";

        var fields = new Dictionary<string, string>
        {
            ["vnp_version"] = "2.1.0",
            ["vnp_command"] = order.Token is { Length: > 0 }
                ? Psp.VnPayTokenPayCommand
                : Psp.VnPayCreateTokenCommand,
            ["vnp_tmn_code"] = Cfg.TmnCode,
            ["vnp_app_user_id"] = order.UserRef ?? "guest",
            ["vnp_locale"] = "vn",
            ["vnp_card_type"] = Psp.VnPayCardType(order.Method),
            ["vnp_txn_ref"] = order.OrderRef,
            ["vnp_amount"] = Psp.VnPayAmount(order.Amount).ToString(),
            ["vnp_curr_code"] = "VND",
            ["vnp_txn_desc"] = order.Description,
            ["vnp_return_url"] = back,
            ["vnp_cancel_url"] = back,
            ["vnp_ip_addr"] = order.ClientIp,
            ["vnp_create_date"] = now.ToString("yyyyMMddHHmmss"),
            ["vnp_store_token"] = "1"
        };

        if (order.Token is { Length: > 0 }) fields["vnp_token"] = order.Token;

        var url = order.Token is { Length: > 0 } ? Cfg.TokenPayUrl : Cfg.CreateTokenUrl;

        return new PspStart(true,
            $"{url}?{Psp.VnPayQuery(fields)}&vnp_secure_hash={Psp.VnPaySign(fields, Cfg.HashSecret)}");
    }

    public PspVerdict Read(IReadOnlyDictionary<string, string> payload)
    {
        if (!Psp.VnPayVerify(payload, Cfg.HashSecret))
        {
            log.LogWarning("VNPay callback for {Ref} failed its signature check.",
                payload.GetValueOrDefault("vnp_TxnRef") ?? payload.GetValueOrDefault("vnp_txn_ref"));
            return PspVerdict.Forged;
        }

        // The token API answers in its own spelling, so every field is looked up
        // under both. One Read for both APIs, because everything after the
        // spelling is identical and two copies would drift.
        string? Field(string pay, string token) =>
            payload.GetValueOrDefault(pay) ?? payload.GetValueOrDefault(token);

        var code = Field("vnp_ResponseCode", "vnp_response_code") ?? "";
        var status = Field("vnp_TransactionStatus", "vnp_transaction_status") ?? code;
        var txn = Field("vnp_TransactionNo", "vnp_transaction_no");

        // vnp_Amount comes back in the same ×100 unit it was sent in.
        var amount = decimal.TryParse(Field("vnp_Amount", "vnp_amount"), out var raw)
            ? raw / 100m
            : 0m;

        // docs/07 §4 — the only place this platform ever learns four digits of a
        // card, now that the number is typed on VNPay's page (§14.2).
        var last4 = Psp.Last4Of(payload.GetValueOrDefault("vnp_card_number"));
        var token = payload.GetValueOrDefault("vnp_token");
        var cardType = payload.GetValueOrDefault("vnp_card_type");

        if (code == "00" && status == "00")
            return new PspVerdict(PaymentSessionStatus.Paid, amount, txn, code,
                CardLast4: last4, CardToken: token, CardType: cardType);

        if (Psp.VnPayCancelled(code))
            return new PspVerdict(PaymentSessionStatus.Cancelled, amount, txn, code);

        return new PspVerdict(PaymentSessionStatus.Failed, amount, txn, code, Psp.VnPayDecline(code));
    }

    /// <summary>
    /// docs/07 §5 / TC-P-05 — querydr. The guest never came back, so VNPay is
    /// asked directly whether the money moved.
    /// </summary>
    public async Task<PspVerdict> QueryAsync(string orderRef, DateTime createdAtUtc, CancellationToken ct)
    {
        if (!IsConfigured) return PspVerdict.Unknown;

        var now = Psp.Vn(DateTime.UtcNow);
        var requestId = $"q{now:yyMMddHHmmssfff}";
        var created = Psp.Vn(createdAtUtc).ToString("yyyyMMddHHmmss");
        const string version = "2.1.0";
        const string command = "querydr";
        const string info = "Kiem tra giao dich";

        // A different checksum shape from the payment one: pipe-joined, fixed order.
        var checksum = Psp.VnPayApiSign(Cfg.HashSecret,
            requestId, version, command, Cfg.TmnCode, orderRef, created,
            now.ToString("yyyyMMddHHmmss"), "127.0.0.1", info);

        var body = new Dictionary<string, string>
        {
            ["vnp_RequestId"] = requestId,
            ["vnp_Version"] = version,
            ["vnp_Command"] = command,
            ["vnp_TmnCode"] = Cfg.TmnCode,
            ["vnp_TxnRef"] = orderRef,
            ["vnp_OrderInfo"] = info,
            ["vnp_TransactionDate"] = created,
            ["vnp_CreateDate"] = now.ToString("yyyyMMddHHmmss"),
            ["vnp_IpAddr"] = "127.0.0.1",
            ["vnp_SecureHash"] = checksum
        };

        try
        {
            using var client = http.CreateClient("psp");
            var res = await client.PostAsJsonAsync(Cfg.ApiUrl, body, ct);
            var json = await res.Content.ReadFromJsonAsync<JsonElement>(ct);

            var code = Text(json, "vnp_ResponseCode");
            var status = Text(json, "vnp_TransactionStatus");
            var amount = decimal.TryParse(Text(json, "vnp_Amount"), out var raw) ? raw / 100m : 0m;

            // 91 is "we have never heard of this order" — the guest never got as
            // far as paying. 94 means the query itself was duplicated; neither is
            // an answer about the money.
            if (code is "91" or "94") return PspVerdict.Unknown;

            if (code == "00" && status == "00")
                return new PspVerdict(PaymentSessionStatus.Paid, amount, Text(json, "vnp_TransactionNo"), code);

            if (status == "01") return PspVerdict.Unknown;    // still in progress

            return new PspVerdict(PaymentSessionStatus.Failed, amount,
                Text(json, "vnp_TransactionNo"), code, Psp.VnPayDecline(status));
        }
        catch (Exception ex)
        {
            // Not knowing is not the same as "not paid". A booking stays pending
            // and gets asked about again rather than being failed on a timeout.
            log.LogWarning(ex, "Không hỏi được VNPay về giao dịch {Ref}.", orderRef);
            return PspVerdict.Unknown;
        }
    }

    private static string Text(JsonElement json, string name) =>
        json.ValueKind == JsonValueKind.Object && json.TryGetProperty(name, out var v)
            ? v.ValueKind == JsonValueKind.Number ? v.ToString() : v.GetString() ?? ""
            : "";
}
