using System.Text;
using Common.Constants;
using Common.Interfaces;
using Common.Models;

namespace Repositories.Data.Seed;

/// <summary>
/// Demo attachments (task 3.2): small valid PNG and PDF files written through <see cref="IFileStorage"/> and attached to
/// about one ticket in ten – some to the new-ticket form, some to an employee reply, some to an internal note (to show
/// that the requester never gets those). Runs after <see cref="TicketSeedGenerator"/> and does not use its Random, so the
/// generated tickets stay exactly as before.
/// </summary>
internal static class SampleAttachments
{
    private const string PngContentType = "image/png";
    private const string PdfContentType = "application/pdf";

    // A valid 1×1 PNG.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static readonly byte[] Pdf = BuildPdf("Ovdenet sample attachment");

    private static readonly string[] ImageNames = ["צילום מסך.png", "תמונה של ההודעה.png", "שגיאה במסך.png"];
    private static readonly string[] DocumentNames = ["תלוש אוקטובר.pdf", "אישור רפואי.pdf", "טופס בקשה.pdf"];

    /// <returns>Number of attachments added.</returns>
    public static async Task<int> AddAsync(IReadOnlyList<Ticket> tickets, IFileStorage storage,
        CancellationToken cancellationToken)
    {
        var count = 0;
        for (var i = 0; i < tickets.Count; i++)
        {
            var ticket = tickets[i];
            switch (i % 10)
            {
                case 0:
                    // Files of the new-ticket form: no comment, uploaded by the requester when the ticket was opened.
                    ticket.Attachments.Add(await NewAsync(ticket, ticket.RequesterId, ticket.CreatedAt, i, storage,
                        cancellationToken));
                    count++;
                    break;

                case 5 when ticket.Comments.FirstOrDefault(c => !c.IsInternal && c.AuthorUserId == ticket.RequesterId)
                    is { } reply:
                    await AttachToCommentAsync(ticket, reply, i, storage, cancellationToken);
                    count++;
                    break;

                case 7 when ticket.Comments.FirstOrDefault(c => c.IsInternal) is { } note:
                    // A file of an internal note: only staff can see or download it.
                    await AttachToCommentAsync(ticket, note, i, storage, cancellationToken);
                    count++;
                    break;
            }
        }

        return count;
    }

    private static async Task AttachToCommentAsync(Ticket ticket, TicketComment comment, int index, IFileStorage storage,
        CancellationToken cancellationToken)
    {
        var attachment = await NewAsync(ticket, comment.AuthorUserId, comment.CreatedAt, index, storage, cancellationToken);
        ticket.Attachments.Add(attachment);
        comment.Attachments.Add(attachment);
    }

    private static async Task<TicketAttachment> NewAsync(Ticket ticket, int uploadedBy, DateTime at, int index,
        IFileStorage storage, CancellationToken cancellationToken)
    {
        var isImage = index % 20 < 10;
        var content = isImage ? Png : Pdf;
        var names = isImage ? ImageNames : DocumentNames;

        using var stream = new MemoryStream(content, writable: false);
        var stored = await storage.SaveAsync(stream, AttachmentLimits.MaxFileBytes, cancellationToken);

        return new TicketAttachment
        {
            Ticket = ticket,
            OriginalFileName = names[index / 10 % names.Length],
            StoredFileName = stored.Name,
            ContentType = isImage ? PngContentType : PdfContentType,
            SizeBytes = (int)stored.SizeBytes,
            UploadedByUserId = uploadedBy,
            CreatedAt = at,
        };
    }

    /// <summary>A one-page PDF with a line of text, with a correct cross-reference table.</summary>
    private static byte[] BuildPdf(string text)
    {
        var page = $"BT /F1 18 Tf 72 720 Td ({text}) Tj ET";
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {page.Length} >>\nstream\n{page}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        ];

        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(pdf.Length); // ASCII only: characters = bytes
            pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = pdf.Length;
        pdf.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            pdf.Append($"{offset:D10} 00000 n \n");
        }

        pdf.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
