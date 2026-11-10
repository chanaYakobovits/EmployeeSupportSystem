using System.Text;
using System.Text.RegularExpressions;
using Services.Interfaces;

namespace Services.Implementations.Ai;

/// <summary>
/// Spec 5.3: replaces identifying details with tags. Guiding rule: when in doubt, redact —
/// over-redaction is preferable to a leak.
/// </summary>
/// <remarks>
/// Replacement order matters:
/// <list type="number">
/// <item>Email – before names, so "dani.cohen@..." becomes one [EMAIL], not names.</item>
/// <item>Employee code – before numbers, so its digits do not become [NUMBER].</item>
/// <item>Phone – before ID: a 9-digit landline (026234567) can pass the ID checksum, and "+972 501234567"
/// would otherwise leave "+972" behind. An ID that looks like a landline (0[23489] + 7 digits) is tagged
/// [PHONE] instead of [ID]; it is redacted either way.</item>
/// <item>ID – 8/9 digits with a valid check digit, before the generic number rule.</item>
/// <item>Number – any remaining run of 6+ digits, and 4x4 card numbers.</item>
/// <item>Names – last; tags contain no letters that could match a name.</item>
/// </list>
/// Names that are also common Hebrew words (אור, טל, חן...) are redacted wherever they appear.
/// This is intentional: there is no exclusion list.
/// This class never logs: it receives raw ticket text (spec 6.4).
/// </remarks>
public sealed class PiiRedactor : IPiiRedactor
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    private const RegexOptions FixedOptions =
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;

    private static readonly Regex EmailRegex = new(
        @"[A-Z0-9._%+\-]+@[A-Z0-9\-]+(?:\.[A-Z0-9\-]+)*\.[A-Z]{2,}",
        FixedOptions, MatchTimeout);

    // "EMP-12345" (also "EMP 12345", "EMP12345", Hebrew prefix like "לEMP-12345"),
    // or 5 digits right after "קוד עובד" / "קוד העובד" / "קוד עובד שלי".
    private static readonly Regex EmployeeCodeRegex = new(
        @"(?<![A-Z0-9])EMP[-\s]?\d{5}(?!\d)" +
        @"|(?<=קוד\s+ה?עובד(?:\s+שלי)?\s*[:\-–]?\s*)\d{5}(?!\d)",
        FixedOptions, MatchTimeout);

    // Separator between phone digit groups: "-" or "." with an optional space on either side, or a single space.
    // Horizontal whitespace only — a line break never joins two numbers into one phone.
    private const string PhoneSeparator = @"(?:[ \t]?[-.][ \t]?|[ \t])?";

    // Israeli mobile / VoIP (05X, 07X) and landline (02, 03, 04, 08, 09), local or international prefix,
    // with optional separators and parentheses: 050-1234567, 050- 1234567, 050 123 4567, +972-50-1234567...
    private static readonly Regex PhoneRegex = new(
        @"(?<![\d+])(?:(?:\+|00)?972" + PhoneSeparator + @"(?:\(0\))?|\(?0)(?:5\d|7\d|[23489])\)?" +
        PhoneSeparator + @"\d{3}" + PhoneSeparator + @"\d{4}(?!\d)",
        FixedOptions, MatchTimeout);

    private static readonly Regex IdCandidateRegex = new(
        @"(?<!\d)\d{8,9}(?!\d)",
        FixedOptions, MatchTimeout);

    private static readonly Regex CardNumberRegex = new(
        @"(?<!\d)\d{4}(?:[-\s]\d{4}){3}(?!\d)",
        FixedOptions, MatchTimeout);

    private static readonly Regex LongNumberRegex = new(
        @"(?<!\d)\d{6,}(?!\d)",
        FixedOptions, MatchTimeout);

    // Hebrew prefix letters (ו ה ב כ ל מ ש), up to three, optionally followed by a hyphen or maqaf.
    private const string HebrewPrefix = "[ובכלמשה]{0,3}[-־]?";

    // Characters that make up a word; a name must not be glued to any of them.
    private const string WordChar = @"[\p{L}\p{M}\p{N}]";

    private const int MinNameLength = 2;

    public PiiRedactionResult Redact(
        string text,
        IReadOnlyCollection<string> requesterNames,
        IReadOnlyCollection<string> organizationNames)
    {
        ArgumentNullException.ThrowIfNull(text);

        var counts = Enum.GetValues<PiiType>().ToDictionary(t => t, _ => 0);

        if (text.Length == 0)
        {
            return new PiiRedactionResult(text, counts);
        }

        var result = text;
        result = Replace(result, EmailRegex, "[EMAIL]", PiiType.Email, counts);
        result = Replace(result, EmployeeCodeRegex, "[EMPLOYEE_CODE]", PiiType.EmployeeCode, counts);
        result = Replace(result, PhoneRegex, "[PHONE]", PiiType.Phone, counts);
        result = IdCandidateRegex.Replace(result, m =>
        {
            if (!IsValidIsraeliId(m.Value))
            {
                return m.Value; // left for the [NUMBER] rule
            }

            counts[PiiType.IdNumber]++;
            return "[ID]";
        });
        result = Replace(result, CardNumberRegex, "[NUMBER]", PiiType.Number, counts);
        result = Replace(result, LongNumberRegex, "[NUMBER]", PiiType.Number, counts);

        var nameRegex = BuildNameRegex(requesterNames, organizationNames);
        if (nameRegex is not null)
        {
            result = nameRegex.Replace(result, m =>
            {
                counts[PiiType.Name]++;
                return m.Groups["prefix"].Value + "[NAME]";
            });
        }

        return new PiiRedactionResult(result, counts);
    }

    private static string Replace(string input, Regex regex, string tag, PiiType type, Dictionary<PiiType, int> counts) =>
        regex.Replace(input, _ =>
        {
            counts[type]++;
            return tag;
        });

    /// <summary>
    /// Israeli ID check digit: weights 1,2,1,2...; two-digit products are reduced to their digit sum;
    /// the total must be divisible by 10. 8-digit inputs are left-padded with 0.
    /// </summary>
    private static bool IsValidIsraeliId(string digits)
    {
        var padded = digits.PadLeft(9, '0');
        var sum = 0;
        for (var i = 0; i < padded.Length; i++)
        {
            var digit = (int)char.GetNumericValue(padded[i]);
            if (digit < 0)
            {
                return false;
            }

            var product = digit * (i % 2 == 0 ? 1 : 2);
            sum += product > 9 ? product - 9 : product;
        }

        return sum % 10 == 0;
    }

    /// <summary>
    /// One alternation of all name tokens, longest first, each optionally preceded by Hebrew prefix letters.
    /// Built per call: the name lists are call parameters. Returns null when there are no usable names.
    /// </summary>
    private static Regex? BuildNameRegex(params IReadOnlyCollection<string>?[] nameLists)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var list in nameLists)
        {
            if (list is null)
            {
                continue;
            }

            foreach (var name in list)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                foreach (var word in name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                {
                    AddToken(tokens, word);

                    // Hyphenated names ("בן-דוד"): also redact each part on its own.
                    foreach (var part in word.Split(['-', '־'], StringSplitOptions.RemoveEmptyEntries))
                    {
                        AddToken(tokens, part);
                    }
                }
            }
        }

        if (tokens.Count == 0)
        {
            return null;
        }

        var alternation = string.Join("|", tokens
            .OrderByDescending(t => t.Length)
            .ThenBy(t => t, StringComparer.Ordinal)
            .Select(Regex.Escape));

        var pattern = new StringBuilder()
            .Append($"(?<!{WordChar})")
            .Append($"(?<prefix>{HebrewPrefix})")
            .Append($"(?:{alternation})")
            .Append($"(?!{WordChar})")
            .ToString();

        return new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, MatchTimeout);
    }

    private static void AddToken(HashSet<string> tokens, string token)
    {
        var trimmed = token.Trim();
        if (trimmed.Length >= MinNameLength)
        {
            tokens.Add(trimmed);
        }
    }
}
