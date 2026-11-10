using Microsoft.Extensions.Logging;

namespace Services.Tests.Fakes;

/// <summary>Collects formatted log messages so tests can check what is (and is not) logged.</summary>
public sealed class CapturingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public string AllText => string.Join("\n", Entries.Select(e => e.Message));

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        if (exception is not null)
        {
            message += "\n" + exception;
        }

        Entries.Add((logLevel, message));
    }
}
