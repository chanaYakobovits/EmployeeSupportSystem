using System.Net;
using System.Text;

namespace Services.Tests.Ai.Fakes;

/// <summary>Stands in for OpenAI: no real network call. Records every request body.</summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<int, CancellationToken, Task<HttpResponseMessage>> _respond;
    private readonly List<string> _requestBodies = [];

    /// <param name="respond">Receives the 1-based call number.</param>
    public FakeHttpMessageHandler(Func<int, CancellationToken, Task<HttpResponseMessage>> respond) => _respond = respond;

    public int CallCount => _requestBodies.Count;
    public IReadOnlyList<string> RequestBodies => _requestBodies;
    public List<Uri?> RequestUris { get; } = [];

    /// <summary>Returns the given responses in order; the last one repeats.</summary>
    public static FakeHttpMessageHandler Sequence(params Func<HttpResponseMessage>[] responses) =>
        new((call, _) => Task.FromResult(responses[Math.Min(call, responses.Length) - 1]()));

    /// <summary>Never answers – waits until the request is cancelled.</summary>
    public static FakeHttpMessageHandler Hanging() =>
        new(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("unreachable");
        });

    public static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Error(HttpStatusCode status) =>
        Json(status, """{"error":{"message":"error","type":"server_error","code":null}}""");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requestBodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
        RequestUris.Add(request.RequestUri);
        return await _respond(_requestBodies.Count, cancellationToken);
    }
}
