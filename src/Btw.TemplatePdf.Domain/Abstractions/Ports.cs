using Btw.TemplatePdf.Domain.Common;
using Btw.TemplatePdf.Domain.Invoices;
using Btw.TemplatePdf.Domain.Templates;

namespace Btw.TemplatePdf.Domain.Abstractions;

public interface ITemplateStore
{
    Task<TemplateDefinition?> GetPublishedAsync(
        string nit,
        DocumentType documentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the live published version of a specific template id.
    /// </summary>
    Task<TemplateDefinition?> GetPublishedByIdAsync(
        Guid templateId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a specific template version (published or historical) for pinned invoice renders.
    /// </summary>
    Task<TemplateDefinition?> GetByVersionAsync(
        Guid templateId,
        int versionNumber,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Remembers which template version was used the first time a CUFE was rendered to PDF.
/// Later template edits do not change already-bound invoices (logos/HTML stay as originally rendered).
/// </summary>
public interface IInvoiceTemplateBindingStore
{
    Task<InvoiceTemplateBinding?> FindAsync(
        string nit,
        string cufe,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        InvoiceTemplateBinding binding,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the pinned template for an existing CUFE (or inserts if missing).
    /// </summary>
    Task ReplaceAsync(
        InvoiceTemplateBinding binding,
        CancellationToken cancellationToken = default);
}

public sealed record InvoiceTemplateBinding(
    string Nit,
    string Cufe,
    DocumentType DocumentType,
    Guid TemplateId,
    int TemplateVersionNumber,
    DateTimeOffset BoundAt);

public interface IUblStore
{
    /// <summary>Returns raw UBL XML for the invoice, or null if missing.</summary>
    Task<string?> GetUblXmlAsync(
        string nit,
        string cufe,
        CancellationToken cancellationToken = default);
}

public interface IUblToViewModelMapper
{
    InvoiceViewModel Map(string nit, string cufe, string ublXml);
}

public interface IAssetStore
{
    Task<IReadOnlyDictionary<string, byte[]>> ResolveAsync(
        IEnumerable<TemplateAssetRef> assets,
        CancellationToken cancellationToken = default);
}

public interface IPdfRenderer
{
    Task<byte[]> RenderAsync(
        TemplateDefinition template,
        InvoiceViewModel invoice,
        IReadOnlyDictionary<string, byte[]> assets,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Writes PDF Info dictionary metadata onto an already-rendered PDF (in memory).
/// </summary>
public interface IPdfMetadataWriter
{
    byte[] Apply(byte[] pdfBytes, PdfFileMetadata metadata);
}

/// <summary>Standard PDF document information (Info dictionary).</summary>
public sealed record PdfFileMetadata(
    string Title,
    string Author,
    string Subject,
    string Keywords,
    string Creator,
    string Producer,
    DateTime CreationDateUtc,
    DateTime ModificationDateUtc);
