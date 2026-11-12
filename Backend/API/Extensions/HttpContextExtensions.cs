namespace API.Extensions;

public static class HttpContextExtensions
{
    /// <summary>Client address for the audit log. No reverse proxy yet; behind one, ForwardedHeaders must be configured first.</summary>
    public static string ClientIp(this HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
}
