using System.Globalization;
using System.Text;
using Common.Constants;

namespace Services.Implementations.Files;

/// <summary>
/// Cleans the original name of an uploaded file before it is stored (only in the database) and later sent back in
/// Content-Disposition. The result has no path, no control or formatting characters and a bounded length.
/// The allowed type is decided from the extension of the <b>sanitized</b> name, so a hidden direction mark cannot make
/// "invoice‮fdp.exe" look like a PDF.
/// </summary>
public static class FileNameSanitizer
{
    /// <summary>Base name used when nothing is left of the original one (e.g. ".pdf").</summary>
    public const string FallbackBaseName = "file";

    /// <summary>An extension longer than this is treated as part of the name (no real allowed extension is).</summary>
    private const int MaxExtensionLength = 10;

    /// <summary>Characters Windows does not allow in file names; '/' and '\' are handled as path separators first.</summary>
    private static readonly HashSet<char> Forbidden = ['<', '>', ':', '"', '|', '?', '*'];

    public static string Sanitize(string? fileName)
    {
        var name = fileName ?? string.Empty;

        // Only the last path segment: "../x.pdf" and "..\..\x.pdf" become "x.pdf".
        name = name[(name.LastIndexOfAny(['/', '\\']) + 1)..];
        name = name.Normalize(NormalizationForm.FormC);

        var cleaned = new StringBuilder(name.Length);
        foreach (var rune in name.EnumerateRunes())
        {
            switch (Rune.GetUnicodeCategory(rune))
            {
                // Control (Cc), format (Cf: U+202E RIGHT-TO-LEFT OVERRIDE, U+200E/F marks, U+2066–2069 isolates,
                // zero-width characters), surrogates, private use and unassigned code points are dropped.
                case UnicodeCategory.Control:
                case UnicodeCategory.Format:
                case UnicodeCategory.Surrogate:
                case UnicodeCategory.PrivateUse:
                case UnicodeCategory.OtherNotAssigned:
                    continue;
                // Any other kind of space or line break becomes a plain space.
                case UnicodeCategory.SpaceSeparator:
                case UnicodeCategory.LineSeparator:
                case UnicodeCategory.ParagraphSeparator:
                    cleaned.Append(' ');
                    continue;
            }

            if (rune.IsBmp && Forbidden.Contains((char)rune.Value))
            {
                continue;
            }

            cleaned.Append(rune.ToString());
        }

        var result = CollapseSpaces(cleaned.ToString()).TrimEnd(' ', '.');

        var dot = result.LastIndexOf('.');
        var extension = dot >= 0 && result.Length - dot <= MaxExtensionLength + 1 ? result[dot..] : string.Empty;
        var baseName = (extension.Length > 0 ? result[..dot] : result).Trim(' ', '.');
        if (baseName.Length == 0)
        {
            baseName = FallbackBaseName;
        }

        var maxBase = AttachmentLimits.MaxFileNameLength - extension.Length;
        return TruncateTextElements(baseName, maxBase).TrimEnd(' ', '.') + extension;
    }

    /// <summary>The extension of a sanitized name in lower case, with the dot ("" when there is none).</summary>
    public static string ExtensionOf(string sanitizedName)
    {
        var dot = sanitizedName.LastIndexOf('.');
        return dot < 0 ? string.Empty : sanitizedName[dot..].ToLowerInvariant();
    }

    private static string CollapseSpaces(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (c == ' ' && builder.Length > 0 && builder[^1] == ' ')
            {
                continue;
            }

            builder.Append(c);
        }

        return builder.ToString().Trim();
    }

    /// <summary>Cuts on whole text elements, so a surrogate pair or a letter with its combining marks is never split.</summary>
    private static string TruncateTextElements(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        var builder = new StringBuilder(maxLength);
        var elements = StringInfo.GetTextElementEnumerator(value);
        while (elements.MoveNext())
        {
            var element = elements.GetTextElement();
            if (builder.Length + element.Length > maxLength)
            {
                break;
            }

            builder.Append(element);
        }

        return builder.ToString();
    }
}
