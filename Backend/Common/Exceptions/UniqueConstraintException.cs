namespace Common.Exceptions;

/// <summary>
/// An insert/update violated a unique index (→ 409 if not translated). Thrown by the data layer when two requests
/// race past the service's pre-check; the service maps <see cref="IndexName"/> to a field-specific message.
/// </summary>
public sealed class UniqueConstraintException : ConflictException
{
    public UniqueConstraintException(string? indexName)
        : base(errorType: "duplicate")
    {
        IndexName = indexName;
    }

    /// <summary>Name of the violated index (e.g. "IX_Users_Email"), when the database reported it.</summary>
    public string? IndexName { get; }
}
