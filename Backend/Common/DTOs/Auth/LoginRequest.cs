namespace Common.DTOs.Auth;

/// <summary>
/// Login with one identifier field (spec 4.1): contains '@' → e-mail, otherwise → employee code.
/// Validated by LoginRequestValidator (Services).
/// </summary>
public sealed record LoginRequest(string Identifier, string Password);
