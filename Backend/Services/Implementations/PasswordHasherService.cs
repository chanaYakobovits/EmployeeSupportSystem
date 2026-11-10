using Common.Interfaces;
using Common.Models;
using Microsoft.AspNetCore.Identity;

namespace Services.Implementations;

/// <summary>
/// Spec 6.1: passwords are stored only as hashes, using ASP.NET Core Identity's PasswordHasher (PBKDF2 with salt).
/// </summary>
public sealed class PasswordHasherService : IPasswordHasher
{
    // The hasher does not use the user instance; a shared placeholder satisfies the API.
    private static readonly User HashSubject = new();

    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(HashSubject, password);

    public bool Verify(string hashedPassword, string providedPassword) =>
        _hasher.VerifyHashedPassword(HashSubject, hashedPassword, providedPassword) != PasswordVerificationResult.Failed;
}
