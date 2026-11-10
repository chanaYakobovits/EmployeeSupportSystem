namespace Services.Interfaces;

/// <summary>
/// Replaces identifying details with tags before any text is sent to the AI (spec 1.4, 5.3).
/// Pure: no database access, no logging, no state. Names are supplied by the caller.
/// </summary>
public interface IPiiRedactor
{
    /// <param name="text">The ticket text. Must not be null.</param>
    /// <param name="requesterNames">The requester's names (first / last, or full names; split on whitespace).</param>
    /// <param name="organizationNames">Names of other employees in the organization.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="System.Text.RegularExpressions.RegexMatchTimeoutException">
    /// Redaction did not complete in time. Callers must treat this as failure and send nothing.
    /// </exception>
    PiiRedactionResult Redact(
        string text,
        IReadOnlyCollection<string> requesterNames,
        IReadOnlyCollection<string> organizationNames);
}
