using Common.Constants;
using Services.Implementations.Files;
using Services.Tests.Fakes;
using Services.Validators;

namespace Services.Tests.Files;

/// <summary>Spec 6.3: the original name is stored and sent back only after it is cleaned.</summary>
public sealed class FileNameSanitizerTests
{
    [Theory]
    [InlineData("../x.pdf", "x.pdf")]
    [InlineData("..\\..\\windows\\x.pdf", "x.pdf")]
    [InlineData("/etc/passwd/x.pdf", "x.pdf")]
    [InlineData("C:\\Users\\noa\\תלוש אוקטובר.pdf", "תלוש אוקטובר.pdf")]
    public void Only_the_last_path_segment_is_kept(string original, string expected) =>
        Assert.Equal(expected, FileNameSanitizer.Sanitize(original));

    [Fact]
    public void A_right_to_left_override_cannot_disguise_the_real_extension()
    {
        // Displayed as "invoiceexe.pdf", really "invoice" + U+202E + "fdp.exe".
        var name = FileNameSanitizer.Sanitize("invoice\u202Efdp.exe");

        Assert.Equal("invoicefdp.exe", name);
        Assert.Equal(".exe", FileNameSanitizer.ExtensionOf(name));
    }

    [Fact]
    public void Such_a_file_is_rejected_even_with_pdf_content()
    {
        var result = new AttachmentFilesValidator().Validate([TestFiles.File("invoice\u202Efdp.exe", TestFiles.Pdf)]);

        Assert.Equal([AttachmentFilesValidator.TypeNotAllowedMessage], result.Errors.Select(e => e.ErrorMessage));
    }

    [Fact]
    public void Control_and_formatting_characters_are_removed()
    {
        // NUL, BEL, CR, LF, TAB (Cc) and RLM, LRI, ZERO WIDTH SPACE (Cf).
        var name = FileNameSanitizer.Sanitize("a\u0000b\u0007\u200Fc\u2066d\u200Be\r\nf\t.pdf");

        Assert.Equal("abcdef.pdf", name);
    }

    [Fact]
    public void Characters_not_allowed_in_file_names_are_removed()
    {
        Assert.Equal("ab cd.pdf", FileNameSanitizer.Sanitize("a<b> c:d|?*\".pdf"));
    }

    [Fact]
    public void A_very_long_name_is_cut_keeping_the_extension()
    {
        var name = FileNameSanitizer.Sanitize(new string('א', 5000) + ".docx");

        Assert.Equal(AttachmentLimits.MaxFileNameLength, name.Length);
        Assert.EndsWith("א.docx", name);
    }

    [Fact]
    public void Cutting_never_splits_a_character_made_of_two_code_units()
    {
        var name = FileNameSanitizer.Sanitize(string.Concat(Enumerable.Repeat("😀", 200)) + ".png");

        Assert.True(name.Length <= AttachmentLimits.MaxFileNameLength);
        Assert.EndsWith(".png", name);
        // The last character of the base name closes a pair \u2013 no half emoji.
        Assert.True(char.IsLowSurrogate(name[^5]));
    }

    [Theory]
    [InlineData(".pdf", "file.pdf")]
    [InlineData("  ..pdf", "file.pdf")]
    [InlineData("", "file")]
    [InlineData(null, "file")]
    [InlineData("....", "file")]
    public void An_empty_name_gets_a_neutral_base_name(string? original, string expected) =>
        Assert.Equal(expected, FileNameSanitizer.Sanitize(original));

    [Fact]
    public void Trailing_dots_and_spaces_do_not_hide_the_extension()
    {
        var name = FileNameSanitizer.Sanitize("report.exe. . ");

        Assert.Equal(".exe", FileNameSanitizer.ExtensionOf(name));
    }

    [Fact]
    public void An_ordinary_hebrew_name_is_kept()
    {
        Assert.Equal("אישור רפואי - מרץ 2026.PDF", FileNameSanitizer.Sanitize("אישור רפואי - מרץ   2026.PDF"));
        Assert.Equal(".pdf", FileNameSanitizer.ExtensionOf("אישור.PDF"));
    }
}
