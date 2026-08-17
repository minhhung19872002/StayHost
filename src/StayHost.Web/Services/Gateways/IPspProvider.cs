using StayHost.Domain;

namespace StayHost.Web.Services.Gateways;

/// <summary>What a gateway was asked to collect.</summary>
public sealed record PspOrder(
    string OrderRef,
    decimal Amount,
    string Description,
    /// <summary>The method the guest picked, which decides the gateway's own sub-choice.</summary>
    string Method,
    string ClientIp,
    /// <summary>
    /// docs/07 §4 — the guest asked to keep this card for next time. On VNPay it
    /// selects the token API, which is also the only way this platform ever
    /// learns the card's last four digits (§14.2 took the card form away).
    /// </summary>
    bool SaveCard = false,
    /// <summary>Who is paying, as the gateway's token store will know them.</summary>
    string? UserRef = null,
    /// <summary>A card the guest already saved there, to charge instead of asking for a new one.</summary>
    string? Token = null);

/// <summary>Where to send the guest, or why they cannot go.</summary>
public sealed record PspStart(bool Ok, string? PayUrl = null, string? Error = null);

/// <summary>
/// What a gateway says about one order — from a return URL, an IPN, or an
/// answer to the platform's own question. The three are the same shape on
/// purpose: docs/07 §5 requires that the last of them can overrule the first.
/// </summary>
public sealed record PspVerdict(
    PaymentSessionStatus Status,
    decimal Amount = 0,
    string? TxnId = null,
    string? Code = null,
    DeclineReason Decline = DeclineReason.Unknown,
    /// <summary>
    /// docs/07 §4 — the card's last four digits, when the gateway told us. Null
    /// on an ordinary payment: the number was typed on their page and they say
    /// nothing about it unless a token was created.
    /// </summary>
    string? CardLast4 = null,
    /// <summary>The gateway's handle on that card, to be sealed before it is stored.</summary>
    string? CardToken = null,
    /// <summary>"01" domestic, "02" international — as much as VNPay will say.</summary>
    string? CardType = null,
    /// <summary>
    /// When the gateway says it took the money, in the gateway's own format.
    /// docs/07 §10 — VNPay's refund API asks for the original transaction's date
    /// back, and our clock is not the same thing as theirs.
    /// </summary>
    string? PaidAt = null)
{
    public static readonly PspVerdict Unknown = new(PaymentSessionStatus.Pending);

    /// <summary>
    /// A verdict that failed its signature check is not a verdict at all — so it
    /// says <em>Pending</em>, not Failed.
    ///
    /// The difference is the whole security of these routes. Nothing authenticates
    /// a caller posting to /api/payments/momo/ipn, so if an unsigned callback
    /// could mark a session failed, anyone who guessed an order reference could
    /// kill a stranger's payment mid-flight — and the guest who then paid for real
    /// would come back to a booking already written off. It is ignored instead,
    /// and the code is kept so the route can answer the gateway properly.
    /// </summary>
    public static PspVerdict Forged => new(PaymentSessionStatus.Pending, Code: Signature);

    /// <summary>Marks a payload whose signature did not check out.</summary>
    public const string Signature = "signature";
}

/// <summary>
/// docs/07 §13 — one licensed gateway. Three things are asked of it: send the
/// guest somewhere, read what it says when they come back, and answer honestly
/// when the platform asks a second time because nobody came back at all.
/// </summary>
public interface IPspProvider
{
    /// <summary>vnpay / momo / zalopay.</summary>
    string Key { get; }

    /// <summary>False when its keys were never filled in.</summary>
    bool IsConfigured { get; }

    Task<PspStart> StartAsync(PspOrder order, CancellationToken ct);

    /// <summary>Reads a signed return or IPN. Never trusts an unsigned one.</summary>
    PspVerdict Read(IReadOnlyDictionary<string, string> payload);

    /// <summary>
    /// docs/07 §5 — "hệ thống phải tự kiểm tra lại kết quả với cổng thanh toán,
    /// không tin vào việc khách quay về trang nào." This is that question.
    /// </summary>
    Task<PspVerdict> QueryAsync(string orderRef, DateTime createdAtUtc, CancellationToken ct);

    /// <summary>
    /// docs/07 §10 — send the guest's money back the way it came.
    ///
    /// Until this existed a cancellation asked the stand-in gateway, which said
    /// yes to everything: the booking was marked refunded, the ledger posted, the
    /// guest told — and with a live gateway not a đồng actually moved.
    /// </summary>
    Task<PspRefundResult> RefundAsync(PspRefund refund, CancellationToken ct);
}

/// <summary>
/// What the gateway answered about a refund.
///
/// The code and their transaction number are kept rather than thrown away
/// because docs/07 §7's reconciliation has to be able to see a refund at all: a
/// day where money went back and nothing recorded it is a day that will not
/// balance, and nobody would know why.
/// </summary>
public sealed record PspRefundResult(
    Psp.RefundOutcome Outcome, string? TxnId = null, string? Code = null);

/// <summary>
/// One refund, with everything the three gateways between them ask for about the
/// payment it is reversing.
/// </summary>
public sealed record PspRefund(
    string OrderRef,
    decimal Amount,
    /// <summary>What the whole payment was, so a gateway can be told full or partial.</summary>
    decimal OriginalAmount,
    /// <summary>The gateway's own transaction id from when the money came in.</summary>
    string? ProviderTxnId,
    /// <summary>When they said they took it, in their format. Null falls back to our clock.</summary>
    string? PaidAt,
    DateTime CreatedAtUtc,
    string Reason,
    /// <summary>Who asked — an admin's name, or the system. VNPay records it.</summary>
    string By);
