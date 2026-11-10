using Microsoft.AspNetCore.Authorization;

namespace API.Auth;

/// <summary>
/// Spec 4.1: a user who must change the password (first login) cannot use the system until it is changed.
/// Self-handling requirement; part of the default, fallback and role policies.
/// </summary>
public sealed class PasswordChangedRequirement : AuthorizationHandler<PasswordChangedRequirement>, IAuthorizationRequirement
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PasswordChangedRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true && !HttpCurrentUser.IsMustChangePassword(context.User))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
