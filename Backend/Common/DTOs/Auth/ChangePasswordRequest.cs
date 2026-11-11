using System.ComponentModel.DataAnnotations;

namespace Common.DTOs.Auth;

/// <summary>Spec 7.2. The password policy itself (spec 6.1) is checked by the service.</summary>
public sealed record ChangePasswordRequest(
    [Required(AllowEmptyStrings = false), StringLength(128)] string CurrentPassword,
    [Required(AllowEmptyStrings = false), StringLength(256)] string NewPassword);
