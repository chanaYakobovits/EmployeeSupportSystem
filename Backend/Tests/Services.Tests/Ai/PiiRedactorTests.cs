using Services.Implementations.Ai;
using Services.Interfaces;

namespace Services.Tests.Ai;

public class PiiRedactorTests
{
    private static readonly string[] NoNames = [];
    private static readonly string[] Requester = ["דני", "כהן"];
    private static readonly string[] Organization = ["רונית לוי", "משה ישראלי"];

    private readonly PiiRedactor _redactor = new();

    private PiiRedactionResult Redact(string text, string[]? requester = null, string[]? organization = null) =>
        _redactor.Redact(text, requester ?? NoNames, organization ?? NoNames);

    private static void AssertOnly(PiiRedactionResult result, PiiType type, int expected)
    {
        foreach (var (key, count) in result.Counts)
        {
            Assert.Equal(key == type ? expected : 0, count);
        }
    }

    // ---------- Email ----------

    [Theory]
    [InlineData("כתובתי dani.cohen@company.co.il תודה", "כתובתי [EMAIL] תודה")]
    [InlineData("DANI.COHEN@COMPANY.COM", "[EMAIL]")]
    [InlineData("למייל:x@y.com", "למייל:[EMAIL]")]
    [InlineData("שלחו לx_y+z@mail.example.org בבקשה", "שלחו ל[EMAIL] בבקשה")]
    public void Email_IsReplaced(string input, string expected)
    {
        var result = Redact(input);

        Assert.Equal(expected, result.RedactedText);
        AssertOnly(result, PiiType.Email, 1);
    }

    [Fact]
    public void Email_ContainingOrganizationName_IsReplacedAsEmailOnly()
    {
        var result = Redact("dani.cohen@company.co.il", organization: ["Dani Cohen"]);

        Assert.Equal("[EMAIL]", result.RedactedText);
        AssertOnly(result, PiiType.Email, 1);
    }

    // ---------- Phone ----------

    [Theory]
    [InlineData("050-1234567")]
    [InlineData("0501234567")]
    [InlineData("050-123-4567")]
    [InlineData("050 123 4567")]
    [InlineData("+972-50-1234567")]
    [InlineData("+972501234567")]
    [InlineData("+972 50 123 4567")]
    [InlineData("00972-50-1234567")]
    [InlineData("02-6234567")]
    [InlineData("026234567")]
    [InlineData("03-1234567")]
    [InlineData("04-8123456")]
    [InlineData("077-1234567")]
    [InlineData("+972-2-6234567")]
    [InlineData("(050) 1234567")]
    [InlineData("050- 1234567")]
    [InlineData("050 -1234567")]
    [InlineData("050 1234567")]
    [InlineData("02 623 4567")]
    [InlineData("050 - 123 - 4567")]
    public void Phone_EachFormat_IsReplaced(string phone)
    {
        var result = Redact($"טלפון: {phone} תודה");

        Assert.Equal("טלפון: [PHONE] תודה", result.RedactedText);
        AssertOnly(result, PiiType.Phone, 1);
    }

    [Fact]
    public void Phone_WithLeftToRightMark_IsReplaced()
    {
        var result = Redact("טלפון ‎+972-50-1234567");

        Assert.Equal("טלפון ‎[PHONE]", result.RedactedText);
        AssertOnly(result, PiiType.Phone, 1);
    }

    [Theory]
    [InlineData("חזרו אליי ל-050-1234567", "חזרו אליי ל-[PHONE]")]
    [InlineData("בטלפון 0501234567 בבוקר", "בטלפון [PHONE] בבוקר")]
    [InlineData("התקשרתי ב050-1234567", "התקשרתי ב[PHONE]")]
    public void Phone_WithHebrewPrefix_IsReplaced(string input, string expected)
    {
        var result = Redact(input);

        Assert.Equal(expected, result.RedactedText);
        AssertOnly(result, PiiType.Phone, 1);
    }

    // ---------- ID ----------

    [Theory]
    [InlineData("123456782")] // 9 digits, valid check digit
    [InlineData("12345674")]  // 8 digits (no leading zero), valid when padded to 012345674
    public void IdNumber_WithValidCheckDigit_IsReplaced(string id)
    {
        var result = Redact($"ת\"ז {id}.");

        Assert.Equal("ת\"ז [ID].", result.RedactedText);
        AssertOnly(result, PiiType.IdNumber, 1);
    }

    [Theory]
    [InlineData("123456789")]
    [InlineData("12345678")]
    public void IdNumber_WithInvalidCheckDigit_IsReplacedAsNumber(string id)
    {
        var result = Redact($"ת\"ז {id}.");

        Assert.Equal("ת\"ז [NUMBER].", result.RedactedText);
        AssertOnly(result, PiiType.Number, 1);
    }

    // ---------- Number ----------

    [Theory]
    [InlineData("123456")]
    [InlineData("4580123456789012")]
    [InlineData("4580 1234 5678 9012")]
    [InlineData("4580-1234-5678-9012")]
    public void LongNumber_AndCardNumber_IsReplaced(string number)
    {
        var result = Redact($"מספר {number} בבקשה");

        Assert.Equal("מספר [NUMBER] בבקשה", result.RedactedText);
        AssertOnly(result, PiiType.Number, 1);
    }

    [Fact]
    public void NumbersOnSeparateLines_AreNotJoinedIntoPhone()
    {
        const string text = "מספר תיק 050\n1234567";

        var result = Redact(text);

        Assert.Equal(0, result.Counts[PiiType.Phone]);
        Assert.Equal("מספר תיק 050\n[NUMBER]", result.RedactedText);
    }

    [Fact]
    public void FiveDigitNumber_IsNotReplaced()
    {
        const string text = "הזמנה 12345 בוצעה";

        var result = Redact(text);

        Assert.Equal(text, result.RedactedText);
        Assert.Equal(0, result.TotalCount);
    }

    // ---------- Employee code ----------

    [Theory]
    [InlineData("קוד EMP-12345 שלי", "קוד [EMPLOYEE_CODE] שלי")]
    [InlineData("emp-12345", "[EMPLOYEE_CODE]")]
    [InlineData("EMP 12345", "[EMPLOYEE_CODE]")]
    [InlineData("שייך לEMP-12345", "שייך ל[EMPLOYEE_CODE]")]
    [InlineData("קוד עובד 12345", "קוד עובד [EMPLOYEE_CODE]")]
    [InlineData("קוד העובד: 54321", "קוד העובד: [EMPLOYEE_CODE]")]
    [InlineData("קוד עובד שלי 54321", "קוד עובד שלי [EMPLOYEE_CODE]")]
    public void EmployeeCode_IsReplaced_AndNotAsNumber(string input, string expected)
    {
        var result = Redact(input);

        Assert.Equal(expected, result.RedactedText);
        AssertOnly(result, PiiType.EmployeeCode, 1);
    }

    // ---------- Names ----------

    [Fact]
    public void RequesterFirstAndLastName_AreReplacedSeparately()
    {
        var result = Redact("שמי דני ושם המשפחה כהן", requester: Requester);

        Assert.Equal("שמי [NAME] ושם המשפחה [NAME]", result.RedactedText);
        AssertOnly(result, PiiType.Name, 2);
    }

    [Fact]
    public void RequesterFullName_IsSplitIntoWords()
    {
        var result = Redact("פונה: דני כהן", requester: ["דני כהן"]);

        Assert.Equal("פונה: [NAME] [NAME]", result.RedactedText);
        AssertOnly(result, PiiType.Name, 2);
    }

    [Theory]
    [InlineData("ולדני", "ול[NAME]")]
    [InlineData("שבכהן", "שב[NAME]")]
    [InlineData("לדני", "ל[NAME]")]
    [InlineData("מכהן", "מ[NAME]")]
    [InlineData("כשדני", "כש[NAME]")]
    [InlineData("הדני", "ה[NAME]")]
    [InlineData("ודני", "ו[NAME]")]
    [InlineData("ל-דני", "ל-[NAME]")]
    [InlineData("ל־כהן", "ל־[NAME]")]
    [InlineData("ושלכהן", "ושל[NAME]")]
    public void Name_WithHebrewPrefixLetters_IsReplaced_AndPrefixKept(string word, string expected)
    {
        var result = Redact($"פניתי {word} אתמול", requester: Requester);

        Assert.Equal($"פניתי {expected} אתמול", result.RedactedText);
        AssertOnly(result, PiiType.Name, 1);
    }

    [Fact]
    public void OrganizationName_IsReplaced()
    {
        var result = Redact("דיברתי עם רונית לוי ועם משה", organization: Organization);

        Assert.Equal("דיברתי עם [NAME] [NAME] ועם [NAME]", result.RedactedText);
        AssertOnly(result, PiiType.Name, 3);
    }

    [Fact]
    public void LatinName_IsReplaced_CaseInsensitive()
    {
        var result = Redact("DANI and dani and Dani", requester: ["Dani"]);

        Assert.Equal("[NAME] and [NAME] and [NAME]", result.RedactedText);
        AssertOnly(result, PiiType.Name, 3);
    }

    [Theory]
    [InlineData("רון", "הארון במשרד שבור")]
    [InlineData("דני", "דניאלה ביקשה עזרה")]
    [InlineData("לוי", "הליווי לא הגיע")]
    public void Name_InsideAnotherWord_IsNotReplaced(string name, string text)
    {
        var result = Redact(text, organization: [name]);

        Assert.Equal(text, result.RedactedText);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public void LongerName_IsMatchedBeforeShorterPrefixOfIt()
    {
        var result = Redact("דניאל ודני", organization: ["דני", "דניאל"]);

        Assert.Equal("[NAME] ו[NAME]", result.RedactedText);
        AssertOnly(result, PiiType.Name, 2);
    }

    [Fact]
    public void NameAppearingSeveralTimes_IsReplacedEverywhere()
    {
        var result = Redact("דני ביקש. דני שוב ביקש. נא לחזור לדני.", requester: Requester);

        Assert.Equal("[NAME] ביקש. [NAME] שוב ביקש. נא לחזור ל[NAME].", result.RedactedText);
        AssertOnly(result, PiiType.Name, 3);
    }

    [Fact]
    public void HyphenatedName_IsReplacedWholeAndByParts()
    {
        var result = Redact("משפחת בן-דוד, ובפרט דוד", organization: ["אבי בן-דוד"]);

        Assert.Equal("משפחת [NAME], ובפרט [NAME]", result.RedactedText);
        AssertOnly(result, PiiType.Name, 2);
    }

    [Fact]
    public void NamesWithRegexSpecialCharacters_AreMatchedLiterally()
    {
        var result = Redact("ג'ורג' פנה. axb לא שם.", organization: ["ג'ורג'", "a.b", "(x"]);

        Assert.Equal("[NAME] פנה. axb לא שם.", result.RedactedText);
        AssertOnly(result, PiiType.Name, 1);
    }

    // ---------- Clean text & edge cases ----------

    [Theory]
    [InlineData("שלום, חסרים 1,200 ש\"ח במשכורת של חודש 10/11.")]
    [InlineData("הפגישה ב-10/11/2024 בשעה 14:30, קומה 3, חדר 205.")]
    [InlineData("המחשב לא נדלק מאז הבוקר, ניסיתי להפעיל מחדש 2 פעמים.")]
    [InlineData("סכום של 12,500.50 ש\"ח הועבר ב-1.11.2024")]
    [InlineData("סכום של 1 200 000 ש\"ח, תאריך 10 11 2024")]
    [InlineData("חדרים 02 03 04 בקומה 5 - 123 4567 כיסאות")]
    [InlineData("שעות 08 - 12 ו-13 - 17, טופס 0 500 1234")]
    public void PlainText_IsReturnedExactlyAsIs(string text)
    {
        var result = Redact(text, Requester, Organization);

        Assert.Equal(text, result.RedactedText);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public void EmptyText_ReturnsEmpty_WithZeroCounts()
    {
        var result = Redact(string.Empty, Requester, Organization);

        Assert.Equal(string.Empty, result.RedactedText);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(Enum.GetValues<PiiType>().Length, result.Counts.Count);
    }

    [Fact]
    public void NullText_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => _redactor.Redact(null!, NoNames, NoNames));
    }

    [Fact]
    public void EmptyNameLists_PatternRulesStillApply()
    {
        var result = Redact("דני, 050-1234567, a@b.com, 123456782");

        Assert.Equal("דני, [PHONE], [EMAIL], [ID]", result.RedactedText);
        Assert.Equal(0, result.Counts[PiiType.Name]);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public void BlankAndSingleLetterNames_AreIgnored()
    {
        const string text = "א ב ג ו ה";

        var result = Redact(text, requester: ["", "  ", "א"], organization: ["ו"]);

        Assert.Equal(text, result.RedactedText);
        Assert.Equal(0, result.TotalCount);
    }

    // ---------- Realistic ticket ----------

    [Fact]
    public void RealisticTicket_AllTypesReplaced_RestUntouched()
    {
        const string text =
            "שלום, אני דני כהן (EMP-12345), ת\"ז 123456782. " +
            "במשכורת של 10/11 חסרים 1,200 ש\"ח. " +
            "דיברתי כבר עם רונית לוי ולא קיבלתי תשובה, גם לדני מהשכר אין מענה. " +
            "אפשר לחזור אליי ל-050-1234567 או למייל dani.cohen@company.co.il. " +
            "מספר החשבון שלי 12-345-678901.";

        const string expected =
            "שלום, אני [NAME] [NAME] ([EMPLOYEE_CODE]), ת\"ז [ID]. " +
            "במשכורת של 10/11 חסרים 1,200 ש\"ח. " +
            "דיברתי כבר עם [NAME] [NAME] ולא קיבלתי תשובה, גם ל[NAME] מהשכר אין מענה. " +
            "אפשר לחזור אליי ל-[PHONE] או למייל [EMAIL]. " +
            "מספר החשבון שלי 12-345-[NUMBER].";

        var result = Redact(text, Requester, Organization);

        Assert.Equal(expected, result.RedactedText);
        Assert.Equal(5, result.Counts[PiiType.Name]);
        Assert.Equal(1, result.Counts[PiiType.EmployeeCode]);
        Assert.Equal(1, result.Counts[PiiType.IdNumber]);
        Assert.Equal(1, result.Counts[PiiType.Phone]);
        Assert.Equal(1, result.Counts[PiiType.Email]);
        Assert.Equal(1, result.Counts[PiiType.Number]);
    }

    // ---------- Logging safety (spec 6.4) ----------

    [Fact]
    public void ResultToString_ContainsCountsOnly_NoText()
    {
        var result = Redact("סיסמה סודית של דני: 050-1234567", requester: Requester);

        var printed = result.ToString();

        Assert.DoesNotContain("סודית", printed);
        Assert.DoesNotContain("[NAME]", printed);
        Assert.DoesNotContain("דני", printed);
        Assert.Contains("Name=1", printed);
        Assert.Contains("Phone=1", printed);
    }
}
