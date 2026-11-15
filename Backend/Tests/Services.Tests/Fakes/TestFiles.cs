using System.IO.Compression;
using System.Text;
using Common.DTOs.Tickets;

namespace Services.Tests.Fakes;

/// <summary>Small files with real signatures (and fakes without), as uploads.</summary>
public static class TestFiles
{
    public static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj << /Type /Catalog >> endobj\n%%EOF\n");

    public static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52];

    public static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, 0x4A, 0x46, 0x49, 0x46, 0];

    public static readonly byte[] Docx = Zip("[Content_Types].xml", "word/document.xml", "_rels/.rels");

    /// <summary>A ZIP that is not a Word document.</summary>
    public static readonly byte[] PlainZip = Zip("readme.txt");

    /// <summary>A macro-enabled document renamed to .docx.</summary>
    public static readonly byte[] DocxWithMacro = Zip("[Content_Types].xml", "word/document.xml", "word/vbaProject.bin");

    /// <summary>Plain text – e.g. a "PDF" that is not one.</summary>
    public static readonly byte[] Text = Encoding.UTF8.GetBytes("this is not really a pdf");

    public static IncomingFile File(string name, byte[] content) =>
        new(name, content.Length, () => new MemoryStream(content, writable: false));

    public static IncomingFile PdfFile(string name = "מסמך.pdf") => File(name, Pdf);

    private static byte[] Zip(params string[] entries)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in entries)
            {
                using var writer = new StreamWriter(zip.CreateEntry(entry).Open());
                writer.Write("<x/>");
            }
        }

        return buffer.ToArray();
    }
}
