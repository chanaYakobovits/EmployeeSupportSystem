using System.Text.RegularExpressions;
using Serilog.Context;

namespace API.Middleware;

/// <summary>
/// Reads the X-Correlation-Id request header (or generates a new id), echoes it in the response,
/// and pushes it into the Serilog LogContext so every log line of the request carries it.
/// </summary>
public partial class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";
    private const string LogProperty = "CorrelationId";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context.Request);

        context.Items[ItemKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(LogProperty, correlationId))
        {
            await _next(context);
        }
    }

    public static string? GetCorrelationId(HttpContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) ? value as string : null;

    private static string ResolveCorrelationId(HttpRequest request)
    {
        var incoming = request.Headers[HeaderName].ToString();

        // Accept a client-supplied id only if it is short and safe to echo/log; otherwise generate one.
        return SafeIdRegex().IsMatch(incoming) ? incoming : Guid.NewGuid().ToString("N");
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex SafeIdRegex();
}
