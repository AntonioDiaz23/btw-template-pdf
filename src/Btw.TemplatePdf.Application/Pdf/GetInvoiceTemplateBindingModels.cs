using Btw.TemplatePdf.Domain.Common;

namespace Btw.TemplatePdf.Application.Pdf;

public sealed record GetInvoiceTemplateBindingRequest(string Nit, string Cufe);

public sealed record GetInvoiceTemplateBindingResponse(
    bool Exists,
    string Nit,
    string Cufe,
    DocumentType? DocumentType = null,
    Guid? TemplateId = null,
    int? TemplateVersion = null,
    DateTimeOffset? BoundAt = null);
