using System.Text;
using Btw.TemplatePdf.Domain.Abstractions;
using Btw.TemplatePdf.Infrastructure.Pdf;
using PdfSharp.Pdf;

namespace Btw.TemplatePdf.Infrastructure.Tests;

public sealed class PdfSharpMetadataWriterTests
{
    [Fact]
    public void Apply_SetsBtwBrandedInfoDictionary()
    {
        var source = BlankPdf();
        var writer = new PdfSharpMetadataWriter();
        var metadata = new PdfFileMetadata(
            Title: "Factura electrónica · 900665411 · 8965881b",
            Author: "NIT 900665411",
            Subject: "CUFE ABCDEFGH",
            Keywords: "FE; NIT=900665411; CUFE=ABCDEFGH; template=11111111-1111-1111-1111-111111111111; v=2; type=Factura",
            Creator: "BTW Template PDF",
            Producer: "BTW Template PDF",
            CreationDateUtc: new DateTime(2026, 7, 19, 12, 0, 0, DateTimeKind.Utc),
            ModificationDateUtc: new DateTime(2026, 7, 19, 12, 0, 0, DateTimeKind.Utc));

        var result = writer.Apply(source, metadata);
        var latin1 = Encoding.Latin1.GetString(result);

        Assert.Contains("Factura electrónica", latin1, StringComparison.Ordinal);
        Assert.Contains("NIT 900665411", latin1, StringComparison.Ordinal);
        Assert.Contains("CUFE ABCDEFGH", latin1, StringComparison.Ordinal);
        Assert.Contains("BTW Template PDF", latin1, StringComparison.Ordinal);
        // Toolkit brand must not remain in Info or XMP producer fields.
        Assert.DoesNotContain("PDFsharp", latin1, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] BlankPdf()
    {
        using var document = new PdfDocument();
        document.AddPage();
        using var stream = new MemoryStream();
        document.Save(stream, closeStream: false);
        return stream.ToArray();
    }
}
