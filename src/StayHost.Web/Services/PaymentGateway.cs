using Microsoft.EntityFrameworkCore;
using StayHost.Domain;
using StayHost.Infrastructure;

namespace StayHost.Web.Services;

/// <summary>
/// Stands in for a real payment processor. There is no PSP behind this build,
/// so a charge succeeds unless the card was set up to be refused — which is the
/// only way to exercise the failed-second-charge path of docs/03 §1 end to end.
/// </summary>
public class PaymentGateway(ILogger<PaymentGateway> log, StayHostDbContext db)
{
    /// <summary>A card ending in these four digits always declines.</summary>
    public const string DecliningCard = "0000";

    /// <remarks>
    /// docs/07 §8 — a refusal names a reason rather than carrying a sentence.
    /// The wording belongs to <see cref="Payments.Message"/>, so every path that
    /// takes money says the same thing about the same failure.
    /// </remarks>
    public record Result(bool Ok, DeclineReason Decline = DeclineReason.Unknown)
    {
        public string? Reason => Ok ? null : Payments.Message(Decline);
    }

    /// <summary>
    /// docs/07 §5 — a card ending in these four digits always sends the guest to
    /// their bank's OTP page.
    ///
    /// In reality the issuer decides, and for Vietnamese cards the answer is
    /// nearly always yes. There is no 3-D Secure server behind this build, so
    /// putting every payment through a page that accepts any code would be worse
    /// than useless — it would look like authentication without being it. A test
    /// card selects the branch instead, the same way <see cref="DecliningCard"/>
    /// selects the refusal branch. Wiring a real PSP means making this true for
    /// every card.
    /// </summary>
    public const string AuthenticatingCard = "0002";

    /// <summary>The OTP the stand-in accepts. A real one comes from the guest's bank.</summary>
    public const string TestOtp = "123456";

    /// <summary>
    /// What this gateway remembers charging, keyed by the attempt key — which is
    /// what makes the self-check of docs/07 §5 possible: the platform can ask
    /// what really happened rather than believing the browser.
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, decimal> _charged = new();

    public Result Charge(decimal amount, string method, string? cardLast4, string? key = null)
    {
        if (amount <= 0) return new Result(true);

        // Already taken under this key — which is what happens after a 3-D Secure
        // authorisation: the bank moved the money on its own page, and this call
        // is the platform catching up. Charging again would be the exact fault
        // docs/07 §7 calls the worst one in the module.
        if (key is not null && (_charged.ContainsKey(key) || db.GatewayCharges.Any(c => c.Reference == key)))
            return new Result(true);

        if (cardLast4 == DecliningCard)
        {
            log.LogInformation("Charge of {Amount} refused: test card.", amount);
            return new Result(false, DeclineReason.BankRefused);
        }

        if (key is not null)
        {
            _charged[key] = amount;

            // The gateway's own book. Business code never writes here — that is
            // what makes the daily reconciliation of docs/07 §7 mean something.
            db.GatewayCharges.Add(new GatewayCharge { Reference = key, Amount = amount, Method = method });
            db.SaveChanges();
        }

        log.LogInformation("Charged {Amount} via {Method}.", amount, method);
        return new Result(true);
    }

    /// <summary>docs/07 §5 — does this card send the guest to their bank first?</summary>
    public bool NeedsAuthentication(string method, string? cardLast4) =>
        PaymentMethods.IsCard(method) && cardLast4 == AuthenticatingCard;

    /// <summary>
    /// docs/07 §5 — "hệ thống phải tự kiểm tra lại kết quả với cổng thanh toán".
    /// Returns what the gateway knows about an attempt, or null when it has
    /// never heard of it.
    /// </summary>
    public AuthOutcome? Lookup(string key) =>
        _charged.ContainsKey(key) || db.GatewayCharges.Any(c => c.Reference == key)
            ? AuthOutcome.Succeeded
            : null;

    /// <summary>
    /// docs/07 §7 — the gateway's list of a day's transactions, which is one half
    /// of the daily reconciliation. The other half is the platform's own.
    /// </summary>
    public async Task<IReadOnlyList<Reconciliation.Record>> StatementAsync(DateOnly day, CancellationToken ct)
    {
        var from = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = from.AddDays(1);

        return await db.GatewayCharges
            .Where(c => c.ChargedAt >= from && c.ChargedAt < to)
            .Select(c => new Reconciliation.Record(c.Reference, c.Amount))
            .ToListAsync(ct);
    }

    /// <summary>The stand-in's OTP check. A real one happens on the bank's own page.</summary>
    public bool CodeAccepted(string? code) => code?.Trim() == TestOtp;

    /// <summary>
    /// Stands in for what happens on the bank's own page: the code is checked and,
    /// if it is right, the money moves — before the platform hears anything.
    ///
    /// That ordering is the whole point. It is why docs/07 §5 insists the platform
    /// ask the gateway rather than believe the browser, and why a guest whose
    /// connection dies on the way back has still been charged.
    /// </summary>
    public Result Authorise(string key, decimal amount, string method, string? cardLast4, string? code)
    {
        if (!CodeAccepted(code)) return new Result(false, DeclineReason.IncorrectDetails);
        return Charge(amount, method, cardLast4, key);
    }
}
