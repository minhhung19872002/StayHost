using System.Security.Cryptography;
using System.Text;

namespace StayHost.Domain;

/// <summary>
/// docs/07 §14.3 — "Thông tin tài khoản nhận tiền của chủ nhà phải được mã hoá
/// khi lưu, chỉ hiện 4 số cuối trên giao diện."
///
/// The build had read that as "do not keep it": <c>SavePayout</c> threw the
/// number away and stored four digits. Which is airtight, and also means the
/// platform cannot send a host their money — it has collected the guest's
/// payment and has nowhere to forward it to. The rule says encrypted at rest and
/// masked on screen; those are two different things.
///
/// AES-GCM, so a tampered ciphertext fails to open rather than decrypting to
/// something else. The output carries its own nonce and tag, which is what makes
/// a single opaque column enough.
/// </summary>
public static class SecretText
{
    private const int NonceBytes = 12;   // AES-GCM standard
    private const int TagBytes = 16;

    /// <summary>How long a key has to be, in bytes, before it is a key at all.</summary>
    public const int KeyBytes = 32;

    /// <summary>
    /// Reads a configured key. Base64 or hex, and anything that is not exactly
    /// <see cref="KeyBytes"/> long is refused rather than stretched — a short key
    /// silently padded is the kind of thing that looks like encryption for years.
    /// </summary>
    public static byte[]? ReadKey(string? configured)
    {
        var raw = (configured ?? "").Trim();
        if (raw.Length == 0) return null;

        byte[] bytes;

        try
        {
            bytes = raw.Length == KeyBytes * 2 && raw.All(Uri.IsHexDigit)
                ? Convert.FromHexString(raw)
                : Convert.FromBase64String(raw);
        }
        catch (FormatException)
        {
            return null;
        }

        return bytes.Length == KeyBytes ? bytes : null;
    }

    /// <summary>A key to paste into configuration. Printed by nothing; used by tests and by hand.</summary>
    public static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(KeyBytes));

    public static string Seal(string plain, byte[] key)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var text = Encoding.UTF8.GetBytes(plain);
        var cipher = new byte[text.Length];
        var tag = new byte[TagBytes];

        using var aes = new AesGcm(key, TagBytes);
        aes.Encrypt(nonce, text, cipher, tag);

        var packed = new byte[NonceBytes + TagBytes + cipher.Length];
        nonce.CopyTo(packed, 0);
        tag.CopyTo(packed, NonceBytes);
        cipher.CopyTo(packed, NonceBytes + TagBytes);

        return Convert.ToBase64String(packed);
    }

    /// <summary>
    /// Null when the value cannot be opened with this key — a different key, a
    /// truncated column, a value somebody edited in the database. Never an
    /// exception, because the caller's answer is the same in every case: this
    /// host's account number is not available, so nothing may be transferred to
    /// it.
    /// </summary>
    public static string? Open(string? sealedText, byte[] key)
    {
        if (string.IsNullOrWhiteSpace(sealedText)) return null;

        byte[] packed;
        try { packed = Convert.FromBase64String(sealedText); }
        catch (FormatException) { return null; }

        if (packed.Length <= NonceBytes + TagBytes) return null;

        var nonce = packed.AsSpan(0, NonceBytes);
        var tag = packed.AsSpan(NonceBytes, TagBytes);
        var cipher = packed.AsSpan(NonceBytes + TagBytes);
        var plain = new byte[cipher.Length];

        try
        {
            using var aes = new AesGcm(key, TagBytes);
            aes.Decrypt(nonce, cipher, tag, plain);
        }
        catch (CryptographicException)
        {
            return null;
        }

        return Encoding.UTF8.GetString(plain);
    }
}
