using System.Security.Cryptography;

namespace Services.Implementations;

/// <summary>
/// Temporary passwords for new users and admin resets (spec 4.2). Cryptographically random, 12 characters,
/// always at least one letter and one digit (so <see cref="PasswordPolicy"/> accepts it), without look-alike
/// characters (0/O, 1/l/I) because the admin reads it out or copies it by hand.
/// The value is returned to the admin once and never stored, logged or audited – only its hash is kept.
/// </summary>
public static class TemporaryPasswordGenerator
{
    public const int Length = 12;

    private const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string All = Letters + Digits;

    public static string Generate()
    {
        var chars = new char[Length];
        chars[0] = Letters[RandomNumberGenerator.GetInt32(Letters.Length)];
        chars[1] = Digits[RandomNumberGenerator.GetInt32(Digits.Length)];
        RandomNumberGenerator.GetItems(All.AsSpan(), chars.AsSpan(2));

        // The guaranteed letter and digit must not always sit at the start.
        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);
    }
}
