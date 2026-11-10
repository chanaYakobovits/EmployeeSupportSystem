namespace Services.Interfaces;

/// <summary>
/// Output of <see cref="IPiiRedactor.Redact"/>: the text that may leave the system, plus per-type counts.
/// </summary>
/// <remarks>
/// Only <see cref="Counts"/> may be logged (spec 6.4). <see cref="ToString"/> is overridden so that
/// passing this object to a logger never writes ticket text.
/// </remarks>
public sealed record PiiRedactionResult(string RedactedText, IReadOnlyDictionary<PiiType, int> Counts)
{
    public int TotalCount => Counts.Values.Sum();

    public override string ToString() =>
        $"{nameof(PiiRedactionResult)} {{ {string.Join(", ", Counts.Select(c => $"{c.Key}={c.Value}"))} }}";
}
