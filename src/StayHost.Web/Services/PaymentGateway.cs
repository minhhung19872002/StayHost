using StayHost.Domain;

namespace StayHost.Web.Services;

/// <summary>
/// Stands in for a real payment processor. There is no PSP behind this build,
/// so a charge succeeds unless the card was set up to be refused — which is the
/// only way to exercise the failed-second-charge path of docs/03 §1 end to end.
/// </summary>
public class PaymentGateway(ILogger<PaymentGateway> log)
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

    public Result Charge(decimal amount, string method, string? cardLast4)
    {
        if (amount <= 0) return new Result(true);

        if (cardLast4 == DecliningCard)
        {
            log.LogInformation("Charge of {Amount} refused: test card.", amount);
            return new Result(false, DeclineReason.BankRefused);
        }

        log.LogInformation("Charged {Amount} via {Method}.", amount, method);
        return new Result(true);
    }
}
