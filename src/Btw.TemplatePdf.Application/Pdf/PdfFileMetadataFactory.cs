using Btw.TemplatePdf.Domain.Abstractions;
using Btw.TemplatePdf.Domain.Common;
using Btw.TemplatePdf.Domain.Invoices;
using Btw.TemplatePdf.Domain.Templates;

namespace Btw.TemplatePdf.Application.Pdf;

/// <summary>
/// Builds PDF Info metadata branded as BTW Template PDF (not the rendering toolkit).
/// User-facing fields only — no template GUIDs / internal ids.
/// </summary>
public static class PdfFileMetadataFactory
{
    public const string AppName = "BTW Template PDF";

    public static PdfFileMetadata Create(
        string nit,
        string cufe,
        DocumentType documentType,
        string? invoiceNumber = null,
        DateTimeOffset? generatedAtUtc = null)
    {
        var now = (generatedAtUtc ?? DateTimeOffset.UtcNow).UtcDateTime;
        var shortCufe = cufe.Length <= 8 ? cufe : cufe[..8];
        var typeLabel = DocumentTypeLabel(documentType);
        var typeKey = documentType.ToString();
        var titleBase = string.IsNullOrWhiteSpace(invoiceNumber)
            ? $"{typeLabel} · {nit} · {shortCufe}"
            : invoiceNumber.Trim();

        return new PdfFileMetadata(
            Title: titleBase,
            Author: $"NIT {nit}",
            Subject: $"CUFE {cufe}",
            Keywords: $"FE; NIT={nit}; CUFE={cufe}; type={typeKey}",
            Creator: AppName,
            Producer: AppName,
            CreationDateUtc: now,
            ModificationDateUtc: now);
    }

    public static PdfFileMetadata FromTemplate(
        TemplateDefinition template,
        InvoiceViewModel invoice,
        DateTimeOffset? generatedAtUtc = null) =>
        Create(
            invoice.Nit,
            invoice.Cufe,
            template.DocumentType,
            ResolveInvoiceNumber(invoice),
            generatedAtUtc);

    /// <summary>
    /// Same convention as Everyone PDF formats: <c>NombreInvoice + ".pdf"</c>
    /// (invoice number / prefijo+número from UBL).
    /// </summary>
    public static string BuildFileName(InvoiceViewModel invoice, string nit, string cufe)
    {
        var nombreInvoice = ResolveInvoiceNumber(invoice);
        if (string.IsNullOrWhiteSpace(nombreInvoice))
        {
            var shortCufe = cufe.Length <= 8 ? cufe : cufe[..8];
            nombreInvoice = $"FE-{nit}-{shortCufe}";
        }

        return SanitizeFileName(nombreInvoice) + ".pdf";
    }

    public static string? ResolveInvoiceNumber(InvoiceViewModel invoice)
    {
        var fromDocumento = TryGet(invoice.Data, "documento", "numero");
        if (!string.IsNullOrWhiteSpace(fromDocumento))
            return fromDocumento.Trim();

        var root = TryGetRoot(invoice.Data, "numero");
        return string.IsNullOrWhiteSpace(root) ? null : root.Trim();
    }

    private static string DocumentTypeLabel(DocumentType documentType) =>
        documentType switch
        {
            DocumentType.Factura => "Factura electrónica",
            DocumentType.NotaCredito => "Nota crédito",
            DocumentType.NotaDebito => "Nota débito",
            _ => "Documento electrónico"
        };

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "documento" : cleaned.Trim();
    }

    private static string? TryGet(
        IReadOnlyDictionary<string, object?> data,
        string root,
        string key)
    {
        if (!data.TryGetValue(root, out var node) || node is not IDictionary<string, object?> map)
            return null;
        return map.TryGetValue(key, out var value) ? value?.ToString() : null;
    }

    private static string? TryGetRoot(IReadOnlyDictionary<string, object?> data, string key)
    {
        return data.TryGetValue(key, out var value) ? value?.ToString() : null;
    }
}
