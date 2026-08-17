using StayHost.Domain;

namespace StayHost.Web.Services.Gateways;

/// <summary>What a gateway was asked to collect.</summary>
public sealed record PspOrder(
    string OrderRef,
    decimal Amount,
    string Description,
    /// <summary>The method the guest picked, which decides the gateway's own sub-choice.</summary>
    string Method,
    string ClientIp);

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
    DeclineReason Decline = DeclineReason.Unknown)
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
}
