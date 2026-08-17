using StayHost.Domain;

namespace StayHost.Web.Services;

/// <summary>
/// docs/07 §14.3 — a host's bank account number, sealed on the way in and opened
/// only where a transfer file needs it.
///
/// A thin face on <see cref="DataSecrets"/> so the call sites read as what they
/// are doing rather than as cryptography, and so the purpose string is written
/// once. Everything about key handling lives there.
/// </summary>
public class PayoutAccounts(DataSecrets secrets)
{
    /// <summary>False when nothing can be sealed, so callers can say why rather than fail quietly.</summary>
    public bool CanStore => secrets.CanStore;

    /// <summary>Said to a host, and on the admin's transfer screen, when nothing can be stored.</summary>
    public const string NoKeyNotice = DataSecrets.NoKeyNotice;

    public string? Seal(string? accountNumber) =>
        secrets.Seal(DataSecrets.PayoutAccount, accountNumber);

    /// <summary>
    /// The number itself. Null when there is no key, nothing stored, or the value
    /// will not open — which for a caller means the same thing every time: do not
    /// transfer to this host until somebody has looked.
    /// </summary>
    public string? Open(string? sealedText) =>
        secrets.Open(DataSecrets.PayoutAccount, sealedText);
}
