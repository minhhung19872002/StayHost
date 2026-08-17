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

    public Task<PspStart> StartAsync(PspOrder order, CancellationToken ct)
    {
        if (!IsConfigured)
            return Task.FromResult(new PspStart(false, Error: "VNPay chưa được cấu hình."));

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

    public PspVerdict Read(IReadOnlyDictionary<string, string> payload)
    {
        if (!Psp.VnPayVerify(payload, Cfg.HashSecret))
        {
            log.LogWarning("VNPay callback for {Ref} failed its signature check.",
                payload.GetValueOrDefault("vnp_TxnRef"));
            return PspVerdict.Forged;
        }

        var code = payload.GetValueOrDefault("vnp_ResponseCode") ?? "";
        var status = payload.GetValueOrDefault("vnp_TransactionStatus") ?? code;
        var txn = payload.GetValueOrDefault("vnp_TransactionNo");

        // vnp_Amount comes back in the same ×100 unit it was sent in.
        var amount = decimal.TryParse(payload.GetValueOrDefault("vnp_Amount"), out var raw)
            ? raw / 100m
            : 0m;

        if (code == "00" && status == "00")
            return new PspVerdict(PaymentSessionStatus.Paid, amount, txn, code);

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
