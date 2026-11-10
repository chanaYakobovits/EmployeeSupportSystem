using API.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;

namespace API.Auth;

/// <summary>
/// A 403 caused by a pending password change gets its own ProblemDetails type, so the client knows to
/// redirect to the change-password screen. Every other 401/403 keeps the default behavior
/// (empty response, turned into ProblemDetails by UseStatusCodePages).
/// </summary>
public sealed class ProblemDetailsAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    public const string PasswordChangeRequiredTitle = "יש להחליף סיסמה לפני המשך העבודה";
    public const string PasswordChangeRequiredType = "password-change-required";

    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        var passwordChangePending = authorizeResult.Forbidden
            && authorizeResult.AuthorizationFailure?.FailedRequirements.OfType<PasswordChangedRequirement>().Any() == true;

        if (!passwordChangePending)
        {
            await _default.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Type = ProblemTitles.TypeBaseUri + PasswordChangeRequiredType,
            Title = PasswordChangeRequiredTitle,
        };
        ProblemDetailsEnricher.AddTraceIds(problem, context);

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null, "application/problem+json");
    }
}
