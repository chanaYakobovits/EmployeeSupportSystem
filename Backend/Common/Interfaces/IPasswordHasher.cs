namespace Common.Interfaces;

/// <summary>
/// Hashes and verifies passwords (spec 6.1). Defined here so any layer can depend on it;
/// the implementation (ASP.NET Core Identity PasswordHasher, PBKDF2 + salt) lives in Services.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string hashedPassword, string providedPassword);
}
