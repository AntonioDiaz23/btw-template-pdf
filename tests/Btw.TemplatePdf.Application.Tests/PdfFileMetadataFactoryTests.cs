using Btw.TemplatePdf.Application.Pdf;
using Btw.TemplatePdf.Domain.Common;

namespace Btw.TemplatePdf.Application.Tests;

public sealed class PdfFileMetadataFactoryTests
{
    [Fact]
    public void Create_BuildsExpectedBrandedFields_WithoutTemplateGuids()
    {
        var at = new DateTimeOffset(2026, 7, 19, 15, 0, 0, TimeSpan.Zero);

        var meta = PdfFileMetadataFactory.Create(
            nit: "900665411",
            cufe: "8965881bf911dc57cea84f152750c1e9",
            documentType: DocumentType.Factura,
            invoiceNumber: "AM12847",
            generatedAtUtc: at);

        Assert.Equal("AM12847", meta.Title);
        Assert.Equal("NIT 900665411", meta.Author);
        Assert.Equal("CUFE 8965881bf911dc57cea84f152750c1e9", meta.Subject);
        Assert.Equal(PdfFileMetadataFactory.AppName, meta.Creator);
        Assert.Equal(PdfFileMetadataFactory.AppName, meta.Producer);
        Assert.Equal("FE; NIT=900665411; CUFE=8965881bf911dc57cea84f152750c1e9; type=Factura", meta.Keywords);
        Assert.DoesNotContain("template=", meta.Keywords, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("11111111", meta.Keywords, StringComparison.Ordinal);
        Assert.Equal(at.UtcDateTime, meta.CreationDateUtc);
        Assert.Equal(at.UtcDateTime, meta.ModificationDateUtc);
    }

    [Fact]
    public void BuildFileName_UsesInvoiceNumber_LikeEveryone()
    {
        var invoice = new Domain.Invoices.InvoiceViewModel
        {
            Nit = "900665411",
            Cufe = "abc",
            Data = new Dictionary<string, object?>
            {
                ["documento"] = new Dictionary<string, object?>
                {
                    ["numero"] = "AM12847"
                }
            }
        };

        Assert.Equal("AM12847.pdf", PdfFileMetadataFactory.BuildFileName(invoice, "900665411", "abc"));
    }
}
