using System.Globalization;
using System.Security.Claims;
using Common.Enums;
using Common.Interfaces;

namespace API.Auth;

/// <summary>Reads the current user from the validated access token claims of the HTTP request.</summary>
public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public int UserId => GetRequiredInt(AuthClaimTypes.UserId);

    public int OrganizationId => GetRequiredInt(AuthClaimTypes.OrganizationId);

    public int DepartmentId => GetRequiredInt(AuthClaimTypes.DepartmentId);

    public IReadOnlyCollection<Role> Roles =>
        !IsAuthenticated
            ? []
            : Principal!.FindAll(AuthClaimTypes.Role)
                .Select(c => Enum.TryParse<Role>(c.Value, out var role) ? role : (Role?)null)
                .OfType<Role>()
                .ToList();

    public bool MustChangePassword => IsMustChangePassword(Principal);

    public bool IsInRole(Role role) => Roles.Contains(role);

    /// <summary>Missing or unreadable claim counts as "must change" – the safe default.</summary>
    public static bool IsMustChangePassword(ClaimsPrincipal? principal)
    {
        var value = principal?.FindFirst(AuthClaimTypes.MustChangePassword)?.Value;
        return !bool.TryParse(value, out var mustChange) || mustChange;
    }

    private int GetRequiredInt(string claimType)
    {
        if (!IsAuthenticated)
        {
            throw new InvalidOperationException("No authenticated user in the current request.");
        }

        var value = Principal!.FindFirst(claimType)?.Value;
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var result)
            ? result
            : throw new InvalidOperationException($"Access token claim '{claimType}' is missing or invalid.");
    }
}
