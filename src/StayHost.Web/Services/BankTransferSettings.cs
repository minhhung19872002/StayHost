using StayHost.Domain;

namespace StayHost.Web.Services;

/// <summary>
/// docs/07 §2.3 — the account a VietQR sends money to.
///
/// Off unless an account number is configured, the same way the translation
/// engine and the social-login buttons are off until somebody fills them in.
/// A payment method that appears but cannot be paid is worse than one that is
/// missing: the guest gets as far as a QR code that credits nobody.
/// </summary>
public class BankTransferSettings
{
    public string BankBin { get; set; } = "";
    public string BankName { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string AccountName { get; set; } = "";

    public VietQr.Beneficiary Beneficiary => new(BankBin, AccountNumber);

    /// <summary>Whether a guest may be offered this at all.</summary>
    public bool Enabled => Beneficiary.IsComplete;
}
