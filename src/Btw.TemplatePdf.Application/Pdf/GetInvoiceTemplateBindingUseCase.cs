using Btw.TemplatePdf.Application.Common;
using Btw.TemplatePdf.Domain.Abstractions;
using FluentValidation;

namespace Btw.TemplatePdf.Application.Pdf;

/// <summary>
/// Looks up whether a CUFE was already rendered (pinned in invoice_template_bindings).
/// </summary>
public sealed class GetInvoiceTemplateBindingUseCase
{
    private readonly IInvoiceTemplateBindingStore _bindings;
    private readonly IValidator<GetInvoiceTemplateBindingRequest> _validator;

    public GetInvoiceTemplateBindingUseCase(
        IInvoiceTemplateBindingStore bindings,
        IValidator<GetInvoiceTemplateBindingRequest> validator)
    {
        _bindings = bindings;
        _validator = validator;
    }

    public async Task<GetInvoiceTemplateBindingResponse> ExecuteAsync(
        GetInvoiceTemplateBindingRequest request,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAppAsync(request, cancellationToken).ConfigureAwait(false);

        var nit = NitNormalizer.Normalize(request.Nit);
        var cufe = request.Cufe.Trim();

        var binding = await _bindings
            .FindAsync(nit, cufe, cancellationToken)
            .ConfigureAwait(false);

        if (binding is null)
        {
            return new GetInvoiceTemplateBindingResponse(
                Exists: false,
                Nit: nit,
                Cufe: cufe);
        }

        return new GetInvoiceTemplateBindingResponse(
            Exists: true,
            Nit: binding.Nit,
            Cufe: binding.Cufe,
            DocumentType: binding.DocumentType,
            TemplateId: binding.TemplateId,
            TemplateVersion: binding.TemplateVersionNumber,
            BoundAt: binding.BoundAt);
    }
}
