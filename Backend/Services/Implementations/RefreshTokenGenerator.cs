using System.Security.Cryptography;
using System.Text;

namespace Services.Implementations;

/// <summary>
/// Refresh token values (spec 6.1): 256 random bits, stored only as a SHA-256 hash.
/// A plain hash is enough – unlike passwords, the value has full entropy, so it cannot be guessed or
/// brute-forced, and an unsalted hash keeps the lookup a simple unique-index seek.
/// </summary>
public static class RefreshTokenGenerator
{
    private const int ValueBytes = 32;

    public static string NewValue() => Base64UrlEncode(RandomNumberGenerator.GetBytes(ValueBytes));

    /// <returns>Upper-case hex, 64 characters.</returns>
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
