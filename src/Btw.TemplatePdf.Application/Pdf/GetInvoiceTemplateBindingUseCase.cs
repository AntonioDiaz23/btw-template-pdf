using Btw.TemplatePdf.Domain.Abstractions;
using Btw.TemplatePdf.Domain.Common;

namespace Btw.TemplatePdf.Application.Pdf;

public sealed record GetInvoiceTemplateBindingResponse(
    bool Exists,
    string Nit,
    string Cufe,
    string? DocumentType = null,
    Guid? TemplateId = null,
    int? TemplateVersion = null,
    DateTimeOffset? BoundAt = null);

public sealed class GetInvoiceTemplateBindingUseCase
{
    private readonly IInvoiceTemplateBindingStore _bindings;

    public GetInvoiceTemplateBindingUseCase(IInvoiceTemplateBindingStore bindings)
    {
        _bindings = bindings;
    }

    public async Task<GetInvoiceTemplateBindingResponse> ExecuteAsync(
        string nit,
        string cufe,
        CancellationToken cancellationToken = default)
    {
        var normalizedNit = NormalizeNit(nit);
        var normalizedCufe = (cufe ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(normalizedNit) || string.IsNullOrWhiteSpace(normalizedCufe))
        {
            return new GetInvoiceTemplateBindingResponse(
                Exists: false,
                Nit: normalizedNit,
                Cufe: normalizedCufe);
        }

        var binding = await _bindings
            .FindAsync(normalizedNit, normalizedCufe, cancellationToken)
            .ConfigureAwait(false);

        if (binding is null)
        {
            return new GetInvoiceTemplateBindingResponse(
                Exists: false,
                Nit: normalizedNit,
                Cufe: normalizedCufe);
        }

        return new GetInvoiceTemplateBindingResponse(
            Exists: true,
            Nit: binding.Nit,
            Cufe: binding.Cufe,
            DocumentType: ToApi(binding.DocumentType),
            TemplateId: binding.TemplateId,
            TemplateVersion: binding.TemplateVersionNumber,
            BoundAt: binding.BoundAt);
    }

    private static string NormalizeNit(string? nit)
    {
        if (string.IsNullOrWhiteSpace(nit)) return string.Empty;
        return new string(nit.Where(char.IsDigit).ToArray());
    }

    private static string ToApi(DocumentType type) => type switch
    {
        DocumentType.NotaCredito => "nota_credito",
        DocumentType.NotaDebito => "nota_debito",
        DocumentType.Otro => "otro",
        _ => "factura"
    };
}
