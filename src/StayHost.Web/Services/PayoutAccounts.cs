using StayHost.Domain;

namespace StayHost.Web.Services;

/// <summary>
/// docs/07 §14.3 — the one place that turns a host's bank account number into
/// something safe to store, and back again when a transfer file needs it.
///
/// The key comes from configuration and never from the database: a key kept
/// beside the thing it protects protects nothing. With none configured the
/// number is simply not stored — the same rule the social-login buttons and the
/// VietQR account follow, and better than writing account numbers in the clear
/// because a setting was missed.
/// </summary>
public class PayoutAccounts(IConfiguration config, ILogger<PayoutAccounts> log)
{
    private readonly byte[]? _key = SecretText.ReadKey(config["Payouts:AccountKey"]);

    /// <summary>False when nothing can be sealed, so callers can say why rather than fail quietly.</summary>
    public bool CanStore => _key is not null;

    /// <summary>
    /// Said to a host, and printed on the admin's transfer screen, when the
    /// platform is collecting money it has no way to forward.
    /// </summary>
    public const string NoKeyNotice =
        "Máy chủ chưa cấu hình khoá mã hoá tài khoản nhận tiền (Payouts:AccountKey), " +
        "nên số tài khoản không được lưu và chưa tạo được lệnh chuyển tiền.";

    public string? Seal(string? accountNumber)
    {
        if (_key is null || string.IsNullOrWhiteSpace(accountNumber)) return null;
        return SecretText.Seal(accountNumber.Trim(), _key);
    }

    /// <summary>
    /// The number itself. Null when there is no key, nothing stored, or the value
    /// will not open — which for a caller means the same thing every time: do not
    /// transfer to this host until somebody has looked.
    /// </summary>
    public string? Open(string? sealedText)
    {
        if (_key is null || string.IsNullOrWhiteSpace(sealedText)) return null;

        var plain = SecretText.Open(sealedText, _key);

        if (plain is null)
        {
            // A key that changed is the likely cause, and it is worth shouting
            // about: every host who saved an account under the old one is now
            // unpayable, and nothing else in the system would show it.
            log.LogError(
                "Không mở được số tài khoản nhận tiền đã mã hoá. Khoá Payouts:AccountKey có thể đã đổi.");
        }

        return plain;
    }
}
