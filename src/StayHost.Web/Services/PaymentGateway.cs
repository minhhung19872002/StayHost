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

    public record Result(bool Ok, string? Reason = null);

    public Result Charge(decimal amount, string method, string? cardLast4)
    {
        if (amount <= 0) return new Result(true);

        if (cardLast4 == DecliningCard)
        {
            log.LogInformation("Charge of {Amount} refused: test card.", amount);
            return new Result(false, "Ngân hàng từ chối giao dịch.");
        }

        log.LogInformation("Charged {Amount} via {Method}.", amount, method);
        return new Result(true);
    }
}
