using System.ComponentModel.DataAnnotations;

namespace Common.DTOs.Auth;

/// <summary>
/// Login with one identifier field (spec 4.1): contains '@' → e-mail, otherwise → employee code.
/// </summary>
public sealed record LoginRequest(
    [Required(AllowEmptyStrings = false), StringLength(256)] string Identifier,
    [Required(AllowEmptyStrings = false), StringLength(128)] string Password);
