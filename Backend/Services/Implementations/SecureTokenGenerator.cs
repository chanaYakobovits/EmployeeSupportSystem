using System.Security.Cryptography;
using System.Text;

namespace Services.Implementations;

/// <summary>
/// Opaque secret tokens: refresh tokens (spec 6.1) and invitation links. 256 random bits, URL-safe, stored only as a
/// SHA-256 hash. A plain hash is enough – unlike passwords, the value has full entropy, so it cannot be guessed or
/// brute-forced, and an unsalted hash keeps the lookup a simple unique-index seek.
/// </summary>
public static class SecureTokenGenerator
{
    private const int ValueBytes = 32;

    /// <returns>Base64Url without padding, 43 characters.</returns>
    public static string NewValue() => Base64UrlEncode(RandomNumberGenerator.GetBytes(ValueBytes));

    /// <returns>Upper-case hex, 64 characters.</returns>
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
