using System.Security.Cryptography;

namespace StayHost.Domain;

/// <summary>PBKDF2-SHA256 with a per-user salt. Shared so the seeder and the web app agree.</summary>
public static class PasswordHasher
{
    private const int Iterations = 210_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;

    public static (string Hash, string Salt) Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public static bool Verify(string password, string hash, string salt)
    {
        var computed = Rfc2898DeriveBytes.Pbkdf2(
            password, Convert.FromBase64String(salt), Iterations, HashAlgorithmName.SHA256, HashBytes);
        return CryptographicOperations.FixedTimeEquals(computed, Convert.FromBase64String(hash));
    }
}
