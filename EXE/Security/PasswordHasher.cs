using System.Security.Cryptography;

namespace EXE.Security;

public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;
    private const string Prefix = "PBKDF2$";

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);

        return $"{Prefix}{Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public static bool Verify(string password, string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return false;

        // Backward compatibility: some rows may still store plain text.
        if (!stored.StartsWith(Prefix, StringComparison.Ordinal))
            return stored == password;

        var parts = stored.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4)
            return false;

        if (!int.TryParse(parts[1], out var iterations))
            return false;

        byte[] salt;
        byte[] storedKey;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            storedKey = Convert.FromBase64String(parts[3]);
        }
        catch
        {
            return false;
        }

        var key = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            storedKey.Length);

        return CryptographicOperations.FixedTimeEquals(key, storedKey);
    }
}

