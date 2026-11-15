using Common.Constants;
using Common.DTOs.Tickets;
using Services.Tests.Fakes;
using Services.Validators;

namespace Services.Tests.Files;

/// <summary>Spec 6.3, 7.4: up to 3 files of up to 5MB, pdf/jpg/png/docx – by extension and by content.</summary>
public sealed class AttachmentFilesValidatorTests
{
    private readonly AttachmentFilesValidator _validator = new();

    [Theory]
    [MemberData(nameof(ValidFiles))]
    public async Task A_real_file_of_each_allowed_type_passes(string name, byte[] content)
    {
        var result = await _validator.ValidateAsync([TestFiles.File(name, content)]);

        Assert.True(result.IsValid, string.Join(", ", result.Errors));
    }

    public static TheoryData<string, byte[]> ValidFiles() => new()
    {
        { "מסמך.pdf", TestFiles.Pdf },
        { "צילום.PNG", TestFiles.Png },
        { "תמונה.jpg", TestFiles.Jpeg },
        { "תמונה.jpeg", TestFiles.Jpeg },
        { "טופס.docx", TestFiles.Docx },
    };

    [Theory]
    [MemberData(nameof(DisguisedFiles))]
    public async Task A_file_whose_content_is_not_its_extension_is_rejected(string name, byte[] content)
    {
        var result = await _validator.ValidateAsync([TestFiles.File(name, content)]);

        var error = Assert.Single(result.Errors);
        Assert.Equal("files[0]", error.PropertyName);
        Assert.Equal(AttachmentFilesValidator.ContentMismatchMessage, error.ErrorMessage);
    }

    public static TheoryData<string, byte[]> DisguisedFiles() => new()
    {
        { "fake.pdf", TestFiles.Text },
        { "fake.pdf", TestFiles.Png },
        { "fake.png", TestFiles.Pdf },
        { "fake.jpg", TestFiles.Png },
        { "archive.docx", TestFiles.PlainZip },
        { "macro.docx", TestFiles.DocxWithMacro },
        { "fake.docx", TestFiles.Pdf },
    };

    [Theory]
    [InlineData("script.exe")]
    [InlineData("page.html")]
    [InlineData("image.svg")]
    [InlineData("noextension")]
    [InlineData("document.pdf.exe")]
    public async Task A_type_that_is_not_allowed_is_rejected_even_with_allowed_content(string name)
    {
        var result = await _validator.ValidateAsync([TestFiles.File(name, TestFiles.Pdf)]);

        Assert.Equal([AttachmentFilesValidator.TypeNotAllowedMessage], result.Errors.Select(e => e.ErrorMessage));
    }

    [Fact]
    public async Task A_file_over_5MB_is_rejected()
    {
        var content = new byte[AttachmentLimits.MaxFileBytes + 1];
        TestFiles.Pdf.CopyTo(content, 0);

        var result = await _validator.ValidateAsync([TestFiles.File("big.pdf", content)]);

        Assert.Equal([AttachmentFilesValidator.TooLargeMessage], result.Errors.Select(e => e.ErrorMessage));
    }

    [Fact]
    public async Task A_file_of_exactly_5MB_passes()
    {
        var content = new byte[AttachmentLimits.MaxFileBytes];
        TestFiles.Pdf.CopyTo(content, 0);

        Assert.True((await _validator.ValidateAsync([TestFiles.File("big.pdf", content)])).IsValid);
    }

    [Fact]
    public async Task Four_files_are_rejected_without_reading_them()
    {
        var reads = 0;
        var files = Enumerable.Range(1, 4)
            .Select(i => new IncomingFile($"{i}.pdf", TestFiles.Pdf.Length, () =>
            {
                reads++;
                return new MemoryStream(TestFiles.Pdf);
            }))
            .ToList();

        var result = await _validator.ValidateAsync(files);

        var error = Assert.Single(result.Errors);
        Assert.Equal("files", error.PropertyName);
        Assert.Equal(AttachmentFilesValidator.TooManyMessage, error.ErrorMessage);
        Assert.Equal(0, reads);
    }

    [Fact]
    public async Task Three_files_and_no_files_pass()
    {
        Assert.True((await _validator.ValidateAsync([])).IsValid);
        Assert.True((await _validator.ValidateAsync(
            [TestFiles.PdfFile("1.pdf"), TestFiles.PdfFile("2.pdf"), TestFiles.File("3.png", TestFiles.Png)])).IsValid);
    }

    [Fact]
    public async Task An_empty_file_is_rejected()
    {
        var result = await _validator.ValidateAsync([TestFiles.File("empty.pdf", [])]);

        Assert.Equal([AttachmentFilesValidator.EmptyMessage], result.Errors.Select(e => e.ErrorMessage));
    }

    [Fact]
    public async Task Each_file_has_its_own_error_key()
    {
        var result = await _validator.ValidateAsync(
            [TestFiles.PdfFile(), TestFiles.File("x.exe", TestFiles.Pdf), TestFiles.File("y.pdf", TestFiles.Text)]);

        Assert.Equal(["files[1]", "files[2]"], result.Errors.Select(e => e.PropertyName));
    }
}
