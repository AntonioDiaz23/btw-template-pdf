using Btw.TemplatePdf.Application.Common;
using Btw.TemplatePdf.Domain.Common;

namespace Btw.TemplatePdf.Application.Pdf;

public sealed record GeneratePdfByCufeRequest(
    string Nit,
    string Cufe,
    DocumentType DocumentType = DocumentType.Factura,
    /// <summary>When set, render with this template's published version instead of the pin/default.</summary>
    Guid? TemplateId = null,
    /// <summary>
    /// When true and a binding already exists, replace the pin with the rendered template.
    /// Requires <see cref="TemplateId"/>. Ignored on first render (always pins).
    /// </summary>
    bool ReplaceBinding = false);

public sealed record GeneratePdfByCufeResponse(
    string Nit,
    string Cufe,
    DocumentType DocumentType,
    Guid TemplateId,
    int TemplateVersion,
    string ContentType,
    string FileName,
    string PdfBase64,
    /// <summary>True when this CUFE already had a pinned template version from a previous render.</summary>
    bool ReusedPinnedTemplate = false,
    /// <summary>True when an existing pin was overwritten with a new template version.</summary>
    bool BindingReplaced = false);

public sealed class PdfGenerationException : AppException
{
    public PdfGenerationException(string code, string message) : base(code, message)
    {
    }
}
