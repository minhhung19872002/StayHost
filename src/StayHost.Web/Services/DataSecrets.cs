using StayHost.Domain;

namespace StayHost.Web.Services;

/// <summary>
/// docs/07 §14.3 — the one place that seals anything the platform must keep but
/// must never be able to leak in readable form.
///
/// Two such things exist: a host's bank account number, without which their money
/// cannot be sent (§13), and a guest's card token at the gateway, without which
/// §4's saved cards cannot exist now that §14.2 has taken the card form away.
/// They share one configured key and nothing else — <see cref="SecretText.Derive"/>
/// gives each purpose an independent key, so a value lifted from one column
/// cannot be pasted into the other.
///
/// With no key configured nothing is stored. That is the same rule the
/// social-login buttons and the VietQR account follow, and it is better than
/// writing account numbers in the clear because a setting was missed.
/// </summary>
public class DataSecrets(IConfiguration config, ILogger<DataSecrets> log)
{
    private readonly byte[]? _master = SecretText.ReadKey(config["Payouts:AccountKey"]);

    /// <summary>Where a host's money goes.</summary>
    public const string PayoutAccount = "payout-account";

    /// <summary>The gateway's handle on a card the guest asked to keep.</summary>
    public const string CardToken = "card-token";

    public bool CanStore => _master is not null;

    /// <summary>
    /// Said to a host, and printed on the admin's transfer screen, when the
    /// platform is collecting money it has no way to forward.
    /// </summary>
    public const string NoKeyNotice =
        "Máy chủ chưa cấu hình khoá mã hoá (Payouts:AccountKey), nên số tài khoản " +
        "nhận tiền không được lưu và chưa tạo được lệnh chuyển tiền.";

    public string? Seal(string purpose, string? plain)
    {
        if (_master is null || string.IsNullOrWhiteSpace(plain)) return null;
        return SecretText.Seal(plain.Trim(), SecretText.Derive(_master, purpose));
    }

    /// <summary>
    /// Null when there is no key, nothing stored, or the value will not open —
    /// which for every caller means the same thing: this secret is not available,
    /// so do not act as though it were.
    /// </summary>
    public string? Open(string purpose, string? sealedText)
    {
        if (_master is null || string.IsNullOrWhiteSpace(sealedText)) return null;

        var plain = SecretText.Open(sealedText, SecretText.Derive(_master, purpose));

        if (plain is null)
        {
            // A key that changed is the likely cause, and it is worth shouting
            // about: every value already sealed under the old one is now
            // unreadable, and nothing else in the system would show it.
            log.LogError("Không mở được dữ liệu đã mã hoá ({Purpose}). " +
                         "Khoá Payouts:AccountKey có thể đã đổi.", purpose);
        }

        return plain;
    }
}
